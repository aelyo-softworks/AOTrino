using DirectN.Extensions.Utilities;

namespace AOTrino.Samples.ShellCommander;

// the shell backend, exposed as chrome.webview.hostObjects.shell. it lists the Windows shell namespace through ShellN.Extensions:
// ShellItem/ShellFolder, not string paths, so it reaches This PC, drives, libraries, archives opened as folders and virtual places the same way, and a listing crosses the bridge as one JSON string.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class ShellApi(Action<string> showContextMenu, Action<string> watchFolder) : DispatchObject
{
    // the page asks for an item's real Windows context menu by its parsing name, the window does the rest.
    public void ShowContextMenu(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            showContextMenu(id);
        }
    }

    // the page asks the window to watch this folder for shell changes (add/remove/rename/update). an empty id is the
    // Desktop. the window registers with the shell and pushes each change back so the page refreshes granularly and toasts it.
    public void Watch(string id) => watchFolder(id ?? string.Empty);

#pragma warning disable CA1822 // Mark members as static.

    // the well known parsing name of This PC
    public string GetThisPc() => $"::{ShellN.Constants.CLSID_MyComputer:B}";

    // a listing is not built and returned whole, it streams:
    // ListBegin binds the folder and hands back its header at once, while the rows enumerate on a background thread into a queue the page pulls with ListDrain.
    // so the first rows of a huge folder show right away instead of the page waiting for the last one. keyed by an opaque token.
    // the page size: how many rows one ListDrain hands back per call. large is fine now the list is virtualized,
    // growing the data and re-sorting a page is cheap and the page only rebuilds the rows actually in view.
    private const int _listPageSize = 4096;
    private readonly ConcurrentDictionary<string, ListJob> _listJobs = new();
    private string? _currentListToken;

    // one listing in flight: the rows enumerated so far, waiting to be drained, and whether enumeration is done.
    private sealed class ListJob
    {
        public ConcurrentQueue<ShellEntry> Queue { get; } = new();
        public CancellationTokenSource Cancellation { get; } = new();
        public volatile bool Done;
        public string? Error;
    }

    // begin listing a folder. an empty id means the Desktop, the root of the namespace.
    // the id is a desktop absolute parsing name, which round trips back through FromParsingName.
    // off the UI thread, binding a slow folder cannot hang it.
    public Task<string> ListBegin(string? id) => Task.Run(() =>
    {
        var token = Guid.NewGuid().ToString("N");
        try
        {
            // let a typed path use environment variables, e.g. %temp% or %windir%\System32. FromParsingName does not
            // expand them itself, and an id with no %VAR% (a shell parsing name, a plain path) comes back unchanged.
            if (!string.IsNullOrEmpty(id))
            {
                id = Environment.ExpandEnvironmentVariables(id);
            }

            // the Desktop is a shared singleton owned by the library, the rest we create here and dispose after enumerating.
            var owned = !string.IsNullOrEmpty(id);
            var folder = owned ? ShellItem.FromParsingName(id!, throwOnError: false) as ShellFolder : ShellFolder.Desktop;
            if (folder == null)
                return SerializeStart(new ListStart(token, string.Empty, id ?? string.Empty, null, Program._strings.GetString("Preview_NotFolder")));

            var name = folder.SIGDN_NORMALDISPLAY ?? string.Empty;
            var self = string.IsNullOrEmpty(id) ? string.Empty : (folder.SIGDN_DESKTOPABSOLUTEPARSING ?? id!);

            // Up goes to the parent's parsing name, except the Desktop, whose children go back to the empty root, and the Desktop itself, which has nowhere above it (null).
            string? parentId = null;
            if (!string.IsNullOrEmpty(id))
            {
                using var parent = folder.GetParent();
                parentId = parent == null || parent.IsDesktop ? string.Empty : (parent.SIGDN_DESKTOPABSOLUTEPARSING ?? string.Empty);
            }

            // supersedes the previous listing, so navigating away stops its enumeration instead of running it to the end.
            var previous = _currentListToken;
            _currentListToken = token;
            if (previous != null && _listJobs.TryRemove(previous, out var stale))
                stale.Cancellation.Cancel();

            var job = new ListJob();
            _listJobs[token] = job;
            _ = Task.Run(() => EnumerateInto(job, folder, owned));

            return SerializeStart(new ListStart(token, name, self, parentId, null));
        }
        catch (Exception ex)
        {
            return SerializeStart(new ListStart(token, string.Empty, id ?? string.Empty, null, ex.Message));
        }
    });

    // enumerate the folder's children into the job's queue on a background thread, the page drains them as they arrive.
    private static void EnumerateInto(ListJob job, ShellFolder folder, bool owned)
    {
        try
        {
            // no flags: the shell's default enumeration, which is folders and non-folders, visible items only.
            foreach (var child in folder.EnumerateChildren())
            {
                if (job.Cancellation.IsCancellationRequested)
                    break;

                // each child is disposed as soon as its display name, id, size and date are read.
                // Size and DateModified are fast (cached) shell properties, -1 for folders and virtual places.
                using (child)
                {
                    job.Queue.Enqueue(new ShellEntry(
                        child.SIGDN_NORMALDISPLAY ?? string.Empty,
                        child.SIGDN_DESKTOPABSOLUTEPARSING ?? string.Empty,
                        child.IsFolder,
                        child.IsFolder ? -1 : (child.Size ?? -1),
                        child.DateModified?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty));
                }
            }
        }
        catch (Exception ex)
        {
            job.Error = ex.Message;
        }
        finally
        {
            job.Done = true;
            if (owned)
            {
                folder.Dispose();
            }
        }
    }

    // hand the page the next batch of rows and say whether the listing is now complete.
    // quick, no shell work here, so it stays synchronous, the page keeps calling it until Done. an unknown token is a listing already superseded.
    public string ListDrain(string token)
    {
        if (!_listJobs.TryGetValue(token, out var job))
            return SerializeChunk(new ListChunk([], true, null));

        var batch = new List<ShellEntry>();
        while (batch.Count < _listPageSize && job.Queue.TryDequeue(out var entry))
        {
            batch.Add(entry);
        }

        var done = job.Done && job.Queue.IsEmpty;
        if (done)
        {
            _listJobs.TryRemove(token, out _);
        }

        return SerializeChunk(new ListChunk(batch, done, job.Error));
    }

    // the shell icons and thumbnails are cached as png files here, so a folder revisited, or the app reopened,
    // paints its images with no shell extraction and no decode, just a file:// load the WebView caches in turn.
    // this folder is left standing between runs on purpose, that persistence is the whole point of the cache.
    private static string IconCacheDir => Path.Combine(Path.GetTempPath(), "aotrino-shellcommander-icons");
    private static readonly ConcurrentDictionary<string, string> _iconUrls = new();

    // bounds how many run at once.
    private static readonly SemaphoreSlim _getIconGate = new(Math.Max(2, Environment.ProcessorCount));

    // the shell image for one item, as a file:// url the page drops straight into an <img>.
    // requested lazily and only for the rows in view, so listing a big folder stays fast and the images fill in behind it.
    // iconOnly picks the file type icon, otherwise the shell may return a thumbnail, an image's own small preview.
    public async Task<string> GetIcon(string id, int size, bool iconOnly, string timeStamp)
    {
        if (string.IsNullOrEmpty(id) || size <= 0)
            return string.Empty;

        // the key is everything that changes the pixels: the item, the size, icon versus thumbnail, and the stamp.
        var key = IconCacheKey(id, size, iconOnly, timeStamp);
        if (_iconUrls.TryGetValue(key, out var known))
            return known;

        var file = Path.Combine(IconCacheDir, key + ".png");
        if (File.Exists(file))
        {
            var hit = FileUrl(file);
            _iconUrls[key] = hit;
            return hit;
        }

        // the first GetImage call blocks unless the factory pends, so extraction must not run on the UI thread,
        // or a folder of slow thumbnails freezes the whole window.
        // offload it, gated so only a few run at once, and the extractor uses the async path so an E_PENDING factory is retried rather than dropped.
        // the bridge gets the Task straight away and the UI keeps pumping.
        await _getIconGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var url = await Task.Run(() => ExtractImageAsync(id, size, iconOnly, file)).ConfigureAwait(false);
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
    // shell items and IShellItemImageFactory are made and used here, off the UI thread.
    private static async Task<string?> ExtractImageAsync(string id, int size, bool iconOnly, string file)
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
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to extract image '{id}': {ex.Message}");
            return null;
        }
    }

    // a stable, filesystem safe cache name for one image. stable across runs, so the on disk cache survives a restart.
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

    // beyond images the document types are not guessed at, they are read from Windows, which is the point of this sample.
    // .pdf is the one WebView2 is documented to render itself.
    // text is whatever Windows perceives as text, and media is whatever the registry declares an audio or video content type, which WebView2 then tries to play.
    private static readonly Lazy<HashSet<string>> _mediaExtensions = new(() =>
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMediaExtensions(set, "audio");
        AddMediaExtensions(set, "video");

        // webview2 doesn't handle this one (and maybe others...)
        set.Remove(".mov");
        return set;
    });

    // the extensions Windows perceives as text, built once by asking AssocGetPerceivedType for every registered extension, so a preview never queries per file.
    // same idea as the media set, a different Windows source.
    private static readonly Lazy<HashSet<string>> _textExtensions = new(() =>
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ext in Registry.ClassesRoot.GetSubKeyNames())
        {
            if (!ext.StartsWith('.'))
                continue;

            if (PerceivedTypeOf(ext) == ShellN.PERCEIVED.PERCEIVED_TYPE_TEXT)
            {
                set.Add(ext);
            }
        }

        set.Add(".log");
        return set;
    });

    // the extensions Windows registers with an audio/ or video/ content type, the same MIME registration Explorer and every media app read.
    // asking the registry is the shell way to know what is media, rather than a stale list.
    private static void AddMediaExtensions(HashSet<string> set, string type)
    {
        try
        {
            foreach (var ext in Registry.ClassesRoot.GetSubKeyNames())
            {
                if (!ext.StartsWith('.'))
                    continue;

                using var key = Registry.ClassesRoot.OpenSubKey(ext);
                if (key?.GetValue("Content Type") is string contentType && contentType.StartsWith(type + "/", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(ext);
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
    // off the UI thread, so extracting or decoding a large item does not hang the window.
    public Task<string> GetPreview(string id) => Task.Run(() => GetPreviewCore(id));
    private string GetPreviewCore(string id)
    {
        try
        {
            using var item = ShellItem.FromParsingName(id, throwOnError: false);
            if (item == null || item.IsFolder)
                return SerializePreview(new PreviewResult(null, null, Program._strings.GetString("Preview_CannotPreview")));

            var name = item.SIGDN_NORMALDISPLAY ?? string.Empty;
            var ext = Path.GetExtension(name);

            var isNativeImage = _webView2ImageExtensions.Contains(ext);
            var isWicImage = !isNativeImage && WicImagingComponent.DecoderFileExtensions.Contains(ext);
            var isDocument = !isNativeImage && !isWicImage &&
                (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase)
                    || _textExtensions.Value.Contains(ext)
                    || _mediaExtensions.Value.Contains(ext));

            if (!isNativeImage && !isWicImage && !isDocument)
                return SerializePreview(new PreviewResult(null, null, Program._strings.GetString("Preview_NoTypePreview")));

            ClearPreviewDir();
            var source = ResolveToFile(item, name);
            if (source == null)
                return SerializePreview(new PreviewResult(null, null, Program._strings.GetString("Preview_CouldNotRead")));

            if (isWicImage)
            {
                var png = WicToPng(source);
                return SerializePreview(png != null ? new PreviewResult(png, "image", null) : new PreviewResult(null, null, Program._strings.GetString("Preview_CouldNotDecode")));
            }

            return SerializePreview(new PreviewResult(source, isNativeImage ? "image" : "document", null));
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to get preview '{id}': {ex.Message}");
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
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to open external '{url}': {ex.Message}");
            // continue
        }
    }

    // invoke the item's default action, the way double-clicking it in Explorer would. a file system item opens with
    // its default app, a virtual place opens in Explorer by its parsing name. this is the details card's Open button.
    public void OpenItem(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        try
        {
            using var item = ShellItem.FromParsingName(id, throwOnError: false);
            var path = item?.SIGDN_FILESYSPATH;
            if (!string.IsNullOrEmpty(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{id}\"", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to open item '{id}': {ex.Message}");
            // continue
        }
    }

    // the details of one item, read from the Windows property store, for folders and un-previewable files (and on demand).
    // off the UI thread, since a property read can touch the disk. everything here is what Explorer's details pane shows.
    public Task<string> GetDetails(string id) => Task.Run(() => GetDetailsCore(id));
    private static string GetDetailsCore(string id)
    {
        try
        {
            using var item = ShellItem.FromParsingName(id, throwOnError: false);
            if (item == null)
                return SerializeDetails(new ItemDetails(string.Empty, string.Empty, false, [], Program._strings.GetString("Details_NotAvailable")));

            var name = item.SIGDN_NORMALDISPLAY ?? string.Empty;
            var typeName = item.Type ?? Program._strings.GetString(item.IsFolder ? "Type_Folder" : "Type_File");
            return SerializeDetails(new ItemDetails(name, typeName, item.IsFolder, ReadProperties(item), null));
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to get details '{id}': {ex.Message}");
            return SerializeDetails(new ItemDetails(string.Empty, string.Empty, false, [], ex.Message));
        }
    }

    // every property the item carries, each a label/value/category row, labelled and formatted exactly as Explorer's
    // details pane does it through DirectN's PropertyDescription wrapper: DisplayName is the label, FormatForDisplay
    // formats the value (sizes, dates, durations, dimensions), so we format nothing. grouped and sorted by category.
    private static List<DetailRow> ReadProperties(ShellItem item)
    {
        var rows = new List<DetailRow>();
        using var store = item.GetPropertyStore(ShellN.GETPROPERTYSTOREFLAGS.GPS_DEFAULT, throwOnError: false);
        if (store != null)
        {
            foreach (var pair in store)
            {
                using var description = PropertyDescription.FromPropertyKey(pair.Key);
                if (description is null)
                    continue;

                var label = description.DisplayName.Nullify() ?? Conversions.Decamelize(description.Name.Nullify());
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var value = description.FormatForDisplay(pair.Value, PROPDESC_FORMAT_FLAGS.PDFF_DEFAULT);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                rows.Add(new DetailRow(label, value!, CategoryOf(description)));
            }
        }

        // group by category, then by label within it, so the page can render one header per category.
        rows.Sort(static (a, b) =>
        {
            var byCategory = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
            return byCategory != 0 ? byCategory : string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
        });
        return rows;
    }

    // the property's category: the last segment of its canonical name's namespace, which the wrapper already split out
    // (System.Size -> "System", System.Image.Dimensions -> "Image"). no canonical name groups under "Other".
    private static string CategoryOf(PropertyDescription description)
    {
        var ns = description.Namespace;
        if (string.IsNullOrEmpty(ns))
            return "Other";

        var lastDot = ns.LastIndexOf('.');
        return lastDot >= 0 ? ns[(lastDot + 1)..] : ns;
    }

    // open the item's location in real Explorer: a folder opens in place, a file opens its folder with it selected.
    public void RevealInExplorer(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
            {
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "shell:Desktop", UseShellExecute = true });
                return;
            }

            using var item = ShellItem.FromParsingName(id, throwOnError: false);
            var path = item?.SIGDN_FILESYSPATH;
            string arguments;
            if (item?.IsFolder != false)
            {
                arguments = $"\"{(string.IsNullOrEmpty(path) ? id : path)}\"";
            }
            else
            {
                arguments = $"/select,\"{(string.IsNullOrEmpty(path) ? id : path)}\"";
            }

            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = arguments, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to reveal in explorer '{id}': {ex.Message}");
            // continue
        }
    }

    // write the page's themed markdown html to a temp file and return its path. the preview loads it over file://,
    // NOT as an iframe srcdoc: an about:srcdoc document cannot load file:// images or follow relative file:// links even with a base href,
    // a real file:// document (with --allow-file-access-from-files) can.
    // same folder as the other preview temp files, one fixed name so it does not accumulate, the page cache busts it with a query.
    public string WriteMarkdownHtml(string html)
    {
        try
        {
            Directory.CreateDirectory(PreviewDir);
            var path = Path.Combine(PreviewDir, "markdown.html");
            File.WriteAllText(path, html ?? string.Empty, Encoding.UTF8);
            return path;
        }
        catch
        {
            return string.Empty;
        }
    }

    // markdown, rendered to html on this side with Markdig, so the preview frame shows a formatted document rather than the raw source.
    // the folder is handed back too, so relative images and links resolve.
    // off the UI thread, so reading (or extracting) and rendering a large document does not hang the window.
    public Task<string> GetMarkdown(string id) => Task.Run(() => GetMarkdownCore(id));
    private string GetMarkdownCore(string id)
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
                {
                    baseHref = new Uri(dir.EndsWith(Path.DirectorySeparatorChar) ? dir : dir + Path.DirectorySeparatorChar).AbsoluteUri;
                }
            }

            return SerializeMarkdown(new MarkdownResult(html, baseHref));
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to get markdown '{id}': {ex.Message}");
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
            using var file = File.Create(png);
            source.Save(file, WicCodec.GUID_ContainerFormatPng);
            return png;
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"Failed to png from Wic '{sourceFile}': {ex.Message}");
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

    private static string SerializeStart(ListStart start) => JsonSerializer.Serialize(start, ShellCommanderJsonContext.Default.ListStart);
    private static string SerializeChunk(ListChunk chunk) => JsonSerializer.Serialize(chunk, ShellCommanderJsonContext.Default.ListChunk);
    private static string SerializePreview(PreviewResult result) => JsonSerializer.Serialize(result, ShellCommanderJsonContext.Default.PreviewResult);
    private static string SerializeMarkdown(MarkdownResult result) => JsonSerializer.Serialize(result, ShellCommanderJsonContext.Default.MarkdownResult);
    private static string SerializeDetails(ItemDetails details) => JsonSerializer.Serialize(details, ShellCommanderJsonContext.Default.ItemDetails);
}
