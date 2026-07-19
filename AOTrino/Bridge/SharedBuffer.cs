namespace AOTrino.Bridge;

// a named block of shared memory between .NET and JS (WebView2 CreateSharedBuffer).
// this is a GENERIC byte channel, it knows nothing about rendering. .NET gets a raw pointer.
// JS gets the same memory (zero-copy) as an ArrayBuffer via window.__aotrino.getBuffer(name) / onBuffer(name, cb).
// grow-only, call Post() to (re)hand the buffer to the page (e.g. after a resize) with optional metadata.
// created ReadWrite it is bidirectional (both sides read/write).
// specializations such as AOTrino.Graphics.Direct2DSurface build a renderer on top of this.
public class SharedBuffer : IDisposable
{
    // the generic __aotrino runtime (embedded resource, loaded/cached from the assembly).
    // injected once per window (WebViewWindow.EnsureSharedRuntime). exposes window.__aotrino: getBuffer/getMeta/onBuffer/post.
    internal static string Runtime => EmbeddedResource.Load("SharedBuffer.Runtime.js");

    // shared memory grows a chunk at a time (never per byte) so a continuously resizing consumer rarely reallocates,
    // the slack is bounded by one chunk and by screen size. call Trim() to reclaim it.
    private const int _growthChunk = 1024 * 1024;

    private readonly WebViewWindow _window;
    private readonly string _name;
    private readonly SharedBufferAccess _access;
    private ComObject<ICoreWebView2SharedBuffer>? _buffer;
    private int _capacity; // allocated bytes (grows in _growthChunk steps).
    private int _size; // last requested bytes (the actually-needed size).

    public SharedBuffer(WebViewWindow window, string name, SharedBufferAccess access)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrEmpty(name);

        _window = window;
        _name = name;
        _access = access;
    }

    public string Name => _name;
    public SharedBufferAccess Access => _access;

    // the last requested size in bytes (what the consumer actually needs).
    public int Size => _size;

    // the allocated size in bytes (>= Size, grows a chunk at a time). reclaim slack with Trim().
    public int Capacity => _capacity;

    // raw pointer to the shared memory, or 0 if not allocated yet.
    public nint Pointer
    {
        get
        {
            if (_buffer == null || _buffer.IsDisposed)
                return 0;

            _buffer.Object.get_Buffer(out var pointer).ThrowOnError();
            return pointer;
        }
    }

    // ensure the buffer holds at least byteLength bytes (grow-only).
    // returns true if it was (re)allocated (in which case you must Post() it again so JS gets the new memory).
    // the allocation grows a chunk at a time, so a continuously growing consumer (e.g. a window resize) only reallocates on chunk boundaries.
    public virtual bool EnsureSize(int byteLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);
        _size = byteLength;
        if (_buffer != null && !_buffer.IsDisposed && _capacity >= byteLength)
            return false;

        var chunks = (byteLength + _growthChunk - 1) / _growthChunk;
        Allocate(chunks * _growthChunk);
        return true;
    }

    // reclaim the slack capacity by reallocating down to the last requested size (Size). developer opt-in,
    // typically after a resize settles. returns true if it reallocated (Post() again so JS gets the new memory).
    public virtual bool Trim()
    {
        if (_buffer == null || _buffer.IsDisposed || _size <= 0 || _capacity <= _size)
            return false;

        Allocate(_size);
        return true;
    }

    private void Allocate(int capacity)
    {
        var environment = _window.SharedEnvironment ?? throw new InvalidOperationException("The WebView2 environment is not ready.");
        Interlocked.Exchange(ref _buffer, null)?.Dispose();
        environment.Object.CreateSharedBuffer((ulong)capacity, out var buffer).ThrowOnError();
        _buffer = new ComObject<ICoreWebView2SharedBuffer>(buffer);
        _capacity = capacity;
    }

    // (re)hand the current buffer to the page.
    // metadataJson is merged into the {"name":...} object the page receives (via 'sharedbufferreceived' → __aotrino), e.g.
    // Post("\"width\":800,\"height\":600").
    public virtual void Post(string? metadataJson = null)
    {
        if (_buffer == null || _buffer.IsDisposed)
            throw new InvalidOperationException("Call EnsureSize before Post.");

        var webView = _window.SharedWebView ?? throw new InvalidOperationException("The WebView2 controller is not ready.");
        var access = _access == SharedBufferAccess.ReadWrite
            ? COREWEBVIEW2_SHARED_BUFFER_ACCESS.COREWEBVIEW2_SHARED_BUFFER_ACCESS_READ_WRITE
            : COREWEBVIEW2_SHARED_BUFFER_ACCESS.COREWEBVIEW2_SHARED_BUFFER_ACCESS_READ_ONLY;

        var json = string.IsNullOrEmpty(metadataJson)
            ? $"{{\"name\":\"{_name}\"}}"
            : $"{{\"name\":\"{_name}\",{metadataJson}}}";
        webView.Object.PostSharedBufferToScript(_buffer.Object, access, PWSTR.From(json)).ThrowOnError();
    }

    protected virtual void Dispose(bool disposing)
    {
        Interlocked.Exchange(ref _buffer, null)?.Dispose();
        _capacity = 0;
        _size = 0;
    }

    ~SharedBuffer() { Dispose(disposing: false); }
    public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
}
