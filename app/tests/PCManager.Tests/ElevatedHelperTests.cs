using PCManager.Core.Elevation;
using PCManager.Services.Abstractions;
using PCManager.Services.Elevation;
using Xunit;

namespace PCManager.Tests;

public sealed class ElevatedHelperTests
{
    [Fact]
    public void Accepts_only_the_fixed_machine_wide_rule_ids()
    {
        foreach (string id in new[]
                 {
                     "windows.system-temp",
                     "windows.update-cache",
                     "windows.logs",
                     "windows.prefetch",
                     "windows.patchcache"
                 })
        {
            Assert.True(ElevatedCleanTargets.TryGet(id, out ElevatedCleanTarget target), id);
            Assert.Equal(id, target.RuleId);
            Assert.NotEmpty(target.Roots);
        }

        Assert.False(ElevatedCleanTargets.TryGet(@"C:\Windows\System32", out _));
        Assert.False(ElevatedCleanTargets.TryGet(@"..\..\Windows", out _));
        Assert.False(ElevatedCleanTargets.TryGet("windows.font-cache", out _));
        Assert.False(ElevatedCleanTargets.TryGet(string.Empty, out _));
    }

    [Fact]
    public void Update_cache_is_the_only_target_that_stops_services()
    {
        Assert.True(ElevatedCleanTargets.Get("windows.update-cache").StopUpdateServices);

        Assert.All(
            ElevatedCleanTargets.All.Where(target => target.RuleId != "windows.update-cache"),
            target => Assert.False(target.StopUpdateServices));
    }

    [Fact]
    public void Machine_wide_targets_stay_under_the_windows_or_programdata_roots()
    {
        var allowedRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };

        foreach (string root in ElevatedCleanTargets.All.SelectMany(target => target.Roots))
        {
            Assert.Contains(allowedRoots, allowed =>
                Path.GetFullPath(root).StartsWith(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowed)) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Missing_helper_is_reported_without_starting_a_process()
    {
        var service = new ElevationService(
            new FakeLogger(),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-helper.exe"));

        Assert.False(service.IsHelperAvailable);

        ElevationOutcome outcome = await service.RunAsync(HelperCommand.CleanRule, "windows.logs");

        Assert.False(outcome.Success);
        Assert.False(outcome.Cancelled);
        Assert.Contains("missing", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_point_service_uses_the_fixed_restore_point_command()
    {
        var fake = new FakeElevation();
        var service = new RestorePointService(fake);

        ElevationOutcome outcome = await service.CreateAsync();

        Assert.True(outcome.Success);
        Assert.Equal(HelperCommand.CreateRestorePoint, fake.LastCommand);
        Assert.Null(fake.LastRuleId);
    }

    private sealed class FakeElevation : IElevationService
    {
        public HelperCommand? LastCommand { get; private set; }
        public string? LastRuleId { get; private set; }
        public bool IsHelperAvailable => true;

        public Task<ElevationOutcome> RunAsync(
            HelperCommand command,
            string? ruleId = null,
            IReadOnlyList<string>? domains = null,
            CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            LastRuleId = ruleId;
            return Task.FromResult(new ElevationOutcome(true, false, 0, 0, 0, "created"));
        }
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
