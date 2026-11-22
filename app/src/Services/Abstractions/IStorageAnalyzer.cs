using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Raw drive and file-system reads used by <see cref="IStorageService"/>.</summary>
public interface IStorageAnalyzer
{
    IReadOnlyList<DriveSpace> GetDrives();

    IReadOnlyList<LargeFile> FindLargeFiles(string path, long minSizeBytes, int maxResults, CancellationToken cancellationToken = default);
}
