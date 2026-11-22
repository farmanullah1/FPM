using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Drive, folder-size and large-file analysis.</summary>
public interface IStorageAnalyzer
{
    IReadOnlyList<DriveSpace> GetDrives();
    IReadOnlyList<FolderSize> ScanFolder(string path, int maxDepth, CancellationToken cancellationToken = default);
    IReadOnlyList<LargeFile> FindLargeFiles(string path, long minSizeBytes, int maxResults, CancellationToken cancellationToken = default);
}
