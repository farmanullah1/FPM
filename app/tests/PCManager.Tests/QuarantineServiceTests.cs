using System.Security.Cryptography;
using PCManager.Core.Models;
using PCManager.Services.Cleanup;
using Xunit;

namespace PCManager.Tests;

public sealed class QuarantineServiceTests
{
    [Fact]
    public void Quarantine_moves_the_file_and_records_it_in_the_manifest()
    {
        using var workspace = new Workspace();
        string source = workspace.CreateFile("cache.bin", "payload");
        var service = new QuarantineService(new TestLogger(), workspace.QuarantineRoot);

        var entry = service.Quarantine(source, "rule.dev.npm", "npm cache");

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(workspace.StoredPath(entry)));
        Assert.Equal("payload", File.ReadAllText(workspace.StoredPath(entry)));
        Assert.Equal("cache.bin", entry.Name);
        Assert.Equal(new FileInfo(workspace.StoredPath(entry)).Length, entry.Bytes);

        string expectedHash = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("payload")));
        Assert.Equal(expectedHash, entry.Sha256);

        var reloaded = new QuarantineService(new TestLogger(), workspace.QuarantineRoot);
        QuarantineEntry stored = Assert.Single(reloaded.GetEntries());
        Assert.Equal(entry.Id, stored.Id);
        Assert.Equal(source, stored.OriginalPath);
    }

    [Fact]
    public void Restore_puts_the_file_back_and_drops_the_entry()
    {
        using var workspace = new Workspace();
        string source = workspace.CreateFile("cache.bin", "payload");
        var service = new QuarantineService(new TestLogger(), workspace.QuarantineRoot);

        QuarantineEntry entry = service.Quarantine(source, "rule", "reason");
        var result = service.Restore(entry.Id);

        Assert.True(result.Success);
        Assert.Equal("payload", File.ReadAllText(source));
        Assert.Empty(service.GetEntries());
        Assert.False(File.Exists(workspace.StoredPath(entry)));
    }

    [Fact]
    public void Restore_refuses_when_the_original_path_is_occupied()
    {
        using var workspace = new Workspace();
        string source = workspace.CreateFile("cache.bin", "old");
        var service = new QuarantineService(new TestLogger(), workspace.QuarantineRoot);

        QuarantineEntry entry = service.Quarantine(source, "rule", "reason");
        File.WriteAllText(source, "new");

        var result = service.Restore(entry.Id);

        Assert.False(result.Success);
        Assert.Equal("new", File.ReadAllText(source));
        Assert.Equal("old", File.ReadAllText(workspace.StoredPath(entry)));
        Assert.Single(service.GetEntries());
    }

    [Fact]
    public void Restore_refuses_a_manifest_entry_that_points_outside_the_quarantine()
    {
        using var workspace = new Workspace();
        string outside = workspace.CreateFile("outside.bin", "secret");
        Directory.CreateDirectory(workspace.QuarantineRoot);

        File.WriteAllText(workspace.ManifestPath, """
            [
              {
                "Id": "tampered",
                "OriginalPath": "C:\\temp\\missing.bin",
                "StoredPath": "files\\..\\..\\outside.bin",
                "QuarantinedUtc": "2026-01-01T00:00:00Z",
                "Sha256": "",
                "Bytes": 6,
                "RuleId": "rule",
                "Reason": "reason"
              }
            ]
            """);

        var service = new QuarantineService(new TestLogger(), workspace.QuarantineRoot);
        var result = service.Restore("tampered");

        Assert.False(result.Success);
        Assert.True(File.Exists(outside));
        Assert.Single(service.GetEntries());
    }

    [Fact]
    public void Quarantine_refuses_databases_and_credentials()
    {
        using var workspace = new Workspace();
        string database = workspace.CreateFile("state.db", "sqlite");
        string credentials = workspace.CreateFile("Login Data", "passwords");
        var service = new QuarantineService(new TestLogger(), workspace.QuarantineRoot);

        Assert.Throws<IOException>(() => service.Quarantine(database, "rule", "reason"));
        Assert.Throws<IOException>(() => service.Quarantine(credentials, "rule", "reason"));
        Assert.True(File.Exists(database));
        Assert.True(File.Exists(credentials));
        Assert.Empty(service.GetEntries());
    }

    [Fact]
    public void Purge_removes_expired_entries_and_their_files()
    {
        using var workspace = new Workspace();
        string source = workspace.CreateFile("cache.bin", "payload");
        var service = new QuarantineService(new TestLogger(), workspace.QuarantineRoot);

        QuarantineEntry entry = service.Quarantine(source, "rule", "reason");
        QuarantinePurgeResult purge = service.Purge(TimeSpan.Zero, long.MaxValue);

        Assert.Equal(1, purge.EntriesRemoved);
        Assert.Equal(entry.Bytes, purge.BytesRemoved);
        Assert.Empty(service.GetEntries());
        Assert.False(File.Exists(workspace.StoredPath(entry)));
    }

    [Fact]
    public void Purge_evicts_the_oldest_entries_beyond_the_size_limit()
    {
        using var workspace = new Workspace();
        string first = workspace.CreateFile("first.bin", new string('a', 4096));
        string second = workspace.CreateFile("second.bin", new string('b', 4096));
        var service = new QuarantineService(new TestLogger(), workspace.QuarantineRoot);

        QuarantineEntry older = service.Quarantine(first, "rule", "reason");
        Thread.Sleep(5);
        QuarantineEntry newer = service.Quarantine(second, "rule", "reason");

        QuarantinePurgeResult purge = service.Purge(TimeSpan.FromDays(365), 4096);

        Assert.Equal(1, purge.EntriesRemoved);
        Assert.DoesNotContain(service.GetEntries(), entry => entry.Id == older.Id);
        Assert.Contains(service.GetEntries(), entry => entry.Id == newer.Id);
        Assert.False(File.Exists(workspace.StoredPath(older)));
        Assert.True(File.Exists(workspace.StoredPath(newer)));
    }

    private sealed class Workspace : IDisposable
    {
        private readonly string _root;

        public Workspace()
        {
            _root = Path.Combine(Path.GetTempPath(), "PCManagerQuarantineTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            QuarantineRoot = Path.Combine(_root, "quarantine");
        }

        public string QuarantineRoot { get; }

        public string ManifestPath => Path.Combine(QuarantineRoot, "manifest.json");

        public string CreateFile(string name, string content)
        {
            string path = Path.Combine(_root, "files", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public string StoredPath(QuarantineEntry entry)
            => Path.Combine(QuarantineRoot, entry.StoredPath);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // Best effort for a test folder.
            }
        }
    }
}
