using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Phase 2 execution. Takes only an approved <see cref="CleanupPlan"/>, re-validates every
/// path through <see cref="ISafetyGuard"/> and writes each outcome to cleanup_log.txt.
/// </summary>
public interface ICleanupExecutor
{
    Task<CleanupReport> ExecuteAsync(
        CleanupPlan plan,
        bool dryRun,
        bool quarantine = false,
        CancellationToken cancellationToken = default);
}
