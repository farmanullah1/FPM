using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Storage;

/// <summary>
/// Drive, folder and large-file analysis.
///
/// Folder sizes are measured recursively but with a depth cap (default 3 levels below
/// the selected folder) and a reparse-point skip, so scanning C:\Users does not walk
/// every node_modules tree or loop through junctions.
/// </summary>
public sealed class StorageAnalyzer : IStorageAnalyzer
{
    public IReadOnlyList<DriveSpace> GetDrives()
    {
        var drives = new List<DriveSpace>();

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    drives.Add(new DriveSpace
                    {
                        Name = drive.Name,
                        DriveType = drive.DriveType.ToString(),
                        IsReady = false
                    });
                    continue;
                }

                drives.Add(new DriveSpace
                {
                    Name = drive.Name,
                    Label = drive.VolumeLabel,
                    Format = drive.DriveFormat,
                    DriveType = drive.DriveType.ToString(),
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.TotalFreeSpace,
                    IsReady = true
                });
            }
            catch (Exception)
            {
                // Removable drive removed between enumeration and inspection.
            }
        }

        return drives;
    }

    public IReadOnlyList<FolderSize> ScanFolder(string path, int maxDepth, CancellationToken cancellationToken = default)
    {
        var results = new List<FolderSize>();

        if (!Directory.Exists(path))
        {
            return results;
        }

        long rootBytes = 0;
        int rootFiles = 0;

        foreach (FileInfo file in SafeFileSystem.EnumerateFiles(path, recurse: false))
        {
            rootBytes += file.Length;
            rootFiles++;
        }

        if (rootFiles > 0)
        {
            results.Add(new FolderSize
            {
                Path = path,
                DisplayName = "(files in this folder)",
                Bytes = rootBytes,
                FileCount = rootFiles
            });
        }

        foreach (DirectoryInfo directory in SafeFileSystem.EnumerateDirectories(path))
        {
            cancellationToken.ThrowIfCancellationRequested();

            (long bytes, int count) = Measure(directory.FullName, Math.Max(0, maxDepth - 1), cancellationToken);

            results.Add(new FolderSize
            {
                Path = directory.FullName,
                DisplayName = directory.Name,
                Bytes = bytes,
                FileCount = count
            });
        }

        long total = results.Sum(r => r.Bytes);
        foreach (FolderSize folder in results)
        {
            folder.SharePercent = total <= 0 ? 0 : (double)folder.Bytes / total * 100.0;
        }

        return results.OrderByDescending(r => r.Bytes).ToList();
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
                if (file.Length < minSizeBytes)
                {
                    continue;
                }

                files.Add(new LargeFile
                {
                    Path = file.FullName,
                    Bytes = file.Length,
                    Modified = file.LastWriteTime
                });
            }
            catch (Exception) { }
        }

        return files
            .OrderByDescending(f => f.Bytes)
            .Take(maxResults)
            .ToList();
    }

    private static (long Bytes, int Files) Measure(string directory, int remainingDepth, CancellationToken cancellationToken)
    {
        long bytes = 0;
        int files = 0;

        foreach (FileInfo file in SafeFileSystem.EnumerateFiles(directory, recurse: false))
        {
            try
            {
                bytes += file.Length;
                files++;
            }
            catch (Exception) { }
        }

        if (remainingDepth <= 0)
        {
            return (bytes, files);
        }

        foreach (DirectoryInfo child in SafeFileSystem.EnumerateDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            (long childBytes, int childFiles) = Measure(child.FullName, remainingDepth - 1, cancellationToken);
            bytes += childBytes;
            files += childFiles;
        }

        return (bytes, files);
    }
}
