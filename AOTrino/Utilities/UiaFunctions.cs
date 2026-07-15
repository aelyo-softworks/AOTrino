using System.Runtime.InteropServices.Marshalling;

namespace AOTrino.Utilities;

internal static partial class UiaFunctions
{
    // "give me your UI Automation provider".
    // https://learn.microsoft.com/windows/win32/winauto/uiauto-serverside-provider-events
#pragma warning disable IDE1006 // Naming Styles
    internal const int UiaRootObjectId = -25;
#pragma warning restore IDE1006 // Naming Styles

    // hands UIA the provider a window wants to be seen through
    // returns the LRESULT the window proc must return for WM_GETOBJECT. UIA takes its own reference on el.
    // https://learn.microsoft.com/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiareturnrawelementprovider
    [LibraryImport("UIAUTOMATIONCORE")]
    [PreserveSig]
    internal static partial LRESULT UiaReturnRawElementProvider(HWND hwnd, WPARAM wParam, LPARAM lParam, [MarshalUsing(typeof(UniqueComInterfaceMarshaller<IRawElementProviderSimple>))] IRawElementProviderSimple? el);
}
