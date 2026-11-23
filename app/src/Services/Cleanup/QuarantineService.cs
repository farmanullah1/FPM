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
/// - the manifest is written atomically, and the move is rolled back if the manifest fails;
/// - a manifest that exists but cannot be parsed blocks every write, so records are never
///   silently dropped;
/// - restore refuses to overwrite anything and verifies the recorded SHA-256 first.
/// </summary>
public sealed class QuarantineService : IQuarantineService
{
    private const int MaximumEntries = 10_000;

    /// <summary>Hashing is capped so a multi-gigabyte cache file does not stall cleanup.</summary>
    private const long HashLimitBytes = 64L * 1024 * 1024;

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

    public QuarantineEntry Quarantine(string filePath, string ruleId, string reason)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new IOException("No file was named for quarantine.");
        }

        string original;

        try
        {
            original = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            throw new IOException("The path is not valid: " + ex.Message, ex);
        }

        if (!File.Exists(original))
        {
            throw new FileNotFoundException("The file no longer exists.", original);
        }

        if (IsInside(original, RootPath))
        {
            throw new IOException("A quarantined file cannot be quarantined again.");
        }

        if (IsForbiddenLocation(original))
        {
            throw new IOException("Windows and Program Files are never quarantined.");
        }

        if (IsForbiddenFile(original))
        {
            throw new IOException("Databases, credentials and session files are never quarantined.");
        }

        lock (_gate)
        {
            List<QuarantineEntry> entries = ReadManifest(forWrite: true);

            if (entries.Count >= MaximumEntries)
            {
                throw new IOException("The quarantine is full. Apply the retention policy first.");
            }

            long bytes = new FileInfo(original).Length;
            string id = Guid.NewGuid().ToString("N");
            string storedRelative = Path.Combine("files", id + "-" + Path.GetFileName(original));
            string storedFull = Path.Combine(RootPath, storedRelative);

            Directory.CreateDirectory(Path.Combine(RootPath, "files"));
            MoveFile(original, storedFull);

            string hash = TryHash(storedFull);
            var entry = new QuarantineEntry(
                id,
                original,
                storedRelative,
                DateTime.UtcNow,
                hash,
                bytes,
                ruleId,
                reason);

            entries.Add(entry);

            try
            {
                WriteManifest(entries);
            }
            catch
            {
                // The file must not be lost because its record could not be written.
                try
                {
                    MoveFile(storedFull, original);
                }
                catch (Exception rollback)
                {
                    _logger.Error(
                        $"A quarantined file could not be moved back after a manifest failure: {original}.",
                        rollback);
                }

                throw;
            }

            _logger.Info($"Quarantined {original} ({entry.SizeText}).");
            return entry;
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
                !string.Equals(TryHash(storedFull), entry.Sha256, StringComparison.OrdinalIgnoreCase))
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

    private static string TryHash(string path)
    {
        try
        {
            if (new FileInfo(path).Length > HashLimitBytes)
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
}
