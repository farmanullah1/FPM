using System.Text.Json;
using PCManager.Core.Debloat;
using PCManager.Core.Elevation;
using PCManager.Services.Abstractions;
using PCManager.Services.Debloat;
using PCManager.ViewModels;
using Xunit;

namespace PCManager.Tests;

public sealed class DebloatTargetTests
{
    private const string CurrentSid = "S-1-5-21-111";
    private static readonly DebloatUserProfile[] Profiles =
    {
        new("Owner", CurrentSid, @"C:\Users\Owner", true) { IsCurrentUser = true },
        new("Ada", "S-1-5-21-222", @"C:\Users\Ada", true),
        new("Bob", "S-1-5-21-333", @"C:\Users\Bob", false),
    };

    [Fact]
    public void Current_and_empty_tokens_use_the_live_current_hive()
    {
        DebloatTarget current = DebloatTargetResolver.Resolve("", Profiles, CurrentSid, 22631).Target!;
        DebloatTarget named = DebloatTargetResolver.Resolve("Owner", Profiles, CurrentSid, 22631).Target!;

        Assert.Equal(DebloatTargetKind.CurrentUser, current.Kind);
        Assert.Equal("HKCU", current.MountRoot);
        Assert.False(current.NeedsLoad);
        Assert.Equal(DebloatTargetKind.CurrentUser, named.Kind);
    }

    [Fact]
    public void Signed_in_other_user_uses_the_live_hive_and_offline_user_loads_ntuser()
    {
        DebloatTarget live = DebloatTargetResolver.Resolve("Ada", Profiles, CurrentSid, 22631).Target!;
        DebloatTarget offline = DebloatTargetResolver.Resolve("sid:S-1-5-21-333", Profiles, CurrentSid, 22631).Target!;

        Assert.Equal(@"HKU\S-1-5-21-222", live.MountRoot);
        Assert.False(live.NeedsLoad);
        Assert.Contains("live hive", live.Warning);

        Assert.Equal(@"HKU\FPM_S1521333", offline.MountRoot);
        Assert.True(offline.NeedsLoad);
        Assert.Equal(@"C:\Users\Bob\NTUSER.DAT", offline.HiveFile);
        Assert.Contains("unload", offline.Warning);
    }

    [Fact]
    public void Default_user_is_gated_by_os_and_uses_its_own_mount()
    {
        DebloatTarget target = DebloatTargetResolver.Resolve(
            "default", Profiles, CurrentSid, 22631, @"C:\Users\Default\NTUSER.DAT").Target!;

        Assert.Equal(DebloatTargetKind.DefaultUser, target.Kind);
        Assert.Equal(@"HKU\FPM_Default", target.MountRoot);
        Assert.True(target.NeedsLoad);
        Assert.Contains("SYSPREP", target.Warning);

        DebloatTargetResolution old = DebloatTargetResolver.Resolve("default", Profiles, CurrentSid, 9600);
        Assert.False(old.Success);
        Assert.Contains("Windows 10", old.Message);
    }

    [Theory]
    [InlineData("nobody")]
    [InlineData("sid:S-1-5-21-999")]
    [InlineData("sid:not-a-sid")]
    public void Unknown_or_malformed_profiles_are_refused(string token)
    {
        DebloatTargetResolution result = DebloatTargetResolver.Resolve(token, Profiles, CurrentSid, 22631);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public void Hive_scope_loads_and_always_unloads_on_success_and_failure()
    {
        string file = Path.GetTempFileName();
        var api = new FakeHiveApi();
        DebloatTarget target = new(
            "sid:S-1-5-21-333", DebloatTargetKind.OtherUser, "Bob", @"HKU\FPM_X", true, file, "");

        using (DebloatHiveScope.Acquire(api, target))
        {
            Assert.True(api.Loaded);
        }

        Assert.False(api.Loaded);
        Assert.Equal(1, api.Loads);
        Assert.Equal(1, api.Unloads);

        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using DebloatHiveScope? scope = DebloatHiveScope.Acquire(api, target);
            throw new InvalidOperationException("apply failed");
        }));

        Assert.False(api.Loaded);
        File.Delete(file);
    }

    [Fact]
    public void Missing_hive_file_and_already_mounted_hive_are_refused()
    {
        var api = new FakeHiveApi();
        DebloatTarget missing = new(
            "sid:S-1-5-21-333", DebloatTargetKind.OtherUser, "Bob", @"HKU\FPM_X", true,
            @"C:\does-not-exist\NTUSER.DAT", "");

        Assert.Throws<FileNotFoundException>(() => DebloatHiveScope.Acquire(api, missing));

        string file = Path.GetTempFileName();
        api.Loaded = true;
        DebloatTarget mounted = missing with { HiveFile = file };

        Assert.Throws<InvalidOperationException>(() => DebloatHiveScope.Acquire(api, mounted));
        File.Delete(file);
    }

    private sealed class FakeHiveApi : IDebloatHiveApi
    {
        public bool Loaded { get; set; }
        public int Loads { get; private set; }
        public int Unloads { get; private set; }

        public bool IsLoaded(string mountKey) => Loaded;

        public void Load(string mountKey, string hiveFile)
        {
            Loads++;
            Loaded = true;
        }

        public void Unload(string mountKey)
        {
            Unloads++;
            Loaded = false;
        }
    }
}

public sealed class DebloatPresetFileTests
{
    private static readonly DebloatCatalog Catalog = DebloatCatalogLoader.LoadEmbedded();

