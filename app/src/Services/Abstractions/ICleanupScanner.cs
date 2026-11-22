using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Phase 1: read-only measurement. This interface has no delete, move or shell operation;
/// the implementation only enumerates directories and reads file metadata.
/// </summary>
public interface ICleanupScanner
{
    Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default);
}
