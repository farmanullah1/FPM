using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Coordinates all file cleanup categories through a single scan, consolidated review,
/// explicit approval, and verified report.
/// </summary>
public interface IUnifiedCleanupCoordinator
{
    /// <summary>
    /// Performs a comprehensive read-only scan of all supported cleanup targets without deleting anything.
    /// </summary>
    Task<UnifiedScanReport> RunFullScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes cleanup only for user-approved item numbers from the specified scan report.
    /// </summary>
    Task<UnifiedExecutionResult> ExecuteApprovedAsync(
        string scanId,
        IReadOnlyList<int> approvedItemNumbers,
        CancellationToken cancellationToken = default);
}
