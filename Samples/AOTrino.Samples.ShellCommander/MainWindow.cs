using DirectN.Extensions.Utilities;

namespace AOTrino.Samples.ShellCommander;

// a retro file manager that browses the Windows shell namespace, not just the file system.
// the left panel navigates through IShellItem (This PC, drives, libraries, archives opened as folders, virtual places), the right panel is a preview.
// this window stays NavigationMode.Local, so browsing goes through the shell host object rather than by navigating the WebView.
//
// the difference from the FileExplorer sample is the whole point:
// that one walks string paths, this one walks the shell namespace as objects, using the ShellN.Extensions NuGet package rather than redeclaring interop.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
#pragma warning disable IDE1006 // Naming Styles
    private const uint WM_SHOW_CONTEXT_MENU = MessageDecoder.WM_APP + 1;
#pragma warning restore IDE1006 // Naming Styles

    // ShellN's shell change notifier for the folder currently on screen, so the page follows adds, deletes and edits.
    // it owns its own STA thread and message-only window, we just feed it the folder and forward what it reports.
    private ChangeNotifier? _notifier;
    private string? _pendingContextMenuId;
    private TerminalApi? _terminal;

    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    // expose the shell backend as chrome.webview.hostObjects.shell (and its right-click menu, which only the window can
    // raise since it owns the HWND), and the console backend as chrome.webview.hostObjects.term.
    protected override void RegisterHostObjects()
    {
        AddHostObject("shell", new ShellApi(RequestContextMenu, WatchFolder));

        _terminal = new TerminalApi(RunScriptOnUiThread);
        AddHostObject("term", _terminal);

        AddHostObject("localization", new LocalizationApi());
    }

    // watch a folder for shell changes with ShellN's ChangeNotifier, stopping the previous one. called on the UI thread
    // from the bridge. the notifier runs on its own STA thread and raises Notified there, which OnShellChange forwards.
    private void WatchFolder(string id)
    {
        _notifier?.Notified -= OnShellChange;
        Interlocked.Exchange(ref _notifier, null)?.Dispose();
        try
        {
            using var item = ShellItem.FromParsingName(id, throwOnError: false);
            if (item == null)
                return;

            using var watchPidl = item.GetIdList(throwOnError: false);
            if (watchPidl is null)
                return;

            const ShellN.SHCNE_ID events = ShellN.SHCNE_ID.SHCNE_CREATE | ShellN.SHCNE_ID.SHCNE_DELETE |
                ShellN.SHCNE_ID.SHCNE_MKDIR | ShellN.SHCNE_ID.SHCNE_RMDIR | ShellN.SHCNE_ID.SHCNE_RENAMEITEM |
                ShellN.SHCNE_ID.SHCNE_RENAMEFOLDER | ShellN.SHCNE_ID.SHCNE_UPDATEITEM | ShellN.SHCNE_ID.SHCNE_UPDATEDIR;

            _notifier = new ChangeNotifier();
            _notifier.Notified += OnShellChange;
            _ = _notifier.Run(watchPidl, recursive: false, events: events);
        }
        catch (Exception ex)
        {
            Application.TraceWarning($"Failed to watch folder '{id}': {ex.Message}");
            // continue;
        }
    }

    // raised on the notifier's STA thread. the pidls are only valid during this call, so the payload is built here,
    // then the (plain string) script is marshaled to the UI thread to run on the page.
    private void OnShellChange(object? sender, ChangeNotifyEventArgs e)
    {
        var json = BuildChangeJson(e.Event, e.IdList1, e.IdList2);
        if (json != null)
        {
            RunScriptOnUiThread($"window.__shellChange({json})");
        }
    }

    private static string? BuildChangeJson(ShellN.SHCNE_ID? evt, ItemIdList? first, ItemIdList? second)
    {
        string action;
        ShellEntry? entry = null;
        string? oldId = null;
        string message;
        var fallbackName = Program._strings.GetString("Change_Item");

        switch (evt)
        {
            case ShellN.SHCNE_ID.SHCNE_CREATE:
            case ShellN.SHCNE_ID.SHCNE_MKDIR:
                action = "add";
                entry = EntryFrom(first);
                message = string.Format(Program._strings.GetString("Change_Added"), entry?.Name ?? first?.DisplayName ?? fallbackName);
                break;

            case ShellN.SHCNE_ID.SHCNE_DELETE:
            case ShellN.SHCNE_ID.SHCNE_RMDIR:
                action = "remove";
                oldId = first?.FullParsingName;
                message = string.Format(Program._strings.GetString("Change_Deleted"), first?.DisplayName ?? fallbackName);
                break;

            case ShellN.SHCNE_ID.SHCNE_RENAMEITEM:
            case ShellN.SHCNE_ID.SHCNE_RENAMEFOLDER:
                action = "rename";
                oldId = first?.FullParsingName;
                entry = EntryFrom(second);
                message = string.Format(Program._strings.GetString("Change_Renamed"), entry?.Name ?? second?.DisplayName ?? fallbackName);
                break;

            case ShellN.SHCNE_ID.SHCNE_UPDATEITEM:
                action = "update";
                entry = EntryFrom(first);
                message = string.Format(Program._strings.GetString("Change_Modified"), entry?.Name ?? first?.DisplayName ?? fallbackName);
                break;

            case ShellN.SHCNE_ID.SHCNE_UPDATEDIR:
                action = "refresh";
                message = Program._strings.GetString("Change_FolderChanged");
                break;

            default:
                return null;
        }

        var change = new ShellChange(action, entry, oldId, message);
        // System.Text.Json escapes quotes, HTML and non-ASCII by default, so the object literal is safe to embed.
        return JsonSerializer.Serialize(change, ShellCommanderJsonContext.Default.ShellChange);
    }

    // a ShellEntry read from a change notification's pidl, same shape the listing uses.
    private static ShellEntry? EntryFrom(ItemIdList? idList)
    {
        using var item = idList?.GetItem(throwOnError: false);
        if (item == null)
            return null;

        return new ShellEntry(
            item.SIGDN_NORMALDISPLAY ?? string.Empty,
            item.SIGDN_DESKTOPABSOLUTEPARSING ?? string.Empty,
            item.IsFolder,
            item.IsFolder ? -1 : (item.Size ?? -1),
            item.DateModified?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty);
    }

    // the console output pump raises this on a pool thread, so the script is posted to the UI thread to run on the page.
    private void RunScriptOnUiThread(string script) => RunTaskOnUIThread(() => ExecuteScript(script, throwOnError: false));

    // the preview frame loads local files over file://, the item's own path when it is on disk, or a temp copy
    // when it is not. allow a file:// page to reach other local files, which an in app preview needs.
    // safe here: the window stays Local, it never loads remote content, and this is the developer's explicit choice,
    // exactly where docs/SECURITY.md says the trust decision belongs.
    protected override CoreWebView2EnvironmentOptions? GetEnvironmentOptions()
    {
        var options = new CoreWebView2EnvironmentOptions();
        options.put_AdditionalBrowserArguments(PWSTR.From("--allow-file-access-from-files"));
        return options;
    }

    // called on the bridge thread from the host object, so the actual menu is deferred to the window's message loop.
    private void RequestContextMenu(string id)
    {
        _pendingContextMenuId = id;
        DirectN.Functions.PostMessageW(Handle, WM_SHOW_CONTEXT_MENU, default, default);
    }

    protected override void Dispose(bool disposing)
    {
        Interlocked.Exchange(ref _terminal, null).SafeDispose();
        Interlocked.Exchange(ref _notifier, null).SafeDispose();
        base.Dispose(disposing);
    }

    protected override LRESULT? WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM_SHOW_CONTEXT_MENU)
        {
            var id = _pendingContextMenuId;
            _pendingContextMenuId = null;
            if (!string.IsNullOrEmpty(id))
            {
                ShowContextMenu(id);
            }
            return 0;
        }

        // while a shell context menu is up its owner drawn items (icons, cascading verbs) need these messages.
        if ((msg == MessageDecoder.WM_INITMENUPOPUP ||
            msg == MessageDecoder.WM_MENUSELECT ||
            msg == MessageDecoder.WM_DRAWITEM ||
            msg == MessageDecoder.WM_MEASUREITEM ||
            msg == MessageDecoder.WM_MENUCHAR) && ShellItem.OnContextMenuWindowMessage(Handle, msg, wParam, lParam, out var result).IsSuccess)
            return result;

        return base.WindowProc(hwnd, msg, wParam, lParam);
    }

    // the real Windows right-click menu for one item, the same one Explorer shows, with its shell extension verbs.
    // the site gives the shell this window as the owner, so the menu and its handlers have an HWND to work against.
    private void ShowContextMenu(string id)
    {
        using var item = ShellItem.FromParsingName(id, throwOnError: false);
        if (item == null)
            return;

        using var site = new Site(this);
        item.ShowContextMenu(site, flags: ShellN.CMF.CMF_EXPLORE | ShellN.CMF.CMF_EXTENDEDVERBS | ShellN.CMF.CMF_CANRENAME);
    }

    // the COM site the shell context menu is given
    [System.Runtime.InteropServices.Marshalling.GeneratedComClass]
    private sealed partial class Site(MainWindow window) : DirectN.IServiceProvider, IObjectWithSite, IOleWindow, IDisposable
    {
        private nint _site;

        public HRESULT QueryService(in Guid guidService, in Guid riid, out nint ppvObject)
        {
            ppvObject = DirectN.Extensions.Com.ComObject.GetOrCreateComInstance(this, riid, CreateComInterfaceFlags.None);
            return ppvObject == 0 ? DirectN.Constants.E_NOINTERFACE : DirectN.Constants.S_OK;
        }

        public HRESULT GetSite(in Guid riid, out nint ppvSite)
        {
            if (_site != 0)
                return Marshal.QueryInterface(_site, riid, out ppvSite);

            ppvSite = 0;
            return DirectN.Constants.E_NOINTERFACE;
        }

        public HRESULT SetSite(nint pUnkSite)
        {
            Dispose();
            if (pUnkSite != 0)
            {
                Marshal.AddRef(pUnkSite);
            }

            _site = pUnkSite;
            return DirectN.Constants.S_OK;
        }

        public HRESULT ContextSensitiveHelp(BOOL fEnterMode) => DirectN.Constants.E_NOTIMPL;
        public HRESULT GetWindow(out HWND phwnd)
        {
            phwnd = window.Handle;
            return DirectN.Constants.S_OK;
        }

        public void Dispose()
        {
            var site = Interlocked.Exchange(ref _site, 0);
            if (site != 0)
            {
                Marshal.Release(site);
            }
        }
    }
}
