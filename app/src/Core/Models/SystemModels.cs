using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

/// <summary>Live memory figures: physical total, physical available and Windows' load percent.</summary>
public sealed class MemorySnapshot
{
    public ulong TotalPhysicalBytes { get; set; }
    public ulong AvailableBytes { get; set; }
    public double LoadPercent { get; set; }
}

/// <summary>
/// Outcome of one working-set trim.
///
/// Trimming asks Windows to page out least-recently-used pages; it does not free memory, so
/// the only honest measure is the change in available physical memory between the before and
/// after snapshots. That delta can be negative when the machine is under load, and the UI has
/// to be able to say so.
/// </summary>
public sealed class MemoryTrimResult
{
    public MemorySnapshot Before { get; init; } = new();
    public MemorySnapshot After { get; init; } = new();

    /// <summary>Processes whose working set Windows accepted the trim request for.</summary>
    public int WorkingSetsTrimmed { get; init; }

    /// <summary>Signed change in available physical memory across the trim.</summary>
    public long AvailableDeltaBytes => (long)After.AvailableBytes - (long)Before.AvailableBytes;

    public string AvailableDeltaText => ByteFormatter.FormatDelta(AvailableDeltaBytes);
}

/// <summary>A file that occupies a lot of disk space.</summary>
public sealed class LargeFile
{
    public string Path { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public DateTime Modified { get; set; }

    public string SizeText => ByteFormatter.Format(Bytes);
    public string ModifiedText => Modified.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>A fixed drive and its space usage.</summary>
public sealed class DriveSpace
{
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedPercent => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes * 100.0;

    public string SizeSummary => $"{ByteFormatter.Format(FreeBytes)} free of {ByteFormatter.Format(TotalBytes)}";
}

/// <summary>Windows Defender state reported on the Protection page.</summary>
public sealed class DefenderStatus
{
    public bool RealTimeEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public string SignatureVersion { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>Windows Update service state.</summary>
public sealed class UpdateStatus
{
    public bool UpdateServiceRunning { get; set; }
    public bool PendingReboot { get; set; }
    public string StatusText { get; set; } = string.Empty;
}
