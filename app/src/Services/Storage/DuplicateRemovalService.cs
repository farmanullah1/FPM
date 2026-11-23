using System.Buffers;
using System.Security.Cryptography;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Storage;

/// <summary>
/// Recycles the extra copies of a duplicate group. Safety rules:
/// - the kept copy must belong to the group and is never touched;
/// - every candidate is re-read with full SHA-256 and must still match the kept copy, so a file
///   changed after the scan is skipped;
/// - whitelisted paths, reparse points, Windows, Program Files, ProgramData, this app's data and
///   installed-package folders are refused;
/// - files are moved to the Recycle Bin through the shell, never deleted permanently.
/// </summary>
public sealed class DuplicateRemovalService : IDuplicateRemovalService
{
    private static readonly string[] DeniedRoots = BuildDeniedRoots();

    private readonly IWhitelistService _whitelist;
    private readonly IFileRecycler _recycler;
    private readonly IAppLogger _logger;

    public DuplicateRemovalService(
        IWhitelistService whitelist,
        IFileRecycler recycler,
        IAppLogger logger)
    {
        _whitelist = whitelist;
        _recycler = recycler;
        _logger = logger;
    }

    public Task<DuplicateRemovalResult> MoveExtrasToRecycleBinAsync(
        DuplicateGroup group,
        string keepPath,
        CancellationToken cancellationToken = default)
        => Task.Run(() => MoveExtras(group, keepPath, cancellationToken), cancellationToken);

    private DuplicateRemovalResult MoveExtras(
        DuplicateGroup group,
        string keepPath,
        CancellationToken cancellationToken)
    {
        string keepFull;

        try
        {
            keepFull = Path.GetFullPath(keepPath);
        }
        catch (Exception ex)
        {
            return Refused("The copy to keep is not a valid path: " + ex.Message);
        }

        bool inGroup = group.Paths.Any(path =>
        {
            try
            {
                return Path.GetFullPath(path).Equals(keepFull, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        });

        if (!inGroup)
        {
            return Refused("The selected copy is not part of this duplicate group. Nothing was recycled.");
        }

        List<string> extras = group.Paths
            .Where(path => !path.Equals(keepPath, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (extras.Count == 0)
        {
            return Refused("There is nothing else to recycle in this group.");
        }

        if (!File.Exists(keepFull))
        {
            return Refused("The copy to keep no longer exists, so nothing was recycled.");
        }

        long keepSize;

        try
        {
            keepSize = new FileInfo(keepFull).Length;
        }
        catch (Exception ex)
        {
            return Refused("The copy to keep could not be read: " + ex.Message);
        }

        string? keepHash = TryHash(keepFull, cancellationToken);

        if (keepHash is null)
        {
            return Refused("The copy to keep could not be hashed, so nothing was recycled.");
        }

        var messages = new List<string>();
        int moved = 0;
        long movedBytes = 0;
        int skipped = 0;

        foreach (string extra in extras)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string full;

            try
            {
                full = Path.GetFullPath(extra);
            }
            catch (Exception ex)
            {
                skipped++;
                messages.Add($"{extra}: not a valid path ({ex.Message}).");
                continue;
            }

            string? refusal = ExplainRefusal(full);

            if (refusal is not null)
            {
                skipped++;
                messages.Add($"{full}: {refusal}");
                continue;
            }

            try
            {
                var info = new FileInfo(full);

                if (!info.Exists)
                {
                    skipped++;
                    messages.Add($"{full}: the file no longer exists.");
                    continue;
                }

                if (info.Length != keepSize)
                {
                    skipped++;
                    messages.Add($"{full}: the size changed after the scan, so it is not recycled.");
                    continue;
                }
            }
            catch (Exception ex)
            {
                skipped++;
                messages.Add($"{full}: could not be read ({ex.Message}).");
                continue;
            }

            string? hash = TryHash(full, cancellationToken);

            if (hash is null)
            {
                skipped++;
                messages.Add($"{full}: could not be hashed (locked or in use).");
                continue;
            }

            if (!hash.Equals(keepHash, StringComparison.Ordinal))
            {
                skipped++;
                messages.Add($"{full}: no longer identical to the kept copy, so it is not recycled.");
                continue;
            }

            OperationResult recycle = _recycler.Recycle(full);

            if (!recycle.Success)
            {
                skipped++;
                messages.Add($"{full}: {recycle.Message}");
                continue;
            }

            moved++;
            movedBytes += keepSize;
        }

        _logger.Info(
            $"Duplicate group {keepSize} B: {moved} file(s) recycled, {skipped} skipped, kept {keepFull}.");

        return new DuplicateRemovalResult(moved, movedBytes, skipped, messages);
    }

    private string? ExplainRefusal(string full)
    {
        if (_whitelist.IsWhitelisted(full))
        {
            return "it is on your whitelist.";
        }

        if (DeniedRoots.Any(root => IsSameOrUnder(full, root)))
        {
            return "it is inside a protected location (Windows, Program Files, ProgramData, app data or installed packages).";
        }

        if (HasReparsePoint(full))
        {
            return "it is or is inside a junction or symlink, so it is not followed.";
        }

        return null;
    }

    private static DuplicateRemovalResult Refused(string message)
        => new(0, 0, 0, new[] { message });

    private static string? TryHash(string path, CancellationToken cancellationToken)
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

    private static bool HasReparsePoint(string path)
    {
        string? current = path;

        for (int depth = 0; depth < 64 && !string.IsNullOrEmpty(current); depth++)
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch
            {
                // Cannot inspect it: fail closed.
                return true;
            }

            string? parent = Path.GetDirectoryName(current);

            if (parent is null || parent.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        return false;
    }

    private static string[] BuildDeniedRoots()
    {
        var roots = new List<string>();

        void Add(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                roots.Add(Path.TrimEndingDirectorySeparator(path));
            }
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        Add(Path.Combine(localAppData, "FarmanullahPCManager"));
        Add(Path.Combine(localAppData, "Packages"));
        Add(Path.Combine(localAppData, "Programs"));

        string systemDrive = Path.GetPathRoot(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
        Add(Path.Combine(systemDrive, "$Recycle.Bin"));

        return roots.ToArray();
    }

    private static bool IsSameOrUnder(string path, string root)
    {
        string candidate = Path.TrimEndingDirectorySeparator(path);
        string parent = Path.TrimEndingDirectorySeparator(root);

        if (candidate.Equals(parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefix = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
