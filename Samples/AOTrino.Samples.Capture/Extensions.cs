using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;
using DirectN;
using WinRT;

namespace AOTrino.Samples.Capture;

internal static class Extensions
{
    // WinRT object -> IComObject<T>. replaces WinRT's As<T>, which misbehaves under AOT in Release builds.
    [return: NotNullIfNotNull(nameof(winRTObject))]
    public static IComObject<T>? AsComObject<T>(this object? winRTObject, CreateObjectFlags flags = CreateObjectFlags.UniqueInstance)
    {
        if (winRTObject == null)
            return null;

        var ptr = MarshalInspectable<object>.FromManaged(winRTObject);
        var obj = DirectN.Extensions.Com.ComObject.FromPointer<T>(ptr, flags);
        return obj ?? throw new InvalidCastException($"Object of type '{winRTObject.GetType().FullName}' is not of type '{typeof(T).FullName}'.");
    }

    // a WinRT IDirect3DSurface (capture frame) -> its underlying DXGI interface (e.g. IDXGISurface)
    public static IComObject<T>? AsDxgiComObject<T>(this object? winRTObject, CreateObjectFlags flags = CreateObjectFlags.UniqueInstance)
    {
        if (winRTObject == null)
            return null;

        using var access = winRTObject.AsComObject<IDirect3DDxgiInterfaceAccess>(flags) ??
            throw new InvalidCastException($"Object of type '{winRTObject.GetType().FullName}' is not of type '{nameof(IDirect3DDxgiInterfaceAccess)}'.");

        access.Object.GetInterface(typeof(T).GUID, out var ptr).ThrowOnError();
        var obj = DirectN.Extensions.Com.ComObject.FromPointer<T>(ptr, flags);
        return obj ?? throw new InvalidCastException($"Object of type '{winRTObject.GetType().FullName}' is not of type '{typeof(T).FullName}'.");
    }
}
