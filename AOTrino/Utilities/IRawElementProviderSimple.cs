namespace AOTrino.Utilities;

// the root UI Automation provider interface, which DirectN doesn't project.
// AOTrino neither implements it nor calls it: WebView2 hands one over for the page's tree, and this exists so
// that reference can be QueryInterface'd to the type UiaReturnRawElementProvider wants, rather than passed as
// a bare IUnknown that happens to point at the same vtable.
// https://learn.microsoft.com/windows/win32/api/uiautomationcore/nn-uiautomationcore-irawelementprovidersimple
[System.Runtime.InteropServices.Marshalling.GeneratedComInterface, Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")]
public partial interface IRawElementProviderSimple
{
    [PreserveSig]
    [return: MarshalAs(UnmanagedType.Error)]
    HRESULT get_ProviderOptions(out int value);

    [PreserveSig]
    [return: MarshalAs(UnmanagedType.Error)]
    HRESULT GetPatternProvider(int patternId, out IUnknown? value);

    [PreserveSig]
    [return: MarshalAs(UnmanagedType.Error)]
    HRESULT GetPropertyValue(int propertyId, out VARIANT value);

    [PreserveSig]
    [return: MarshalAs(UnmanagedType.Error)]
    HRESULT get_HostRawElementProvider(out IRawElementProviderSimple? value);
}
