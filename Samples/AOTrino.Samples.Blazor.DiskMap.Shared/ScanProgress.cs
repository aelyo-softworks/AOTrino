namespace AOTrino.Samples.Blazor.DiskMap.Shared;

// what a running scan reports. polled by the page, because a scan produces far more progress than a UI can draw.
public sealed class ScanProgress
{
    public bool IsRunning { get; set; }
    public bool IsComplete { get; set; }
    public string? CurrentPath { get; set; }
    public long BytesScanned { get; set; }
    public int FilesScanned { get; set; }
    public int DirectoriesScanned { get; set; }
    public int ErrorCount { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public string? Error { get; set; }

    // which of the two scanners ran, and why, so the page can say so rather than leave a difference of minutes unexplained.
    public bool UsedMasterFileTable { get; set; }
    public string? Method { get; set; }

    // best effort only. the total is what the drive reports as used, and a scan of one folder,
    // of a drive with files it may not read, or with compressed or sparse files, will not land on exactly 100.
    public long EstimatedTotalBytes { get; set; }

    // the master file table scan knows exactly how much work there is, it is a fixed number of records,
    // and every record costs about the same to read. that is a real denominator, unlike anything the walk has.
    public long RecordsRead { get; set; }
    public long RecordsTotal { get; set; }

    private bool CountsRecords => RecordsTotal > 0;

    public double PercentComplete
    {
        get
        {
            if (IsComplete)
                return 100;

            if (CountsRecords)
                return Math.Min(99.9, RecordsRead * 100.0 / RecordsTotal);

            // the walk has no honest denominator, so this is bytes against what the drive says it is using.
            // it moves in jumps, because the cost of a walk is per directory entry and this measures bytes,
            // and a folder of half a million tiny files takes minutes while barely moving it.
            return EstimatedTotalBytes <= 0 ? 0 : Math.Min(99.9, BytesScanned * 100.0 / EstimatedTotalBytes);
        }
    }

    // only offered where the units being counted cost the same as each other, which is true of records and not of bytes.
    // extrapolating the walk from bytes produced estimates that were wrong by orders of magnitude,
    // so it now reports elapsed time instead of inventing a figure it cannot stand behind.
    public bool HasEstimate => CountsRecords;

    public long EstimatedMillisecondsRemaining
    {
        get
        {
            var percent = PercentComplete;
            if (!IsRunning || !HasEstimate || percent <= 1 || ElapsedMilliseconds <= 0)
                return 0;

            return (long)(ElapsedMilliseconds / percent * (100 - percent));
        }
    }
}
