using PCManager.Core.Models;
using PCManager.Services.Cleanup;
using Xunit;

namespace PCManager.Tests;

public sealed class CleanupHistoryTests
{
    [Fact]
    public void Completed_runs_are_kept_newest_first()
    {
        string folder = TemporaryFolder();
        string path = Path.Combine(folder, "cleanup_history.json");

        try
        {
            var service = new CleanupHistoryService(new TestLogger(), path);
            service.Add(Report("plan-one", DateTime.UtcNow.AddMinutes(-5), 1024, dryRun: false));
            service.Add(Report("plan-two", DateTime.UtcNow, 2048, dryRun: true));

            IReadOnlyList<CleanupHistoryEntry> entries = service.GetRecent();

            Assert.Equal(2, entries.Count);
            Assert.Equal("plan-two", entries[0].PlanId);
            Assert.True(entries[0].DryRun);
            Assert.Equal("plan-one", entries[1].PlanId);
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Computed_columns_are_not_persisted()
    {
        string folder = TemporaryFolder();
        string path = Path.Combine(folder, "cleanup_history.json");

        try
        {
            var service = new CleanupHistoryService(new TestLogger(), path);
            service.Add(Report("plan", DateTime.UtcNow, 1024, dryRun: false));

            string json = File.ReadAllText(path);

            Assert.DoesNotContain("DisplayText", json, StringComparison.Ordinal);
            Assert.Contains("plan", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Corrupt_history_returns_an_empty_list_and_logs()
    {
        string folder = TemporaryFolder();
        string path = Path.Combine(folder, "cleanup_history.json");

        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "{not json");

            var logger = new TestLogger();
            var service = new CleanupHistoryService(logger, path);

            Assert.Empty(service.GetRecent());
            Assert.NotEmpty(logger.Errors);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void History_is_capped_at_fifty_entries()
    {
        string folder = TemporaryFolder();
        string path = Path.Combine(folder, "cleanup_history.json");

        try
        {
            var service = new CleanupHistoryService(new TestLogger(), path);

            for (int index = 0; index < 55; index++)
            {
                service.Add(Report($"plan-{index}", DateTime.UtcNow.AddSeconds(index), index, dryRun: false));
            }

            Assert.Equal(50, service.GetRecent(100).Count);
            Assert.Equal("plan-54", service.GetRecent(100)[0].PlanId);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static CleanupReport Report(string planId, DateTime completed, long bytes, bool dryRun)
        => new()
        {
            PlanId = planId,
            CompletedUtc = completed,
            DryRun = dryRun,
            EstimatedBytes = bytes,
            BytesDeleted = dryRun ? 0 : bytes,
            FilesDeleted = 1,
            FilesSkipped = 0,
            Duration = TimeSpan.FromSeconds(1),
            Summary = "Test report"
        };

    private static string TemporaryFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "PCManagerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
