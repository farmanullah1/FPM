using System.Text.Json;
using PCManager.Core.Debloat;
using PCManager.Services.Debloat;
using Xunit;

namespace PCManager.Tests;

public sealed class DebloatPresetTests
{
    private static readonly DebloatCatalog Catalog = DebloatCatalogLoader.LoadEmbedded();

    [Fact]
    public void Recommended_preset_selects_every_low_risk_tweak()
    {
        IReadOnlyList<string> ids = DebloatPresetCatalog.Select(Catalog, "recommended");
        var selected = Catalog.Tweaks.Where(tweak => ids.Contains(tweak.Id)).ToList();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, selected.Count);
        Assert.All(selected, tweak => Assert.Equal("Low", tweak.Risk));
        Assert.Equal(Catalog.Tweaks.Count(tweak => tweak.Risk == "Low"), ids.Count);
    }

    [Fact]
    public void Privacy_and_performance_presets_exclude_high_risk_and_stay_in_their_families()
    {
        List<DebloatTweak> privacy = Catalog.Tweaks
            .Where(tweak => DebloatPresetCatalog.Select(Catalog, "privacy").Contains(tweak.Id))
            .ToList();
        List<DebloatTweak> performance = Catalog.Tweaks
            .Where(tweak => DebloatPresetCatalog.Select(Catalog, "performance").Contains(tweak.Id))
            .ToList();

        Assert.NotEmpty(privacy);
        Assert.NotEmpty(performance);
        Assert.All(privacy, tweak =>
        {
            Assert.NotEqual("High", tweak.Risk);
            Assert.True(DebloatPresetCatalog.IsPrivacy(tweak));
        });
        Assert.All(performance, tweak =>
        {
            Assert.NotEqual("High", tweak.Risk);
            Assert.True(DebloatPresetCatalog.IsPerformance(tweak));
        });
    }

    [Fact]
    public void Minimal_is_only_low_risk_privacy_and_custom_selects_nothing()
    {
        List<DebloatTweak> minimal = Catalog.Tweaks
            .Where(tweak => DebloatPresetCatalog.Select(Catalog, "minimal").Contains(tweak.Id))
            .ToList();

        Assert.NotEmpty(minimal);
        Assert.All(minimal, tweak =>
        {
            Assert.Equal("Low", tweak.Risk);
            Assert.Contains(tweak.Category, new[] { "Appearance", "Start Menu & Search" });
        });
        Assert.Empty(DebloatPresetCatalog.Select(Catalog, DebloatPresetCatalog.CustomId));
    }
}

public sealed class DebloatPageCatalogTests
{
    private static readonly DebloatCatalog Catalog = DebloatCatalogLoader.LoadEmbedded();

    [Fact]
    public void Category_and_search_filter_are_combined_and_ordered()
    {
        IReadOnlyList<DebloatTweak> filtered = DebloatPageCatalog.Filter(Catalog, "Taskbar", "taskbar");

        Assert.NotEmpty(filtered);
        Assert.All(filtered, tweak => Assert.Equal("Taskbar", tweak.Category));
        Assert.Equal(filtered.OrderBy(tweak => tweak.Category).ThenBy(tweak => tweak.Title),
            filtered);
    }

    [Fact]
    public void Search_matches_title_and_returns_empty_for_no_match()
    {
        DebloatTweak first = Catalog.Tweaks.First(tweak => tweak.Title.Length > 4);
        IReadOnlyList<DebloatTweak> matches =
            DebloatPageCatalog.Filter(Catalog, null, first.Title[..4]);

        Assert.Contains(matches, tweak => tweak.Id == first.Id);
        Assert.Empty(DebloatPageCatalog.Filter(Catalog, null, "zzzz-no-such-tweak"));
    }

    [Fact]
    public void Plan_summary_reports_ready_skipped_and_risk_counts()
    {
        DebloatTweak low = Catalog.Tweaks.First(tweak =>
            tweak.Risk == "Low" &&
            DebloatPlanner.Plan(Catalog, new[] { tweak.Id }, 22631, true).Runnable.Any());
        DebloatTweak gated = Catalog.Tweaks.First(tweak =>
            tweak.Os.ContainsKey("win10") && !tweak.Os.ContainsKey("win11"));

        DebloatPlan plan = DebloatPlanner.Plan(Catalog, new[] { low.Id, gated.Id }, 22631, true);
        string summary = DebloatPageCatalog.BuildImpactSummary(plan);

        Assert.Contains("2 selected", summary);
        Assert.Contains("1 ready", summary);
        Assert.Contains("1 skipped", summary);
        Assert.Contains("Low", summary);
    }

    [Fact]
    public void Review_text_lists_typed_actions_and_never_key_deletion_support()
    {
        DebloatTweak tweak = Catalog.Tweaks.First(candidate =>
            candidate.Actions.All(action => action is RegistrySetAction));

        string review = DebloatPageCatalog.DescribeActions(tweak);

        Assert.Contains(tweak.Actions.OfType<RegistrySetAction>().First().RegistryName, review);
    }
}