    [Fact]
    public void Round_trip_keeps_tweaks_and_apps_and_drops_unknown_ids()
    {
        string tweak = Catalog.Tweaks[0].Id;
        string app = Catalog.Apps[0].Id;
        string json = DebloatPresetFile.Serialize(new[] { tweak, "unknown.tweak" }, new[] { app, "unknown.app" });

        DebloatPresetImport import = DebloatPresetFile.Parse(json, Catalog);

        Assert.Equal(new[] { tweak }, import.TweakIds);
        Assert.Equal(new[] { app }, import.AppIds);
        Assert.Equal(2, import.Warnings.Count);
        Assert.Contains(import.Warnings, warning => warning.Contains("unknown.tweak"));
    }

    [Fact]
    public void Unsupported_version_and_bad_json_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            DebloatPresetFile.Parse("""{"version":99,"tweaks":[],"apps":[]}""", Catalog));
        Assert.Throws<InvalidDataException>(() =>
            DebloatPresetFile.Parse("not json", Catalog));
    }
}

public sealed class DebloatCommandLineTests
{
    [Fact]
    public void Dry_run_parses_without_silent_or_risk_gate()
    {
        DebloatCommandParse parse = DebloatCommandLine.Parse(
            new[] { "--debloat", "--preset", "minimal", "--dry-run" });

        Assert.True(parse.Success);
        Assert.True(parse.Options!.DryRun);
        Assert.False(parse.Options.Silent);
        Assert.Equal("minimal", parse.Options.Preset);
    }

    [Fact]
    public void Silent_requires_accept_risk()
    {
        DebloatCommandParse parse = DebloatCommandLine.Parse(
            new[] { "--debloat", "--tweaks", "win11debloat.DisableTelemetry", "--silent" });

        Assert.False(parse.Success);
        Assert.Contains("accept-risk", parse.Error);
    }

    [Theory]
    [InlineData("--preset", "nope")]
    [InlineData("--tweaks", "")]
    public void Invalid_preset_and_empty_tweaks_are_refused(string option, string value)
    {
        DebloatCommandParse parse = DebloatCommandLine.Parse(
            new[] { "--debloat", option, value, "--dry-run" });

        Assert.False(parse.Success);
    }

    [Fact]
    public void Apps_without_a_tweak_selection_is_refused()
    {
        DebloatCommandParse parse = DebloatCommandLine.Parse(
            new[] { "--debloat", "--apps", "Clipchamp.Clipchamp", "--dry-run" });

        Assert.False(parse.Success);
        Assert.Contains("App removal", parse.Error);
    }
}

public sealed class DebloatTargetPropagationTests
{
    [Fact]
    public async Task Engine_passes_the_target_to_detect_apply_and_undo_and_journals_it()
    {
        DebloatCatalog catalog = DebloatCatalogLoader.LoadEmbedded();
        DebloatTweak tweak = catalog.Tweaks.First(candidate =>
            candidate.Actions.Count > 0 &&
            candidate.Actions.All(action => action is RegistrySetAction) &&
            DebloatPlanner.Plan(catalog, new[] { candidate.Id }, 22631, true).Runnable.Any());
        var executor = new RecordingExecutor();
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var engine = new DebloatEngine(
            executor,
            new FakeRestorePoint(),
            new FakeLogger(),
            new DebloatJournalStore(Path.Combine(root, "journal")),
            Path.Combine(root, "backups"));

        DebloatRunRecord record = await engine.ApplyAsync(
            new[] { tweak.Id }, 22631, true, targetUser: "sid:S-1-5-21-333");

        Assert.True(record.Completed);
        Assert.Equal("sid:S-1-5-21-333", record.TargetUser);
        Assert.All(executor.Targets, target => Assert.Equal("sid:S-1-5-21-333", target));

        await engine.UndoAsync();
        Assert.All(executor.Targets, target => Assert.Equal("sid:S-1-5-21-333", target));
        Directory.Delete(root, recursive: true);
    }

    private sealed class RecordingExecutor : IDebloatExecutor
    {
        public List<string> Targets { get; } = new();

        public Task<DebloatHelperOutcome> DetectAsync(
            DebloatTweak tweak,
            string targetUser = "",
            CancellationToken cancellationToken = default)
        {
            Targets.Add(targetUser);
            return Task.FromResult(new DebloatHelperOutcome
            {
                Success = true,
                Detected = false,
                Values = Array.Empty<RegistryValueState>(),
            });
        }

        public Task<DebloatHelperOutcome> ApplyAsync(
            DebloatTweak tweak,
            string targetUser = "",
            CancellationToken cancellationToken = default)
        {
            Targets.Add(targetUser);
            return Task.FromResult(new DebloatHelperOutcome { Success = true });
        }

        public Task<DebloatHelperOutcome> RevertAsync(
            DebloatTweak tweak,
            IReadOnlyList<RegistryValueState> before,
            string targetUser = "",
            CancellationToken cancellationToken = default)
        {
            Targets.Add(targetUser);
            return Task.FromResult(new DebloatHelperOutcome { Success = true });
        }
    }

    private sealed class FakeRestorePoint : IRestorePointService
    {
        public Task<ElevationOutcome> CreateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ElevationOutcome.Failed("Restore points are disabled."));
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
}

public sealed class DebloatDriftTests
{
    [Fact]
    public void Drift_summary_reports_reverted_tweaks_without_reapplying()
    {
        string summary = DebloatDrift.Summarize(new[]
        {
            new DebloatDriftItem("a", "Telemetry", true),
            new DebloatDriftItem("b", "Taskbar search", false),
        });

        Assert.Contains("1 still applied", summary);
        Assert.Contains("Taskbar search", summary);
        Assert.Contains("Nothing was reapplied", summary);
    }
}
