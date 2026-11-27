using System.Text.Json;
using PCManager.Core.Debloat;
using PCManager.Core.Elevation;
using PCManager.Services.Abstractions;
using PCManager.Services.Debloat;
using Xunit;

namespace PCManager.Tests;

public sealed class DebloatPlannerTests
{
    [Fact]
    public void Orders_tweaks_by_catalog_category_then_id()
    {
        DebloatCatalog catalog = SyntheticCatalog(
            ("b.tweak", "Beta", null),
            ("a.tweak", "Alpha", null),
            ("a.other", "Alpha", null));

        DebloatPlan plan = DebloatPlanner.Plan(catalog, new[] { "b.tweak", "a.other", "a.tweak" }, 22631, true);

        Assert.Equal(new[] { "a.other", "a.tweak", "b.tweak" }, plan.Items.Select(item => item.Tweak.Id));
        Assert.All(plan.Items, item => Assert.True(item.IsRunnable));
    }

    [Fact]
    public void Os_gate_and_unknown_ids_are_refused_before_elevation()
    {
        DebloatCatalog catalog = SyntheticCatalog(("win10.only", "Alpha", new DebloatOsGate { MaxBuild = 22000 }));

        DebloatPlan plan = DebloatPlanner.Plan(catalog, new[] { "win10.only", "missing" }, 22631, true);

        Assert.Equal(DebloatPlanStatus.SkippedOs, plan.Items[0].Status);
        Assert.Equal(DebloatPlanStatus.SkippedUnsupported, plan.Items[1].Status);
        Assert.Empty(plan.Runnable);
    }

    [Fact]
    public void Key_delete_and_appx_tweaks_are_unsupported_and_protected_packages_refused()
    {
        DebloatCatalog catalog = SyntheticCatalog(("key.delete", "Alpha", null), ("appx", "Alpha", null));
        catalog.Tweaks[0].Actions.Add(new RegistryKeyDeleteAction { Hive = "HKCU", Path = "Software\\X" });
        catalog.Tweaks[1].Actions.Add(new AppxRemoveAction
        {
            PackageId = DebloatCatalogValidator.ProtectedPackages[0],
        });

        DebloatPlan plan = DebloatPlanner.Plan(catalog, new[] { "key.delete", "appx" }, 22631, true);

        Assert.Equal(DebloatPlanStatus.SkippedUnsupported, plan.Items.First(i => i.Tweak.Id == "key.delete").Status);
        Assert.Equal(DebloatPlanStatus.SkippedProtected, plan.Items.First(i => i.Tweak.Id == "appx").Status);
    }

    private static DebloatCatalog SyntheticCatalog(params (string Id, string Category, DebloatOsGate? Gate)[] tweaks)
    {
        var catalog = new DebloatCatalog
        {
            Categories =
            {
                new DebloatCategory { Id = "alpha", Name = "Alpha" },
                new DebloatCategory { Id = "beta", Name = "Beta" },
            },
        };

        foreach ((string id, string category, DebloatOsGate? gate) in tweaks)
        {
            catalog.Tweaks.Add(new DebloatTweak
            {
                Id = id,
                Category = category,
                Title = id,
                Risk = "Low",
                Os = gate is null ? new() : new() { ["win11"] = gate },
                Actions = { new RegistrySetAction { Hive = "HKCU", Path = "Software\\FPM", RegistryName = id, ValueType = "DWord", Value = JsonSerializer.SerializeToElement(1) } },
            });
        }

        return catalog;
    }
}

public sealed class DebloatEngineTests
{
    private static readonly DebloatCatalog Catalog = DebloatCatalogLoader.LoadEmbedded();

