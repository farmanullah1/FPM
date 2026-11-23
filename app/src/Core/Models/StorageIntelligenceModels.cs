namespace PCManager.Core.Models;

/// <summary>
/// Hardware health and media properties of a physical storage drive.
/// </summary>
public sealed record DriveHealthInfo(
    string DeviceId,
    string Model,
    string MediaType,
    string BusType,
    string HealthStatus,
    bool? TrimEnabled,
    string OperationalStatus,
    long SizeBytes);

/// <summary>
/// Result of a read-only DISM Component Store analysis (/AnalyzeComponentStore).
/// </summary>
public sealed record DismAnalysisResult(
    bool Success,
    string RawOutput,
    string ComponentStoreSize,
    string ActualStoreSize,
    string SharedWithWindows,
    string BackupsAndDisabledFeatures,
    string CacheAndTempData,
    bool CleanupRecommended,
    string ErrorMessage)
{
    public static DismAnalysisResult Failed(string error) => new(
        Success: false,
        RawOutput: string.Empty,
        ComponentStoreSize: "Unknown",
        ActualStoreSize: "Unknown",
        SharedWithWindows: "Unknown",
        BackupsAndDisabledFeatures: "Unknown",
        CacheAndTempData: "Unknown",
        CleanupRecommended: false,
        ErrorMessage: error);
}

/// <summary>
/// Top-level category folder in a drive's storage map.
/// </summary>
public sealed record StorageFolderItem(
    string Name,
    string FullPath,
    long Bytes,
    string FormattedSize,
    string Category,
    double PercentOfDrive);
