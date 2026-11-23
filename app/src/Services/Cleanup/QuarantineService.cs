using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Cleanup;

/// <summary>
/// Moves eligible files to %LOCALAPPDATA%\FarmanullahPCManager\quarantine (created with the
/// profile's own ACLs, i.e. current user only) and records them in manifest.json.
///
/// Safety rules:
/// - files under Windows, Program Files, Program Files (x86) or inside the quarantine itself
///   are refused;
/// - databases, credentials, keys, cookies and session/login files are refused by extension
///   and name;
/// - a batch is recorded with one atomic manifest write, and every move in it is rolled back
///   if that write fails;
/// - a manifest that exists but cannot be parsed blocks every write, so records are never
///   silently dropped;
/// - restore refuses to overwrite anything and verifies the recorded SHA-256 first.
///
/// Hashing is deliberately bounded: files above 4 MB and anything beyond 128 MB of hashed
/// bytes per batch are recorded without a hash instead of delaying cleanup.
/// </summary>
public sealed class QuarantineService : IQuarantineService
{
    private const int MaximumEntries = 20_000;
    private const long HashLimitBytes = 4L * 1024 * 1024;
    private const long HashBudgetPerBatch = 128L * 1024 * 1024;

    private static readonly string[] ForbiddenExtensions =
    {
        ".db", ".db3", ".sqlite", ".sqlite3", ".mdf", ".ldf", ".pfx", ".p12",
        ".key", ".kdbx", ".pem", ".crt", ".ovpn", ".env"
    };

