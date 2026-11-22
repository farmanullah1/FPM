using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Storage Manager: drive usage, folder analysis, cleanup and Storage Sense.</summary>
public interface IStorageService
{
    Task<IReadOnlyList<DriveSpace>> GetDrivesAsync(CancellationToken cancellationToken = default);

    /// <summary>Sizes the immediate children of <paramref name="path"/>.</summary>
    Task<IReadOnlyList<FolderSize>> ScanFolderAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Finds files larger than <paramref name="minSizeBytes"/> below <paramref name="path"/>.</summary>
    Task<IReadOnlyList<LargeFile>> FindLargeFilesAsync(string path, long minSizeBytes, CancellationToken cancellationToken = default);

    /// <summary>Temp files + browser caches + recycle bin; returns bytes freed.</summary>
    Task<long> RunCleanupAsync(CancellationToken cancellationToken = default);

    long GetRecycleBinSize();

    Task<OperationResult> EmptyRecycleBinAsync(CancellationToken cancellationToken = default);

    bool GetStorageSenseEnabled();

    OperationResult SetStorageSenseEnabled(bool enabled);
}
