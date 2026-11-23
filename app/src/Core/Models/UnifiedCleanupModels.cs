using PCManager.Core.Models;

namespace PCManager.Core.Models;

public sealed record UnifiedCleanupItem(
    int ItemNumber,
    string AppOrCategory,
    string ExactPath,
    long SizeBytes,
    string Description,
    CleanupTier Tier,
    string RuleId,
    bool RequiresElevation = false,
    bool RequiresAppClosed = false,
    bool IsSelected = true);

public sealed record UnifiedScanReport(
    string ScanId,
    DateTime CreatedAtUtc,
    IReadOnlyList<UnifiedCleanupItem> Items,
    long TotalPotentialRecoveryBytes,
    long SafeTierBytes,
    long CautionTierBytes,
    long OptInTierBytes);

public sealed record UnifiedExecutionResult(
    string ScanId,
    int ItemsCleaned,
    int ItemsSkipped,
    long ActualBytesDeleted,
    long DriveFreeSpaceDeltaBytes,
    IReadOnlyList<string> SkippedReasons,
    string Summary);
