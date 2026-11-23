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
    /// Moves a batch of files into the quarantine and records them with one manifest write.
    /// Each file gets its own result, so one locked or refused file never hides the rest.
    /// </summary>
    IReadOnlyList<QuarantineFileResult> QuarantineFiles(
        IReadOnlyList<string> filePaths,
        string ruleId,
        string reason);

    /// <summary>Restores one entry, refusing when the original location is occupied.</summary>
    OperationResult Restore(string entryId);

    /// <summary>Deletes entries older than <paramref name="retention"/> or beyond the size limit.</summary>
    QuarantinePurgeResult Purge(TimeSpan retention, long maximumBytes);
}
