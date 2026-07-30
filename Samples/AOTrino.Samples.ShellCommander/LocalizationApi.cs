namespace AOTrino.Samples.ShellCommander;

// the page's strings, exposed as chrome.webview.hostObjects.localization.
// the whole catalog crosses once at startup rather than a call per string, so the page's t(key) is a local lookup, see AOTrino.Localization.
// this sample ships one language, so there is no picker here, only the catalog.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class LocalizationApi : DispatchObject
{
#pragma warning disable CA1822 // Mark members as static.
    public string GetCatalog() => Program._strings.GetCatalogJson();
#pragma warning restore CA1822
}
