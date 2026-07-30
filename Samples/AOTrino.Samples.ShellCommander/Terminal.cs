using DirectN.Extensions.Utilities;

namespace AOTrino.Samples.ShellCommander;

// a pseudo console (ConPTY) hosting a real console program. this is the shell integration the console pane shows off:
// Windows' own CreatePseudoConsole, a child process wired to it, and its two pipes read and written as byte streams.
public sealed partial class Terminal : IDisposable
{
    private nint _pseudoConsole;
    private nint _inputRead, _inputWrite, _outputRead, _outputWrite;
    private nint _attributeList;
    private nint _process, _thread;
    private FileStream? _writeStream;
    private FileStream? _readStream;
    private RegisteredWaitHandle? _exitWait;
    private AutoResetEvent? _exitEvent;
    private volatile bool _disposed;

    // raised on a background thread with each block of console output, and once when the child process exits.
    public event Action<byte[]>? Output;
    public event Action? Exited;

    public Terminal(short columns, short rows)
    {
        if (!CreatePipe(out _inputRead, out _inputWrite, 0, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        if (!CreatePipe(out _outputRead, out _outputWrite, 0, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        CreatePseudoConsole(new COORD { X = columns, Y = rows }, _inputRead, _outputWrite, 0, out _pseudoConsole).ThrowOnError();
    }

    // start the child console program wired to the pseudo console, in the given working directory.
    public unsafe void Start(string commandLine, string? workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        // an attribute list carrying the pseudo console, the documented way to hand a child its console.
        nint size = 0;
        InitializeProcThreadAttributeList(0, 1, 0, ref size);
        _attributeList = Marshal.AllocHGlobal(size);
        if (!InitializeProcThreadAttributeList(_attributeList, 1, 0, ref size))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        if (!UpdateProcThreadAttribute(_attributeList, 0, (nint)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, _pseudoConsole, nint.Size, 0, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        var startupInfo = new STARTUPINFOEX();
        startupInfo.StartupInfo.cb = (uint)sizeof(STARTUPINFOEX);
        startupInfo.lpAttributeList = _attributeList;
        if (!CreateProcessW(null, commandLine, 0, 0, false, EXTENDED_STARTUPINFO_PRESENT, 0, workingDirectory, in startupInfo, out var info))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        _process = info.hProcess;
        _thread = info.hThread;

        // the streams do not own the handles, this session closes them itself in Dispose.
        _writeStream = new FileStream(new SafeFileHandle(_inputWrite, ownsHandle: false), FileAccess.Write);
        _readStream = new FileStream(new SafeFileHandle(_outputRead, ownsHandle: false), FileAccess.Read);

        _exitEvent = new AutoResetEvent(false) { SafeWaitHandle = new SafeWaitHandle(_process, ownsHandle: false) };
        _exitWait = ThreadPool.RegisterWaitForSingleObject(_exitEvent, (_, _) =>
        {
            try
            {
                Exited?.Invoke();
            }
            catch
            {
                // continue
            }
        }, null, -1, executeOnlyOnce: true);

        new Thread(ReadLoop) { IsBackground = true, Name = typeof(Terminal).FullName }.Start();
    }

    // block reading console output and hand each block to the Output event, until the pipe closes (child exit or Dispose).
    private void ReadLoop()
    {
        try
        {
            var buffer = new byte[16384];
            int read;
            while (!_disposed && (read = _readStream!.Read(buffer, 0, buffer.Length)) > 0)
            {
                var block = new byte[read];
                Buffer.BlockCopy(buffer, 0, block, 0, read);
                Output?.Invoke(block);
            }
        }
        catch
        {
            // the stream was closed under us on Dispose, which is how this loop is meant to end
        }
    }

    public void Write(byte[] data)
    {
        var stream = _writeStream;
        if (stream == null || data.Length == 0)
            return;

        try
        {
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch
        {
            // the child is gone, continue
        }
    }

    public void Resize(short columns, short rows)
    {
        if (_pseudoConsole != 0)
        {
            ResizePseudoConsole(_pseudoConsole, new COORD { X = columns, Y = rows });
        }
    }

    ~Terminal() => Dispose(false);
    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        if (disposing)
        {
            // managed cleanup, not touched from the finalizer where these objects may already be gone.
            _exitWait?.Unregister(null);
            Interlocked.Exchange(ref _readStream, null).SafeDispose();
            Interlocked.Exchange(ref _writeStream, null).SafeDispose();
            Interlocked.Exchange(ref _exitEvent, null).SafeDispose();
        }

        // closing the pseudo console ends the session and closes its pipe, so the read loop's Read returns 0 and exits.
        var console = Interlocked.Exchange(ref _pseudoConsole, 0);
        if (console != 0)
        {
            ClosePseudoConsole(console);
        }

        var attributeList = Interlocked.Exchange(ref _attributeList, 0);
        if (attributeList != 0)
        {
            DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
        }

        CloseHandleIfSet(ref _process);
        CloseHandleIfSet(ref _thread);
        CloseHandleIfSet(ref _inputRead);
        CloseHandleIfSet(ref _inputWrite);
        CloseHandleIfSet(ref _outputRead);
        CloseHandleIfSet(ref _outputWrite);
    }

    private static void CloseHandleIfSet(ref nint handle)
    {
        var value = Interlocked.Exchange(ref handle, 0);
        if (value != 0)
        {
            DirectN.Functions.CloseHandle(value);
        }
    }

    private struct COORD
    {
        public short X;
        public short Y;
    }

    private struct STARTUPINFO
    {
        public uint cb;
        public nint lpReserved;
        public nint lpDesktop;
        public nint lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public nint lpReserved2;
        public nint hStdInput;
        public nint hStdOutput;
        public nint hStdError;
    }

    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public nint lpAttributeList;
    }

    private struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

#pragma warning disable IDE1006 // Naming Styles
    private const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
#pragma warning restore IDE1006 // Naming Styles

    [LibraryImport("kernel32")]
    private static partial HRESULT CreatePseudoConsole(COORD size, nint hInput, nint hOutput, uint dwFlags, out nint phPC);

    [LibraryImport("kernel32")]
    private static partial HRESULT ResizePseudoConsole(nint hPC, COORD size);

    [LibraryImport("kernel32")]
    private static partial void ClosePseudoConsole(nint hPC);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreatePipe(out nint hReadPipe, out nint hWritePipe, nint lpPipeAttributes, uint nSize);

    [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateProcessW(
        string? lpApplicationName,
        string lpCommandLine,
        nint lpProcessAttributes,
        nint lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        nint lpEnvironment,
        string? lpCurrentDirectory,
        in STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InitializeProcThreadAttributeList(nint lpAttributeList, uint dwAttributeCount, uint dwFlags, ref nint lpSize);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateProcThreadAttribute(nint lpAttributeList, uint dwFlags, nint attribute, nint lpValue, nint cbSize, nint lpPreviousValue, nint lpReturnSize);

    [LibraryImport("kernel32", SetLastError = true)]
    private static partial void DeleteProcThreadAttributeList(nint lpAttributeList);
}