public sealed class DebloatRunReportTests
{
    [Fact]
    public void Completed_run_offers_undo_and_reports_backup()
    {
        var record = new DebloatRunRecord
        {
            RunId = "run-1",
            Completed = true,
            BackupPath = @"C:\backups\run-1.reg",
            RestorePointStatus = "Skipped: disabled",
            Entries =
            {
                new DebloatJournalEntry { TweakId = "a", Title = "A" },
                new DebloatJournalEntry { TweakId = "b", Title = "B" },
            },
        };

        DebloatRunReport report = DebloatRunReport.From(record, 3);

        Assert.False(report.IsFailure);
        Assert.True(report.CanUndo);
        Assert.Equal("Applied 2 tweak(s)", report.Headline);
        Assert.Contains("1 selected tweak(s) were already applied", report.Detail);
        Assert.Contains("run-1.reg", report.Detail);
    }

    [Fact]
    public void Rolled_back_run_is_a_failure_and_offers_no_undo()
    {
        var record = new DebloatRunRecord
        {
            RunId = "run-2",
            Completed = false,
            RolledBack = true,
            FailureMessage = "'B' failed: denied",
            Entries =
            {
                new DebloatJournalEntry { TweakId = "a", Title = "A" },
            },
        };

        DebloatRunReport report = DebloatRunReport.From(record, 2);

        Assert.True(report.IsFailure);
        Assert.True(report.RolledBack);
        Assert.False(report.CanUndo);
        Assert.Equal("The run was partially rolled back", report.Headline);
        Assert.Contains("restored them in reverse order", report.Detail);
    }

    [Fact]
    public void Cancelled_or_empty_run_is_not_success()
    {
        DebloatRunReport stopped = DebloatRunReport.From(new DebloatRunRecord
        {
            Completed = false,
            FailureMessage = "Administrator permission was declined before anything changed.",
        }, 2);
        DebloatRunReport empty = DebloatRunReport.From(new DebloatRunRecord
        {
            Completed = true,
            FailureMessage = "Every selected tweak is already applied.",
        }, 2);

        Assert.True(stopped.IsFailure);
        Assert.Contains("declined", stopped.Detail);
        Assert.Equal("Nothing to apply", empty.Headline);
        Assert.False(empty.CanUndo);
    }

    [Fact]
    public async Task Restore_point_toggle_is_recorded_as_skipped_not_faked()
    {
        (DebloatTweak tweak, FakeRegistry registry) = PickTweak();
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var engine = new DebloatEngine(
            new FakeExecutor(registry),
            new FakeRestorePoint(),
            new FakeLogger(),
            new DebloatJournalStore(Path.Combine(root, "journal")),
            Path.Combine(root, "backups"));

        DebloatRunRecord record = await engine.ApplyAsync(
            new[] { tweak.Id }, 22631, true, createRestorePoint: false);

        Assert.True(record.Completed);
        Assert.StartsWith("Skipped: turned off", record.RestorePointStatus);
        Directory.Delete(root, recursive: true);
    }

    private static (DebloatTweak, FakeRegistry) PickTweak()
    {
        DebloatCatalog catalog = DebloatCatalogLoader.LoadEmbedded();
        DebloatTweak tweak = catalog.Tweaks.First(candidate =>
            candidate.Actions.Count > 0 &&
            candidate.Actions.All(action => action is RegistrySetAction) &&
            DebloatPlanner.Plan(catalog, new[] { candidate.Id }, 22631, true).Runnable.Any());
        var registry = new FakeRegistry();

        foreach (RegistrySetAction action in tweak.Actions.OfType<RegistrySetAction>())
        {
            registry.Values[$"{action.Hive}\\{action.Path}\\{action.RegistryName}"] =
                (action.ValueType, JsonSerializer.SerializeToElement<object>(0));
        }

        return (tweak, registry);
    }

    private sealed class FakeRegistry
    {
        public Dictionary<string, (string Type, JsonElement Value)> Values { get; } = new();
    }

    private sealed class FakeExecutor(FakeRegistry registry) : IDebloatExecutor
    {
        public Task<DebloatHelperOutcome> DetectAsync(DebloatTweak tweak, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DebloatHelperOutcome
            {
                Success = true,
                Detected = false,
                Values = tweak.Actions.OfType<RegistrySetAction>().Select(action => new RegistryValueState
                {
                    Hive = action.Hive,
                    Path = action.Path,
                    Name = action.RegistryName,
                    Existed = registry.Values.ContainsKey($"{action.Hive}\\{action.Path}\\{action.RegistryName}"),
                    ValueType = action.ValueType,
                    Value = registry.Values.TryGetValue(
                        $"{action.Hive}\\{action.Path}\\{action.RegistryName}", out var value)
                        ? value.Value
                        : null,
                }).ToList(),
            });

        public Task<DebloatHelperOutcome> ApplyAsync(DebloatTweak tweak, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DebloatHelperOutcome { Success = true });

        public Task<DebloatHelperOutcome> RevertAsync(
            DebloatTweak tweak,
            IReadOnlyList<RegistryValueState> before,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DebloatHelperOutcome { Success = true });
    }

    private sealed class FakeRestorePoint : PCManager.Services.Abstractions.IRestorePointService
    {
        public Task<PCManager.Core.Elevation.ElevationOutcome> CreateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PCManager.Core.Elevation.ElevationOutcome.Failed("Restore points are disabled."));
    }

    private sealed class FakeLogger : PCManager.Services.Abstractions.IAppLogger
    {
        public string LogFilePath => "test.log";
        public void Info(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
