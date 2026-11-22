using System.Security.Cryptography;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Storage;

/// <summary>
/// Two-stage duplicate detection, which is the same strategy tools such as Duplicate
/// Cleaner use:
///   1. group candidates by exact size (cheap, no I/O beyond metadata),
///   2. hash the first and last 32 KB plus the length for groups with more than one file.
/// Hashing the edges is enough to tell real duplicates apart without reading gigabytes,
/// and identical edges + identical size is a safe duplicate signal for cache/backup use.
/// </summary>
public sealed class DuplicateFinderService : IDuplicateFinderService
{
    private const int EdgeBytes = 32 * 1024;

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
                string? hash = TryHashEdges(path, size, cancellationToken);
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
                groups.Add(new DuplicateGroup { FileSize = size, Paths = duplicates });
            }
        }

        return groups
            .OrderByDescending(g => g.WasteBytes)
            .Take(100)
            .ToList();
    }, cancellationToken);

    private static string? TryHashEdges(string path, long size, CancellationToken cancellationToken)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using var sha = SHA256.Create();

            byte[] lengthBytes = BitConverter.GetBytes(size);
            sha.TransformBlock(lengthBytes, 0, lengthBytes.Length, null, 0);

            int bufferSize = (int)Math.Min(EdgeBytes, size);
            byte[] buffer = new byte[bufferSize];

            int read = stream.Read(buffer, 0, bufferSize);
            sha.TransformBlock(buffer, 0, read, null, 0);

            if (size > EdgeBytes * 2L)
            {
                stream.Seek(-bufferSize, SeekOrigin.End);
                read = stream.Read(buffer, 0, bufferSize);
                sha.TransformBlock(buffer, 0, read, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            cancellationToken.ThrowIfCancellationRequested();

            return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
        }
        catch (Exception)
        {
            return null;
        }
    }
}
