using System.Runtime.InteropServices.Marshalling;

namespace AOTrino;

// note we can't reuse the one from DirectN because of desing issues with COM interop on .NET core source generators,
// so we need to define our own IDispatch interface here
[GeneratedComInterface, Guid("00020400-0000-0000-c000-000000000046")]
public partial interface IDispatch
{
    [PreserveSig]
    [return: MarshalAs(UnmanagedType.Error)]
    HRESULT GetTypeInfoCount(out uint pctinfo);

    [PreserveSig]
    [return: MarshalAs(UnmanagedType.Error)]
    HRESULT GetTypeInfo(uint iTInfo, uint lcid, [MarshalUsing(typeof(UniqueComInterfaceMarshaller<ITypeInfo>))] out ITypeInfo ppTInfo);

    [PreserveSig]
    [return: MarshalAs(UnmanagedType.Error)]
    HRESULT GetIDsOfNames(in Guid riid, [In][MarshalUsing(CountElementName = nameof(cNames))] PWSTR[] rgszNames, uint cNames, uint lcid, [In][Out][MarshalUsing(CountElementName = nameof(cNames))] int[] rgDispId);

    [PreserveSig]
    [return: MarshalAs(UnmanagedType.Error)]
    HRESULT Invoke(int dispIdMember, in Guid riid, uint lcid, DISPATCH_FLAGS wFlags, in DISPPARAMS pDispParams, nint /* optional VARIANT* */ pVarResult, nint /* optional EXCEPINFO* */ pExcepInfo, nint /* optional uint* */ puArgErr);
}
