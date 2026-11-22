using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

/// <summary>A logical drive and its space usage.</summary>
public sealed class DriveSpace
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string DriveType { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public bool IsReady { get; set; }

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedPercent => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes * 100.0;

    // Display helpers so the XAML stays converter-free.
    public string FreeText => ByteFormatter.Format(FreeBytes);
    public string TotalText => ByteFormatter.Format(TotalBytes);
    public string UsedPercentText => $"{UsedPercent:0}%";
    public string SizeSummary => $"{FreeText} free of {TotalText}";
}

/// <summary>Measured size of one folder during a storage scan.</summary>
public sealed class FolderSize
{
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public int FileCount { get; set; }

    /// <summary>Share of the parent folder, filled in by the service.</summary>
    public double SharePercent { get; set; }

    public string SizeText => ByteFormatter.Format(Bytes);
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
    public ulong CommitUsedBytes { get; set; }
    public int WorkingSetsTrimmed { get; set; }
    public long ReclaimedBytes { get; set; }
}

/// <summary>Windows Defender state reported on the Protection card.</summary>
public sealed class DefenderStatus
{
    public bool Available { get; set; }
    public bool RealTimeEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public string SignatureVersion { get; set; } = string.Empty;
    public string LastScan { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>Windows Update service state.</summary>
public sealed class UpdateStatus
{
    public bool UpdateServiceRunning { get; set; }
    public bool PendingReboot { get; set; }
    public List<string> LastInstalled { get; set; } = new();
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>One network adapter with its addressing information.</summary>
public sealed class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IPv4 { get; set; } = string.Empty;
    public string IPv6 { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string DnsServers { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public long SpeedBitsPerSecond { get; set; }
    public bool IsUp { get; set; }
}

/// <summary>Result of a DNS lookup.</summary>
public sealed class DnsLookupResult
{
    public string HostName { get; set; } = string.Empty;
    public List<string> Addresses { get; set; } = new();
    public long ElapsedMs { get; set; }
    public string Error { get; set; } = string.Empty;
}
