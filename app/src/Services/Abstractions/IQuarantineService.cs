using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Restoreable quarantine for eligible non-system cleanup files. Files are moved (never copied
/// and left behind) under %LOCALAPPDATA%\FarmanullahPCManager\quarantine, recorded in a
/// manifest with their original path, size and SHA-256 where practical. Restore never
/// overwrites an occupied destination.
/// </summary>
public interface IQuarantineService
{
    /// <summary>Absolute path of the quarantine folder inside the app's own data directory.</summary>
    string RootPath { get; }

    string ManifestPath { get; }

    IReadOnlyList<QuarantineEntry> GetEntries();

    /// <summary>
    /// Moves one file into the quarantine and records it. Throws <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> when the file is locked, refused or the
    /// quarantine cannot be written; the caller treats that as a skipped file.
    /// </summary>
    QuarantineEntry Quarantine(string filePath, string ruleId, string reason);

    /// <summary>Restores one entry, refusing when the original location is occupied.</summary>
    OperationResult Restore(string entryId);

    /// <summary>Deletes entries older than <paramref name="retention"/> or beyond the size limit.</summary>
    QuarantinePurgeResult Purge(TimeSpan retention, long maximumBytes);
}
