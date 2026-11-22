using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

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

/// <summary>A file that occupies a lot of disk space.</summary>
public sealed class LargeFile
{
    public string Path { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public DateTime Modified { get; set; }

    public string SizeText => ByteFormatter.Format(Bytes);
    public string ModifiedText => Modified.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>Result of GlobalMemoryStatusEx plus optimiser counters.</summary>
public sealed class MemorySnapshot
{
    public ulong TotalPhysicalBytes { get; set; }
    public ulong AvailableBytes { get; set; }
    public double LoadPercent { get; set; }
    public int WorkingSetsTrimmed { get; set; }
    public long ReclaimedBytes { get; set; }
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
