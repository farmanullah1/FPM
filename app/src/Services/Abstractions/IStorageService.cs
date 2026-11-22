using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Storage page: drive usage, large-file search and Storage Sense.</summary>
public interface IStorageService
{
    Task<IReadOnlyList<DriveSpace>> GetDrivesAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds files larger than <paramref name="minSizeBytes"/> below <paramref name="path"/>.</summary>
    Task<IReadOnlyList<LargeFile>> FindLargeFilesAsync(string path, long minSizeBytes, CancellationToken cancellationToken = default);

    bool GetStorageSenseEnabled();

    OperationResult SetStorageSenseEnabled(bool enabled);
}
