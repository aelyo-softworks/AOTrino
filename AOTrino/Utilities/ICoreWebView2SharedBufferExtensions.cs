namespace AOTrino.Utilities;

public static class ICoreWebView2SharedBufferExtensions
{
    public static ulong GetSize(this IComObject<ICoreWebView2SharedBuffer> buffer) => GetSize(buffer?.Object!);
    public static ulong GetSize(this ICoreWebView2SharedBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var value = default(ulong);
        buffer.get_Size(ref value).ThrowOnError();
        return value;
    }

    public static nint GetBuffer(this IComObject<ICoreWebView2SharedBuffer> buffer) => GetBuffer(buffer?.Object!);
    public static nint GetBuffer(this ICoreWebView2SharedBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.get_Buffer(out nint value).ThrowOnError();
        return value;
    }

    public static IStream OpenStream(this IComObject<ICoreWebView2SharedBuffer> buffer) => OpenStream(buffer?.Object!);
    public static IStream OpenStream(this ICoreWebView2SharedBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.OpenStream(out var value).ThrowOnError();
        return value;
    }

    public static HANDLE GetFileMappingHandle(this IComObject<ICoreWebView2SharedBuffer> buffer) => GetFileMappingHandle(buffer?.Object!);
    public static HANDLE GetFileMappingHandle(this ICoreWebView2SharedBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var value = default(HANDLE);
        buffer.get_FileMappingHandle(ref value).ThrowOnError();
        return value;
    }

    public static void Close(this IComObject<ICoreWebView2SharedBuffer> buffer) => Close(buffer?.Object!);
    public static void Close(this ICoreWebView2SharedBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.Close().ThrowOnError();
    }
}