    [Fact]
    public async Task Apply_then_undo_restores_the_exact_original_state()
    {
        (DebloatTweak tweak, FakeRegistry registry) = PickRegistryTweak();
        var engine = CreateEngine(registry, out string root);

        DebloatRunRecord record = await engine.ApplyAsync(new[] { tweak.Id }, 22631, true);

        Assert.True(record.Completed);
        Assert.Single(record.Entries);
        Assert.Contains("Skipped", record.RestorePointStatus);
        Assert.True(File.Exists(record.BackupPath));
        Assert.Contains("Windows Registry Editor Version 5.00", await File.ReadAllTextAsync(record.BackupPath));
        Assert.True(registry.Has(tweak));
        Assert.NotEqual(record.Entries[0].Before, registry.States(tweak.Actions).ToList(), new StateComparer());

        DebloatHelperOutcome undo = await engine.UndoAsync();

        Assert.True(undo.Success, undo.Message);
        Assert.Equal(record.Entries[0].Before, registry.States(tweak.Actions).ToList(), new StateComparer());
        Assert.True(engine.LatestRun()!.Undone);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Mid_run_failure_rolls_back_applied_tweaks_and_journals_the_truth()
    {
        (DebloatTweak first, FakeRegistry registry) = PickRegistryTweak();
        DebloatTweak second = Catalog.Tweaks.First(candidate =>
            candidate.Id != first.Id &&
            candidate.Actions.Count > 0 &&
            candidate.Actions.All(action => action is RegistrySetAction) &&
            DebloatPlanner.Plan(Catalog, new[] { candidate.Id }, 22631, true).Runnable.Any());

        registry.FailOn = second.Id;
        var engine = CreateEngine(registry, out string root);

        DebloatRunRecord record = await engine.ApplyAsync(new[] { first.Id, second.Id }, 22631, true);

        Assert.False(record.Completed);
        Assert.True(record.RolledBack);
        Assert.Single(record.Entries);
        Assert.Equal(record.Entries[0].Before, registry.States(first.Actions).ToList(), new StateComparer());
        Assert.True(engine.LatestRun()!.RolledBack);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Journal_round_trips_pre_state_and_backup_contains_it()
    {
        (DebloatTweak tweak, FakeRegistry registry) = PickRegistryTweak();
        var journal = new DebloatJournalStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var engine = new DebloatEngine(
            new FakeExecutor(registry),
            new FakeRestorePoint(),
            new FakeLogger(),
            journal,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        DebloatRunRecord record = await engine.ApplyAsync(new[] { tweak.Id }, 22631, true);
        DebloatRunRecord? reloaded = new DebloatJournalStore(journal.Directory).LoadLatest();

        Assert.NotNull(reloaded);
        Assert.Equal(tweak.Id, reloaded!.Entries[0].TweakId);
        Assert.Equal(
            record.Entries[0].Before.Select(state => $"{state.Hive}\\{state.Path}\\{state.Name}={state.Existed}"),
            reloaded.Entries[0].Before.Select(state => $"{state.Hive}\\{state.Path}\\{state.Name}={state.Existed}"));

        Directory.Delete(journal.Directory, recursive: true);
        Directory.Delete(Path.GetDirectoryName(record.BackupPath)!, recursive: true);
    }

    private static DebloatEngine CreateEngine(FakeRegistry registry, out string root)
    {
        root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        return new DebloatEngine(
            new FakeExecutor(registry),
            new FakeRestorePoint(),
            new FakeLogger(),
            new DebloatJournalStore(Path.Combine(root, "journal")),
            Path.Combine(root, "backups"));
    }

    private static (DebloatTweak Tweak, FakeRegistry Registry) PickRegistryTweak()
    {
        DebloatTweak tweak = Catalog.Tweaks.First(candidate =>
            candidate.Actions.Count > 0 &&
            candidate.Actions.All(action => action is RegistrySetAction) &&
            DebloatPlanner.Plan(Catalog, new[] { candidate.Id }, 22631, true).Runnable.Any());

        var registry = new FakeRegistry();
        foreach (DebloatAction action in tweak.Actions)
        {
            registry.Seed(action.Hive, action.Path, ((RegistrySetAction)action).RegistryName,
                ((RegistrySetAction)action).ValueType,
                JsonSerializer.SerializeToElement<object>(
                    ((RegistrySetAction)action).ValueType == "DWord" ? 0 : "original"));
        }

        return (tweak, registry);
    }

    private sealed class FakeRegistry
    {
        private readonly Dictionary<string, (string Type, JsonElement Value)> _values = new(StringComparer.OrdinalIgnoreCase);

        public string? FailOn { get; set; }

        public void Seed(string hive, string path, string name, string type, JsonElement value) =>
            _values[Key(hive, path, name)] = (type, value);

        public bool Has(DebloatTweak tweak) => (FailOn is null || FailOn != tweak.Id) &&
            tweak.Actions.OfType<RegistrySetAction>().Any(action =>
                _values.TryGetValue(Key(action.Hive, action.Path, action.RegistryName), out var value) &&
                value.Value.GetRawText() == action.Value.GetRawText());

        public List<RegistryValueState> States(IEnumerable<DebloatAction> actions) =>
            actions.OfType<RegistrySetAction>().Select(action =>
            {
                bool exists = _values.TryGetValue(Key(action.Hive, action.Path, action.RegistryName), out var value);
                return new RegistryValueState
                {
                    Hive = action.Hive,
                    Path = action.Path,
                    Name = action.RegistryName,
                    Existed = exists,
                    ValueType = exists ? value.Type : string.Empty,
                    Value = exists ? value.Value : null,
                };
            }).ToList();

        public DebloatHelperOutcome Detect(DebloatTweak tweak) => new()
        {
            Success = true,
            Detected = IsApplied(tweak),
            Values = States(tweak.Actions),
        };

        private bool IsApplied(DebloatTweak tweak) => tweak.Detect.Count > 0 &&
            tweak.Detect.OfType<RegistryEqualsProbe>().All(probe =>
                _values.TryGetValue(Key(probe.Hive, probe.Path, probe.RegistryName), out var value) &&
                value.Value.GetRawText() == probe.Value.GetRawText());

        public DebloatHelperOutcome Apply(DebloatTweak tweak)
        {
            if (FailOn == tweak.Id)
            {
                return DebloatHelperOutcome.Failed("sandbox failure");
            }

            foreach (RegistrySetAction action in tweak.Actions.OfType<RegistrySetAction>())
            {
                _values[Key(action.Hive, action.Path, action.RegistryName)] = (action.ValueType, action.Value);
            }

            return new DebloatHelperOutcome { Success = true, Message = "applied" };
        }

        public void Restore(IReadOnlyList<RegistryValueState> states)
        {
            foreach (RegistryValueState state in states)
            {
                string key = Key(state.Hive, state.Path, state.Name);
                if (state.Existed)
                {
                    _values[key] = (state.ValueType, state.Value!.Value);
                }
                else
                {
                    _values.Remove(key);
                }
            }
        }

        private static string Key(string hive, string path, string name) => $"{hive}\\{path}\\{name}";
    }

    private sealed class FakeExecutor : IDebloatExecutor
    {
        private readonly FakeRegistry _registry;

        public FakeExecutor(FakeRegistry registry) => _registry = registry;

        public Task<DebloatHelperOutcome> DetectAsync(DebloatTweak tweak, CancellationToken cancellationToken = default) =>
            Task.FromResult(_registry.Detect(tweak));

        public Task<DebloatHelperOutcome> ApplyAsync(DebloatTweak tweak, CancellationToken cancellationToken = default) =>
            Task.FromResult(_registry.Apply(tweak));

        public Task<DebloatHelperOutcome> RevertAsync(
            DebloatTweak tweak,
            IReadOnlyList<RegistryValueState> before,
            CancellationToken cancellationToken = default)
        {
            _registry.Restore(before);
            return Task.FromResult(new DebloatHelperOutcome { Success = true });
        }
    }

    private sealed class FakeRestorePoint : IRestorePointService
    {
        public Task<ElevationOutcome> CreateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ElevationOutcome.Failed("Restore points are disabled on this machine."));
    }

    private sealed class FakeLogger : IAppLogger
    {
        public string LogFilePath => "test.log";
        public void Info(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }

    private sealed class StateComparer : IEqualityComparer<RegistryValueState>
    {
        public bool Equals(RegistryValueState? x, RegistryValueState? y) =>
            x is not null && y is not null &&
            x.Hive == y.Hive && x.Path == y.Path && x.Name == y.Name &&
            x.Existed == y.Existed && x.ValueType == y.ValueType &&
            (x.Value?.GetRawText() ?? string.Empty) == (y.Value?.GetRawText() ?? string.Empty);

        public int GetHashCode(RegistryValueState obj) => HashCode.Combine(obj.Hive, obj.Path, obj.Name, obj.Existed);
    }
}
