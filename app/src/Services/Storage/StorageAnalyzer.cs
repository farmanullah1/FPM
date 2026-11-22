using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Storage;

/// <summary>Drive and large-file analysis.</summary>
public sealed class StorageAnalyzer : IStorageAnalyzer
{
    public IReadOnlyList<DriveSpace> GetDrives()
    {
        var drives = new List<DriveSpace>();

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                {
                    continue;
                }

                drives.Add(new DriveSpace
                {
                    Name = drive.Name,
                    Format = drive.DriveFormat,
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.TotalFreeSpace
                });
            }
            catch (Exception)
            {
                // A drive can disappear between enumeration and inspection.
            }
        }

        return drives;
    }

    public IReadOnlyList<LargeFile> FindLargeFiles(string path, long minSizeBytes, int maxResults, CancellationToken cancellationToken = default)
    {
        var files = new List<LargeFile>();

        if (!Directory.Exists(path))
        {
            return files;
        }

        foreach (FileInfo file in SafeFileSystem.EnumerateFiles(path, recurse: true, maxFiles: 400_000))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (file.Length >= minSizeBytes)
                {
                    files.Add(new LargeFile
                    {
                        Path = file.FullName,
                        Bytes = file.Length,
                        Modified = file.LastWriteTime
                    });
                }
            }
            catch (Exception)
            {
                // File vanished mid-scan.
            }
        }

        return files
            .OrderByDescending(f => f.Bytes)
            .Take(maxResults)
            .ToList();
    }
}
