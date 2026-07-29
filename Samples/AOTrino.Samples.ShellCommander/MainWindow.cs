using DirectN.Extensions.Utilities;

namespace AOTrino.Samples.ShellCommander;

// a retro file manager that browses the Windows shell namespace, not just the file system.
// the left panel navigates through IShellItem (This PC, drives, libraries, archives opened as folders,
// virtual places), the right panel is a preview. this window stays NavigationMode.Local, so browsing goes
// through the shell host object rather than by navigating the WebView.
//
// the difference from the FileExplorer sample is the whole point: that one walks string paths, this one walks
// the shell namespace as objects, using the ShellN.Extensions NuGet package rather than redeclaring interop.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
#pragma warning disable IDE1006 // Naming Styles
    private const uint WM_SHOW_CONTEXT_MENU = MessageDecoder.WM_APP + 1;
#pragma warning restore IDE1006 // Naming Styles

    private string? _pendingContextMenuId;

    public MainWindow()
        : base(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>()!.Title)
    {
    }

    // expose the shell backend to JS as chrome.webview.hostObjects.shell, and let it ask this window for the
    // native right-click menu, which only the window can raise since it owns the HWND the menu attaches to.
    protected override void RegisterHostObjects() => AddHostObject("shell", new ShellApi(RequestContextMenu));

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

    // the site the shell context menu is given
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