    private static readonly string[] ForbiddenNameFragments =
    {
        "cookies", "login data", "logins.json", "web data", "key4.db", "key3.db",
        "password", "credential", "session store", "indexeddb", "local storage"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAppLogger _logger;
    private readonly object _gate = new();

    public QuarantineService(IAppLogger logger, string? quarantineRoot = null)
    {
        _logger = logger;
        RootPath = quarantineRoot ?? Path.Combine(AppInfo.DataFolder, "quarantine");
        ManifestPath = Path.Combine(RootPath, "manifest.json");
    }

    public string RootPath { get; }

    public string ManifestPath { get; }

    public IReadOnlyList<QuarantineEntry> GetEntries()
    {
        lock (_gate)
        {
            return ReadManifest(forWrite: false)
                .OrderByDescending(entry => entry.QuarantinedUtc)
                .ToList();
        }
    }

    public IReadOnlyList<QuarantineFileResult> QuarantineFiles(
        IReadOnlyList<string> filePaths,
        string ruleId,
        string reason)
    {
        var results = new List<QuarantineFileResult>();

        if (filePaths.Count == 0)
        {
            return results;
        }

        lock (_gate)
        {
            List<QuarantineEntry> entries;

            try
            {
                entries = ReadManifest(forWrite: true);
            }
            catch (Exception ex)
            {
                return filePaths
                    .Select(path => new QuarantineFileResult(path, null, QuarantineRefusalKind.Failed, ex.Message))
                    .ToList();
            }

            var attempts = new List<QuarantineAttempt>();
            var moved = new List<QuarantineAttempt>();
            long hashBudget = HashBudgetPerBatch;

            foreach (string path in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                bool room = entries.Count < MaximumEntries;
                QuarantineAttempt attempt = room
                    ? TryQuarantineOne(path, ruleId, reason, entries, ref hashBudget)
                    : new QuarantineAttempt(
                        path, null, null, QuarantineRefusalKind.Refused,
                        "The quarantine is full. Apply the retention policy first.");

                attempts.Add(attempt);

                if (attempt.Entry is not null)
                {
                    moved.Add(attempt);
                }
            }

            if (moved.Count > 0)
            {
                try
                {
                    WriteManifest(entries);
                }
                catch (Exception writeFailure)
                {
                    // No record means no restore, so put every moved file back.
                    foreach (QuarantineAttempt attempt in moved)
                    {
                        try
                        {
                            MoveFile(attempt.StoredFull!, attempt.OriginalFull!);
                        }
                        catch (Exception rollback)
                        {
                            _logger.Error(
                                $"A quarantined file could not be moved back after a manifest failure: {attempt.Path}.",
                                rollback);
                        }
                    }

                    _logger.Error("Quarantine manifest write failed; the batch was rolled back.", writeFailure);

                    return attempts
                        .Select(attempt => attempt.Entry is null
                            ? new QuarantineFileResult(attempt.Path, null, attempt.Refusal, attempt.Error)
                            : new QuarantineFileResult(
                                attempt.Path,
                                null,
                                QuarantineRefusalKind.Failed,
                                "The quarantine manifest could not be written, so the file was put back: " +
                                writeFailure.Message))
                        .ToList();
                }
            }

            foreach (QuarantineAttempt attempt in attempts)
            {
                results.Add(new QuarantineFileResult(attempt.Path, attempt.Entry, attempt.Refusal, attempt.Error));

                if (attempt.Entry is not null)
                {
                    _logger.Info($"Quarantined {attempt.Path} ({attempt.Entry.SizeText}).");
                }
                else
                {
                    _logger.Info($"Not quarantined ({attempt.Refusal}): {attempt.Path} — {attempt.Error}");
                }
            }

            return results;
        }
    }

    public OperationResult Restore(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return OperationResult.Fail("No quarantine entry was selected.");
        }

        lock (_gate)
        {
            List<QuarantineEntry> entries;

            try
            {
                entries = ReadManifest(forWrite: true);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("The quarantine manifest could not be read: " + ex.Message);
            }

            QuarantineEntry? entry = entries.FirstOrDefault(candidate =>
                candidate.Id.Equals(entryId, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return OperationResult.Fail("That quarantine entry is no longer recorded.");
            }

            string storedFull;

            try
            {
                storedFull = Path.GetFullPath(Path.Combine(RootPath, entry.StoredPath));
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("The quarantine path is not valid: " + ex.Message);
            }

            if (!IsInside(storedFull, Path.Combine(RootPath, "files")))
            {
                return OperationResult.Fail(
                    "The manifest entry points outside the quarantine folder. Nothing was restored.");
            }

            if (!File.Exists(storedFull))
            {
                return OperationResult.Fail("The quarantined file is missing. Nothing was restored.");
            }

            string original;

            try
            {
                original = Path.GetFullPath(entry.OriginalPath);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("The recorded original path is not valid: " + ex.Message);
            }

            if (File.Exists(original))
            {
                return OperationResult.Fail(
                    $"The original location is occupied by a newer file: {original}. Nothing was overwritten.");
            }

            if (Directory.Exists(original))
            {
                return OperationResult.Fail(
                    $"The original location is now a folder: {original}. Nothing was overwritten.");
            }

            if (IsInside(original, RootPath) || IsForbiddenLocation(original))
            {
                return OperationResult.Fail(
                    "The recorded destination is not a location this app restores to. Nothing was restored.");
            }

            if (!string.IsNullOrEmpty(entry.Sha256) &&
                !string.Equals(TryHash(storedFull, long.MaxValue), entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail(
                    "The quarantined copy does not match its recorded SHA-256. Nothing was restored.");
            }

            try
            {
                string? parent = Path.GetDirectoryName(original);

                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                MoveFile(storedFull, original);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Could not restore {original}: {ex.Message}");
            }

            entries.RemoveAll(candidate => candidate.Id == entry.Id);
            TryWriteManifest(entries);
            RemoveEmptyFolders();

            _logger.Info($"Restored {original} from quarantine.");
            return OperationResult.Ok($"Restored {original} from quarantine.");
        }
    }

    public QuarantinePurgeResult Purge(TimeSpan retention, long maximumBytes)
    {
        if (retention < TimeSpan.Zero)
        {
            retention = TimeSpan.Zero;
        }

        if (maximumBytes < 0)
        {
            maximumBytes = 0;
        }

        lock (_gate)
        {
            List<QuarantineEntry> entries;

            try
            {
                entries = ReadManifest(forWrite: true);
            }
            catch (Exception ex)
            {
                _logger.Error("The quarantine retention pass was skipped.", ex);
                return new QuarantinePurgeResult(0, 0);
            }

            DateTime cutoffUtc = DateTime.UtcNow - retention;
            long remainingBytes = entries.Sum(entry => entry.Bytes);
            int removed = 0;
            long removedBytes = 0;

            foreach (QuarantineEntry entry in entries.OrderBy(candidate => candidate.QuarantinedUtc).ToList())
            {
                bool expired = entry.QuarantinedUtc < cutoffUtc;
                bool overSize = remainingBytes > maximumBytes;

                if (!expired && !overSize)
                {
                    continue;
                }

                if (!TryDeleteStoredFile(entry))
                {
                    // Still locked: keep the record so the next pass can try again.
                    continue;
                }

                entries.Remove(entry);
                remainingBytes -= entry.Bytes;
                removed++;
                removedBytes += entry.Bytes;
            }

            if (removed > 0)
            {
                TryWriteManifest(entries);
                RemoveEmptyFolders();
                _logger.Info($"Quarantine retention removed {removed} file(s), {removedBytes} bytes.");
            }

            return new QuarantinePurgeResult(removed, removedBytes);
        }
    }

    private QuarantineAttempt TryQuarantineOne(
        string filePath,
        string ruleId,
        string reason,
        List<QuarantineEntry> entries,
        ref long hashBudget)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new QuarantineAttempt(filePath, null, null, QuarantineRefusalKind.Failed, "No file was named.");
        }

        string original;

        try
        {
            original = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            return new QuarantineAttempt(filePath, null, null, QuarantineRefusalKind.Failed, ex.Message);
        }

        if (!File.Exists(original))
        {
            return new QuarantineAttempt(
                filePath, null, null, QuarantineRefusalKind.Locked, "The file no longer exists.");
        }

        if (IsInside(original, RootPath) || IsForbiddenLocation(original))
        {
            return new QuarantineAttempt(
                filePath, null, null, QuarantineRefusalKind.Refused,
                "Windows, Program Files and the quarantine itself are never quarantined.");
        }

        if (IsForbiddenFile(original))
        {
            return new QuarantineAttempt(
                filePath, null, null, QuarantineRefusalKind.Refused,
                "Databases, credentials and session files are never quarantined.");
        }

        long bytes;
        string storedFull;
        QuarantineEntry entry;

        try
        {
            bytes = new FileInfo(original).Length;
            string id = Guid.NewGuid().ToString("N");
            string storedRelative = Path.Combine("files", id + "-" + Path.GetFileName(original));
            storedFull = Path.Combine(RootPath, storedRelative);

            Directory.CreateDirectory(Path.Combine(RootPath, "files"));
            MoveFile(original, storedFull);

            string hash = TryHash(storedFull, hashBudget);

            if (hash.Length > 0)
            {
                hashBudget = Math.Max(0, hashBudget - bytes);
            }

            entry = new QuarantineEntry(
                id,
                original,
                storedRelative,
                DateTime.UtcNow,
                hash,
                bytes,
                ruleId,
                reason);

            entries.Add(entry);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new QuarantineAttempt(filePath, null, null, QuarantineRefusalKind.Locked, ex.Message);
        }
        catch (IOException ex)
        {
            return new QuarantineAttempt(filePath, null, null, QuarantineRefusalKind.Locked, ex.Message);
        }
        catch (Exception ex)
        {
            return new QuarantineAttempt(filePath, null, null, QuarantineRefusalKind.Failed, ex.Message);
        }

        return new QuarantineAttempt(filePath, entry, storedFull, QuarantineRefusalKind.None, null);
    }

