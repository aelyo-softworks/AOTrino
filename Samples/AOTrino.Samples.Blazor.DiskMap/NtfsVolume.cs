namespace AOTrino.Samples.Blazor.DiskMap;

using System.Runtime.CompilerServices;
using System.Security.Principal;

// the small amount of NTFS that this sample needs, declared here rather than in DirectN,
// because it is specific to reading a Master File Table and nothing else in Windows wants it.
// DeviceIoControl itself lives in DirectN, being the gateway to every FSCTL there is.
internal static class NtfsVolume
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NtfsVolumeData
    {
        public long VolumeSerialNumber;
        public long NumberSectors;
        public long TotalClusters;
        public long FreeClusters;
        public long TotalReserved;
        public uint BytesPerSector;
        public uint BytesPerCluster;
        public uint BytesPerFileRecordSegment;
        public uint ClustersPerFileRecordSegment;
        public long MftValidDataLength;
        public long MftStartLcn;
        public long Mft2StartLcn;
        public long MftZoneStart;
        public long MftZoneEnd;
    }

    // true when the process can open a volume for raw reading, which is what reading the MFT comes down to.
    // it is administrator or nothing, there is no lesser right that grants it.
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    // restarts this app as administrator, which is the only way to reach the master file table.
    // "runas" is what raises the UAC prompt, and it needs UseShellExecute, since elevation is a shell service
    // rather than something a process can grant itself.
    // returns false when the prompt is refused, and refusing is a normal answer, not an error.
    public static bool TryRestartElevated()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                return false;

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory,
            });

            return true;
        }
        catch
        {
            // ERROR_CANCELLED when the prompt is dismissed, and nothing else here is worth reporting either.
            return false;
        }
    }

    public static bool IsNtfs(string root)
    {
        try
        {
            return new DriveInfo(root).DriveFormat.EqualsIgnoreCase("NTFS");
        }
        catch
        {
            return false;
        }
    }

    // the MFT can be read for this path when it is a whole NTFS drive and this process is elevated.
    public static bool CanRead(string path, out string reason)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root) || !full.TrimEnd(Path.DirectorySeparatorChar).EqualsIgnoreCase(root.TrimEnd(Path.DirectorySeparatorChar)))
        {
            reason = "the master file table describes a whole volume, so it is only used when a whole drive is scanned.";
            return false;
        }

        if (!IsNtfs(root))
        {
            reason = "this drive is not NTFS, and only NTFS has a master file table.";
            return false;
        }

        if (!IsElevated())
        {
            reason = "reading the master file table means opening the volume directly, which Windows only allows an administrator to do.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    // opens \\.\C: for raw reading. the trailing backslash of a drive root has to go,
    // "\\.\C:\" is a path on the volume and "\\.\C:" is the volume itself.
    public static FileStream? OpenVolume(string root, out NtfsVolumeData data)
    {
        data = default;
        var device = @"\\.\" + root.TrimEnd(Path.DirectorySeparatorChar);

        var handle = DirectNFunctions.CreateFileW(
            PWSTR.From(device),
            (uint)GENERIC_ACCESS_RIGHTS.GENERIC_READ,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            0,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            0,
            0);
        if (handle == 0 || handle == HANDLE.InvalidValue)
            return null;

        var safe = new SafeFileHandle(handle, ownsHandle: true);
        if (!TryGetVolumeData(handle, out data))
        {
            safe.Dispose();
            return null;
        }

        // a FileStream over the volume handle, so the reads themselves stay managed.
        // no buffering, because every read here is already a large aligned block and a buffer would only copy it twice.
        return new FileStream(safe, FileAccess.Read, bufferSize: 1, isAsync: false);
    }

    private static unsafe bool TryGetVolumeData(HANDLE handle, out NtfsVolumeData data)
    {
        // generously sized, the real NTFS_VOLUME_DATA_BUFFER is longer than the part declared above,
        // and DeviceIoControl fails rather than truncates when the buffer is too small.
        var buffer = stackalloc byte[512];
        uint returned = 0;

        // see https://www.magnumdb.com/search?q=FSCTL_GET_NTFS_VOLUME_DATA
        // winioctl.h, CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 25, METHOD_BUFFERED, FILE_ANY_ACCESS).
        const uint FSCTL_GET_NTFS_VOLUME_DATA = 0x00090064;
        var ok = DirectNFunctions.DeviceIoControl(
                handle,
                FSCTL_GET_NTFS_VOLUME_DATA,
                0,
                0,
                (nint)buffer,
                512,
                (nint)(&returned),
                0);

        if (!ok || returned < (uint)sizeof(NtfsVolumeData))
        {
            data = default;
            return false;
        }

        data = Unsafe.ReadUnaligned<NtfsVolumeData>(buffer);
        return true;
    }
}
