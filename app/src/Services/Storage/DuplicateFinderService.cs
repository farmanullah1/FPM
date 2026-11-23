using System.Buffers;
using System.Security.Cryptography;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Storage;

/// <summary>
/// Two-stage duplicate detection:
///   1. group candidates by exact size (cheap, no I/O beyond metadata),
///   2. hash the full contents with SHA-256 for groups with more than one file.
/// Only files that share a size are read, and the hash is cancellable, so a large scan can be
/// stopped without leaving the UI frozen. The kept copy is the oldest by write time.
/// </summary>
public sealed class DuplicateFinderService : IDuplicateFinderService
{
    public Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        string root,
        long minimumFileSizeBytes = 1024 * 1024,
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<DuplicateGroup>>(() =>
    {
        var groups = new List<DuplicateGroup>();

        if (!Directory.Exists(root))
        {
            return groups;
        }

        var bySize = new Dictionary<long, List<string>>();

        foreach (FileInfo file in SafeFileSystem.EnumerateFiles(root, recurse: true, maxFiles: 80_000))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (file.Length < minimumFileSizeBytes)
                {
                    continue;
                }

                if (!bySize.TryGetValue(file.Length, out List<string>? paths))
                {
                    paths = new List<string>();
                    bySize[file.Length] = paths;
                }

                paths.Add(file.FullName);
            }
            catch (Exception)
            {
                // File disappeared or is inaccessible.
            }
        }

        foreach ((long size, List<string> candidates) in bySize.Where(pair => pair.Value.Count > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var byHash = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (string path in candidates)
            {
                string? hash = TryHashFull(path, cancellationToken);
                if (hash is null)
                {
                    continue;
                }

                if (!byHash.TryGetValue(hash, out List<string>? paths))
                {
                    paths = new List<string>();
                    byHash[hash] = paths;
                }

                paths.Add(path);
            }

            foreach (List<string> duplicates in byHash.Values.Where(list => list.Count > 1))
            {
                List<string> ordered = duplicates
                    .OrderBy(TryLastWriteUtc)
                    .ThenBy(path => path.Length)
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                groups.Add(new DuplicateGroup
                {
                    FileSize = size,
                    Paths = ordered,
                    KeepPath = ordered[0]
                });
            }
        }

        return groups
            .OrderByDescending(g => g.WasteBytes)
            .Take(100)
            .ToList();
    }, cancellationToken);

    private static string? TryHashFull(string path, CancellationToken cancellationToken)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);

            try
            {
                int read;

                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hash.AppendData(buffer, 0, read);
                }

                return Convert.ToHexString(hash.GetHashAndReset());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime TryLastWriteUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MaxValue;
        }
    }
}