    private List<QuarantineEntry> ReadManifest(bool forWrite)
    {
        try
        {
            if (!File.Exists(ManifestPath))
            {
                return new List<QuarantineEntry>();
            }

            string json = File.ReadAllText(ManifestPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new JsonException("The manifest is empty.");
            }

            List<QuarantineEntry>? entries =
                JsonSerializer.Deserialize<List<QuarantineEntry>>(json, JsonOptions);

            return (entries ?? new List<QuarantineEntry>())
                .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Id))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Error("Could not read quarantine manifest.", ex);

            if (forWrite)
            {
                throw new InvalidOperationException(
                    "The quarantine manifest could not be read, so nothing was changed. " +
                    "Inspect " + ManifestPath + ".", ex);
            }

            return new List<QuarantineEntry>();
        }
    }

    private void WriteManifest(List<QuarantineEntry> entries)
    {
        Directory.CreateDirectory(RootPath);

        string temporary = ManifestPath + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(entries, JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, ManifestPath, overwrite: true);
    }

    private void TryWriteManifest(List<QuarantineEntry> entries)
    {
        try
        {
            WriteManifest(entries);
        }
        catch (Exception ex)
        {
            _logger.Error("Could not update the quarantine manifest.", ex);
        }
    }

    private bool TryDeleteStoredFile(QuarantineEntry entry)
    {
        try
        {
            string full = Path.GetFullPath(Path.Combine(RootPath, entry.StoredPath));

            if (!IsInside(full, Path.Combine(RootPath, "files")))
            {
                // A tampered record is dropped without touching anything.
                return true;
            }

            if (!File.Exists(full))
            {
                return true;
            }

            File.SetAttributes(full, FileAttributes.Normal);
            File.Delete(full);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Could not delete quarantined file {entry.OriginalPath}.", ex);
            return false;
        }
    }

    private void RemoveEmptyFolders()
    {
        try
        {
            string filesRoot = Path.Combine(RootPath, "files");

            if (!Directory.Exists(filesRoot))
            {
                return;
            }

            foreach (string directory in Directory
                         .EnumerateDirectories(filesRoot, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                }
                catch
                {
                    // Left for the next pass.
                }
            }
        }
        catch
        {
            // Housekeeping is best-effort.
        }
    }

    private static string TryHash(string path, long budget)
    {
        try
        {
            long length = new FileInfo(path).Length;

            if (length > HashLimitBytes || length > budget)
            {
                return string.Empty;
            }

            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Moves within a volume with File.Move; across volumes a copy is written first and the
    /// source is deleted only after the copy exists. A failed delete removes the copy again,
    /// so the original is never lost and no duplicate is left behind.
    /// </summary>
    private static void MoveFile(string source, string destination)
    {
        string? sourceRoot = Path.GetPathRoot(source);
        string? destinationRoot = Path.GetPathRoot(destination);

        if (string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(source, destination);
            return;
        }

        DateTime lastWrite = File.GetLastWriteTimeUtc(source);
        File.Copy(source, destination, overwrite: false);

        try
        {
            File.Delete(source);
            File.SetLastWriteTimeUtc(destination, lastWrite);
        }
        catch
        {
            try
            {
                File.Delete(destination);
            }
            catch
            {
                // Both copies remain; the caller reports the failure.
            }

            throw;
        }
    }

    private static bool IsForbiddenLocation(string path)
    {
        string candidate;

        try
        {
            candidate = Path.GetFullPath(path);
        }
        catch
        {
            return true;
        }

        foreach (Environment.SpecialFolder folder in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86
                 })
        {
            string root = Environment.GetFolderPath(folder);

            if (!string.IsNullOrWhiteSpace(root) && IsInside(candidate, root))
            {
                return true;
            }
        }

        return string.IsNullOrEmpty(Path.GetFileName(candidate));
    }

    private static bool IsForbiddenFile(string path)
    {
        string extension = Path.GetExtension(path);
        string name = Path.GetFileName(path);

        return ForbiddenExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
               ForbiddenNameFragments.Any(fragment =>
                   name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInside(string path, string root)
    {
        string candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        if (candidate.Equals(parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefix = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record QuarantineAttempt(
        string Path,
        QuarantineEntry? Entry,
        string? StoredFull,
        QuarantineRefusalKind Refusal,
        string? Error)
    {
        public string? OriginalFull => Entry?.OriginalPath;
    }
}
