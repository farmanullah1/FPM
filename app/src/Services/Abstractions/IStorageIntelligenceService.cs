using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Read-only storage intelligence, drive health, component store analysis, and storage map.
/// </summary>
public interface IStorageIntelligenceService
{
    Task<IReadOnlyList<DriveHealthInfo>> GetDrivesHealthAsync(CancellationToken cancellationToken = default);
    Task<DismAnalysisResult> AnalyzeComponentStoreAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageFolderItem>> AnalyzeStorageMapAsync(string driveRoot, int topCount = 10, CancellationToken cancellationToken = default);
}
