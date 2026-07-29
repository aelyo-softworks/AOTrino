namespace AOTrino.Samples.ShellCommander;

// the shell backend, exposed as chrome.webview.hostObjects.shell. it lists the Windows shell namespace
// through ShellN.Extensions: ShellItem/ShellFolder, not string paths, so it reaches This PC, drives, libraries,
// archives opened as folders and virtual places the same way, and a listing crosses the bridge as one JSON string.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class ShellApi(Action<string> showContextMenu) : DispatchObject
{
    // the page asks for an item's real Windows context menu by its parsing name, the window does the rest.
    public void ShowContextMenu(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            showContextMenu(id);
        }
    }

    // bridge invokes members on the instance, so they stay instance members even without instance state.
#pragma warning disable CA1822 // Mark members as static.

    // the well known parsing name of This PC
    public string GetThisPc() => $"::{ShellN.Constants.CLSID_MyComputer:B}";

    // list a shell folder's children. an empty id means the Desktop, the root of the namespace.
    // the id is a desktop absolute parsing name, which round trips back through FromParsingName, which is how the
    // page names the folder to open next.
    public string List(string? id)
    {
        // the Desktop is a shared singleton owned by the library, the rest we create here and dispose ourselves.
        ShellFolder? folder = null;
        var owned = false;
        try
        {
            if (string.IsNullOrEmpty(id))
            {
                folder = ShellFolder.Desktop;
            }
            else
            {
                folder = ShellItem.FromParsingName(id, throwOnError: false) as ShellFolder;
                owned = true;
            }

            if (folder == null)
                return Serialize(new ShellListing(string.Empty, id ?? string.Empty, null, "This is not a folder that can be listed.", []));

            var name = folder.SIGDN_NORMALDISPLAY ?? string.Empty;
            var self = string.IsNullOrEmpty(id) ? string.Empty : (folder.SIGDN_DESKTOPABSOLUTEPARSING ?? id!);

            // Up goes to the parent's parsing name, except the Desktop, whose children go back to the empty root,
            // and the Desktop itself, which has nowhere above it (null).
            string? parentId = null;
            if (!string.IsNullOrEmpty(id))
            {
                using var parent = folder.GetParent();
                parentId = parent == null || parent.IsDesktop ? string.Empty : (parent.SIGDN_DESKTOPABSOLUTEPARSING ?? string.Empty);
            }

            // no flags: the shell's default enumeration, which is folders and non-folders, visible items only.
            var entries = new List<ShellEntry>();
            foreach (var child in folder.EnumerateChildren())
            {
                // owned children, so each is disposed as soon as its display name, id, size and date are read.
                // Size and DateModified are fast (cached) shell properties, null for folders and virtual places.
                using (child)
                {
                    entries.Add(new ShellEntry(
                        child.SIGDN_NORMALDISPLAY ?? string.Empty,
                        child.SIGDN_DESKTOPABSOLUTEPARSING ?? string.Empty,
                        child.IsFolder,
                        child.IsFolder ? -1 : (child.Size ?? -1),
                        child.DateModified?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty));
                }
            }

            // folders before items, each ordered by display name, the way a file manager lists a folder.
            entries.Sort(static (a, b) => a.IsFolder != b.IsFolder ? (a.IsFolder ? -1 : 1) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return Serialize(new ShellListing(name, self, parentId, null, entries));
        }
        catch (Exception ex)
        {
            return Serialize(new ShellListing(string.Empty, id ?? string.Empty, null, ex.Message, []));
        }
        finally
        {
            if (owned)
            {
                folder?.Dispose();
            }
        }
    }

    // the shell icons and thumbnails are cached as png files here, so a folder revisited, or the app reopened,
    // paints its images with no shell extraction and no decode, just a file:// load the WebView caches in turn.
    // this folder is left standing between runs on purpose, that persistence is the whole point of the cache.
    private static string IconCacheDir => Path.Combine(Path.GetTempPath(), "aotrino-shellcommander-icons");
    private static readonly ConcurrentDictionary<string, string> _iconUrls = new();

    // extraction runs off the UI thread (below), this bounds how many run at once so a big folder does not spawn a
    // thread per visible row hammering the shell. the rest queue on the gate, still off the UI thread.
    private static readonly SemaphoreSlim _getIconGate = new(Math.Max(2, Environment.ProcessorCount));

    // the shell image for one item, as a file:// url the page drops straight into an <img>. requested lazily and only
    // for the rows in view, so listing a big folder stays fast and the images fill in behind it.
    // iconOnly picks the file type icon, otherwise the shell may return a thumbnail, an image's own small preview.
    // stamp is the item's modified time from the listing, folded into the cache key so an edited file rethumbnails.
    public async Task<string> GetIcon(string id, int size, bool iconOnly, string stamp)
    {
        if (string.IsNullOrEmpty(id) || size <= 0)
            return string.Empty;

        // the key is everything that changes the pixels: the item, the size, icon versus thumbnail, and the stamp.
        var key = IconCacheKey(id, size, iconOnly, stamp);
        if (_iconUrls.TryGetValue(key, out var known))
            return known;

        var file = Path.Combine(IconCacheDir, key + ".png");
        if (File.Exists(file))
        {
            var hit = FileUrl(file);
            _iconUrls[key] = hit;
            return hit;
        }

        // the first GetImage call blocks unless the factory pends, so extraction must not run on the UI thread, or a
        // folder of slow thumbnails freezes the whole window. offload it, gated so only a few run at once, and the
        // extractor uses the async path so an E_PENDING factory is retried rather than dropped. the bridge gets the
        // Task straight away and the UI keeps pumping.
        await _getIconGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var url = await Task.Run(() => ExtractIconAsync(id, size, iconOnly, file)).ConfigureAwait(false);
            if (url != null)
            {
                _iconUrls[key] = url;
                return url;
            }
        }
        finally
        {
            _getIconGate.Release();
        }

        return string.Empty;
    }

    // binds the item, extracts its shell image and writes it to the cache file, all on a background thread.
    // shell items and IShellItemImageFactory are made and used here, off the UI thread, the way Explorer thumbnails.
    private static async Task<string?> ExtractIconAsync(string id, int size, bool iconOnly, string file)
    {
        try
        {
            using var item = ShellItem.FromParsingName(id, throwOnError: false);
            if (item == null)
                return null;

            var flags = ShellN.SIIGBF.SIIGBF_RESIZETOFIT;
            if (iconOnly)
            {
                flags |= ShellN.SIIGBF.SIIGBF_ICONONLY;
            }

            // the async variant handles the E_PENDING retry the underlying factory can ask for, the sync one does not.
            // we are already off the UI thread (Task.Run below), so the first call is free to block.
            using var image = await item.GetImageAsBitmapAsync(new SIZE(size, size), flags).ConfigureAwait(false);
            if (image == null)
                return null;

            Directory.CreateDirectory(IconCacheDir);
            using (var bmp = new WicBitmapSource(image))
            {
                // write to a unique temp name then move into place, so two rows racing on one file cannot tear it.
                var temp = file + ".tmp-" + Guid.NewGuid().ToString("N");
                using (var output = File.Create(temp))
                {
                    bmp.Save(output, WicCodec.GUID_ContainerFormatPng);
                }
                File.Move(temp, file, overwrite: true);
            }

            return FileUrl(file);
        }
        catch
        {
            return null;
        }
    }

    // a stable, filesystem safe cache name for one image. stable across runs, so the on disk cache survives a restart,
    // which a randomized string hash would not give, hence the content hash.
    private static string IconCacheKey(string id, int size, bool iconOnly, string stamp)
    {
        var raw = $"{size}|{(iconOnly ? 1 : 0)}|{stamp}|{id}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static string FileUrl(string path) => new Uri(path).AbsoluteUri;

    // one folder holds the preview temp files, cleared before each preview so they never pile up.
    private static string PreviewDir => Path.Combine(Path.GetTempPath(), "aotrino-shellcommander-preview");

    // a preview is not a copy tool, so an oversized virtual item is refused rather than extracted whole.
    private const long _maxPreviewBytes = 256 * 1024 * 1024;
    private const long _maxTextBytes = 4 * 1024 * 1024;

    // raw html is disabled so the rendered document the page shows unsandboxed can carry no script of its own.
    private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    // the image types WebView2 renders natively, from its own documentation. anything else that is an image is left
    // to WIC, whose installed decoders are queried live rather than guessed at, see WicImagingComponent.
    private static readonly HashSet<string> _webView2ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".apng", ".png", ".avif", ".gif", ".jpg", ".jpeg", ".jpe", ".jif", ".jfif",
        ".pjpeg", ".pjp", ".svg", ".webp", ".bmp", ".ico", ".cur",
    };

    // beyond images the document types are not guessed at, they are read from Windows, which is the point of this
    // sample. .pdf is the one WebView2 is documented to render itself. text is whatever Windows perceives as text,
    // and media is whatever the registry declares an audio or video content type, which WebView2 then tries to play.
    private static readonly Lazy<HashSet<string>> _mediaExtensions = new(() =>
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMediaExtensions(set, "audio");
        AddMediaExtensions(set, "video");

        // webview2 doesn't handle this one (and maybe others...)
        set.Remove(".mov");
        return set;
    });

    // the extensions Windows perceives as text, built once by asking AssocGetPerceivedType for every registered
    // extension, so a preview never queries per file. same idea as the media set, a different Windows source.
    private static readonly Lazy<HashSet<string>> _textExtensions = new(() =>
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var ext in Registry.ClassesRoot.GetSubKeyNames())
            {
                if (!ext.StartsWith('.'))
                    continue;

                if (PerceivedTypeOf(ext) == ShellN.PERCEIVED.PERCEIVED_TYPE_TEXT)
                {
                    set.Add(ext);
                }
            }
        }
        catch
        {
            // continue
        }

        set.Add(".log");
        return set;
    });

    // the extensions Windows registers with an audio/ or video/ content type, the same MIME registration Explorer
    // and every media app read. asking the registry is the shell way to know what is media, rather than a stale list.
    private static void AddMediaExtensions(HashSet<string> set, string type)
    {
        try
        {
            foreach (var ext in Registry.ClassesRoot.GetSubKeyNames())
            {
                if (!ext.StartsWith('.'))
                    continue;

                try
                {
                    using var key = Registry.ClassesRoot.OpenSubKey(ext);
                    if (key?.GetValue("Content Type") is string contentType && contentType.StartsWith(type + "/", StringComparison.OrdinalIgnoreCase))
                        set.Add(ext);
                }
                catch
                {
                    // continue
                }
            }
        }
        catch
        {
            // continue
        }
    }

    // Windows' own simple classification of an extension (Text, Image, Audio, Video, Document...), via AssocGetPerceivedType.
    // doesn't work for all extensions, but still interesting.
    private static ShellN.PERCEIVED PerceivedTypeOf(string ext)
    {
        if (string.IsNullOrEmpty(ext))
            return ShellN.PERCEIVED.PERCEIVED_TYPE_UNKNOWN;

        ShellN.Functions.AssocGetPerceivedType(PWSTR.From(ext), out var perceived, out _, 0);
        return perceived;
    }

    // the file the WebView should load to preview this item, and how to show it.
    // a type WebView2 renders itself, an image it knows or a document (pdf, media, text), is handed over as is.
    // a type only WIC can read, a .heic say (with proper extensions installed), is decoded to a png.
    // either way the source is the item's own path when it is on disk, or a shell copy of it into a temp folder when it is not,
    // which is what makes a file inside a, say, .7z work (Windows 11): some namespace handlers offer no readable stream, but the shell can always copy the item out.
    public string GetPreview(string id)
    {
        try
        {
            using var item = ShellItem.FromParsingName(id, throwOnError: false);
            if (item == null || item.IsFolder)
                return SerializePreview(new PreviewResult(null, null, "This item cannot be previewed."));

            var name = item.SIGDN_NORMALDISPLAY ?? string.Empty;
            var ext = Path.GetExtension(name);

            var isNativeImage = _webView2ImageExtensions.Contains(ext);
            var isWicImage = !isNativeImage && WicImagingComponent.DecoderFileExtensions.Contains(ext);
            var isDocument = !isNativeImage && !isWicImage &&
                (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase)
                    || _textExtensions.Value.Contains(ext)
                    || _mediaExtensions.Value.Contains(ext));

            if (!isNativeImage && !isWicImage && !isDocument)
                return SerializePreview(new PreviewResult(null, null, "No preview for this file type."));

            ClearPreviewDir();
            var source = ResolveToFile(item, name);
            if (source == null)
                return SerializePreview(new PreviewResult(null, null, "This item could not be read for preview."));

            if (isWicImage)
            {
                var png = WicToPng(source);
                return SerializePreview(png != null ? new PreviewResult(png, "image", null) : new PreviewResult(null, null, "This image could not be decoded."));
            }

            return SerializePreview(new PreviewResult(source, isNativeImage ? "image" : "document", null));
        }
        catch (Exception ex)
        {
            return SerializePreview(new PreviewResult(null, null, ex.Message));
        }
    }

    // the item's own path when it is on disk, or a shell copy of it in the (already cleared) preview folder when not.
    private static string? ResolveToFile(ShellItem item, string name)
    {
        var path = item.SIGDN_FILESYSPATH;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            return path;

        if ((item.Size ?? 0) > _maxPreviewBytes)
            return null;

        return ExtractToTemp(item, name);
    }

    // opens a url or path with the shell, so an external link clicked in the markdown preview goes to the browser.
    public void OpenExternal(string url)
    {
        if (string.IsNullOrEmpty(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // continue
        }
    }

    // markdown, rendered to html on this side with Markdig, so the preview frame shows a formatted document rather than the raw source.
    // the folder is handed back too, so relative images and links resolve.
    public string GetMarkdown(string id)
    {
        try
        {
            using var item = ShellItem.FromParsingName(id, throwOnError: false);
            if (item == null)
                return SerializeMarkdown(new MarkdownResult(string.Empty, string.Empty));

            var text = ReadAllText(item);
            var html = text == null ? string.Empty : Markdown.ToHtml(text, _markdownPipeline);

            var baseHref = string.Empty;
            var path = item.SIGDN_FILESYSPATH;
            if (!string.IsNullOrEmpty(path))
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    baseHref = new Uri(dir.EndsWith(Path.DirectorySeparatorChar) ? dir : dir + Path.DirectorySeparatorChar).AbsoluteUri;
            }

            return SerializeMarkdown(new MarkdownResult(html, baseHref));
        }
        catch (Exception ex)
        {
            return SerializeMarkdown(new MarkdownResult($"<p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p>", string.Empty));
        }
    }

    // copy the item into the preview folder with the shell copy engine,
    // which extracts it from an archive or another virtual namespace where a plain stream binding is not offered,
    // and returns the copied file. this is the whole reason a file inside a .7z can be previewed: OpenReadStream returns nothing there, but the shell can copy it.
    private static string? ExtractToTemp(ShellItem item, string name)
    {
        using var destinationFolder = ShellItem.FromParsingName(PreviewDir, throwOnError: false);
        if (destinationFolder == null)
            return null;

        using var operation = new FileOperation();
        operation.SetOperationFlags(ShellN.FILEOPERATION_FLAGS.FOF_NO_UI, throwOnError: false);
        if (operation.CopyItem(item, destinationFolder, name, throwOnError: false).IsError)
            return null;

        if (operation.PerformOperations(throwOnError: false).IsError)
            return null;

        var destination = Path.Combine(PreviewDir, name);
        return File.Exists(destination) ? destination : null;
    }

    // decode an image WebView2 cannot read with WIC and re-encode it as a png in the preview folder.
    private static string? WicToPng(string sourceFile)
    {
        try
        {
            using var source = WicBitmapSource.Load(sourceFile);
            Directory.CreateDirectory(PreviewDir);
            var png = Path.Combine(PreviewDir, Guid.NewGuid().ToString("N") + ".png");
            using (var file = File.Create(png))
            {
                source.Save(file, WicCodec.GUID_ContainerFormatPng);
            }
            return png;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadAllText(ShellItem item)
    {
        var path = item.SIGDN_FILESYSPATH;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            // a markdown inside an archive: extract it first, its handler may offer no stream, then read the copy.
            if ((item.Size ?? 0) > _maxTextBytes)
                return null;

            ClearPreviewDir();
            path = ExtractToTemp(item, item.SIGDN_NORMALDISPLAY ?? "readme.md");
            if (path == null)
                return null;
        }
        else if (new FileInfo(path).Length > _maxTextBytes)
            return null;

        return File.ReadAllText(path);
    }

    // a fresh, empty preview folder for the current preview, so its temp files never accumulate.
    private static void ClearPreviewDir()
    {
        try
        {
            if (!Directory.Exists(PreviewDir))
            {
                Directory.CreateDirectory(PreviewDir);
                return;
            }

            foreach (var file in Directory.EnumerateFiles(PreviewDir))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // continue
                }
            }
        }
        catch
        {
            // continue
        }
    }

#pragma warning restore CA1822

    private static string Serialize(ShellListing listing) => JsonSerializer.Serialize(listing, ShellCommanderJsonContext.Default.ShellListing);
    private static string SerializePreview(PreviewResult result) => JsonSerializer.Serialize(result, ShellCommanderJsonContext.Default.PreviewResult);
    private static string SerializeMarkdown(MarkdownResult result) => JsonSerializer.Serialize(result, ShellCommanderJsonContext.Default.MarkdownResult);
}
