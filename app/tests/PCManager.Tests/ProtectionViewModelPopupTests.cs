using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.ViewModels;
using Xunit;

namespace PCManager.Tests;

public sealed class ProtectionViewModelPopupTests
{
    private sealed class FakePopupMonitor : IPopupMonitorService
    {
        public PopupSourceInfo Source { get; set; } =
            new("notepad", "Untitled - Notepad", 42, 1, IsAllowed: true, SeenCount: 2);

        public List<(string ProcessName, bool Allowed)> Calls { get; } = new();

        public List<string> Allowed { get; } = new();

        public int HistoryClears { get; private set; }

        public Task<IReadOnlyList<PopupSourceInfo>> ScanAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PopupSourceInfo>>(new[] { Source });

        public Task<bool> IsAllowedAsync(string processName)
            => Task.FromResult(Source.IsAllowed);

        public Task SetAllowedAsync(string processName, bool allowed)
        {
            Calls.Add((processName, allowed));
            Source = Source with { IsAllowed = allowed };

            if (allowed)
            {
                if (!Allowed.Contains(processName, StringComparer.OrdinalIgnoreCase))
                {
                    Allowed.Add(processName);
                }
            }
            else
            {
                Allowed.RemoveAll(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetAllowedAsync()
            => Task.FromResult<IReadOnlyList<string>>(
                Allowed.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList());

        public Task ClearHistoryAsync()
        {
            HistoryClears++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProtectionService : IProtectionService
    {
        public DefenderStatus GetDefenderStatus() => new();

        public UpdateStatus GetUpdateStatus() => new();
    }

    private sealed class FakeRepairService : ISystemRepairService
    {
        public OperationResult OpenNotificationSettings() => OperationResult.Ok("opened");

        public OperationResult OpenDefaultAppsSettings() => OperationResult.Ok("opened");

        public OperationResult OpenWindowsUpdateSettings() => OperationResult.Ok("opened");

        public OperationResult OpenAppsFeatures() => OperationResult.Ok("opened");

        public OperationResult RestartExplorer() => OperationResult.Ok("restarted");
    }

    private sealed class FakeBrowserService : IDefaultBrowserService
    {
        public DefaultBrowserSetting GetDefaultBrowser() => new();

        public OperationResult OpenDefaultAppsSettings() => OperationResult.Ok("opened");
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();

        public bool IsFirstRun => false;

        public int SaveCount { get; private set; }

        public void Save() => SaveCount++;
    }

    [Fact]
    public void SpeedTestEndpoint_persists_edits_and_reloads_them()
    {
        var settings = new FakeSettingsService();
        settings.Current.SpeedTestEndpoint = "https://example.com/custom.bin";

        var vm = new ProtectionViewModel(
            new FakeProtectionService(),
            new FakeRepairService(),
            new FakeBrowserService(),
            new TestLogger(),
            settings: settings);

        Assert.Equal("https://example.com/custom.bin", vm.SpeedTestEndpoint);

        vm.SpeedTestEndpoint = "https://example.org/other.bin";

        Assert.Equal("https://example.org/other.bin", settings.Current.SpeedTestEndpoint);
        Assert.Equal(1, settings.SaveCount);

        var relaunched = new ProtectionViewModel(
            new FakeProtectionService(),
            new FakeRepairService(),
            new FakeBrowserService(),
            new TestLogger(),
            settings: settings);

        Assert.Equal("https://example.org/other.bin", relaunched.SpeedTestEndpoint);
    }

    [Fact]
    public async Task TogglePopupAllowCommand_revokes_an_allowed_app()
    {
        var popups = new FakePopupMonitor();
        var vm = new ProtectionViewModel(
            new FakeProtectionService(),
            new FakeRepairService(),
            new FakeBrowserService(),
            new TestLogger(),
            popups: popups);

        await vm.TogglePopupAllowCommand.ExecuteAsync(popups.Source);

        (string ProcessName, bool Allowed) call = Assert.Single(popups.Calls);
        Assert.Equal("notepad", call.ProcessName);
        Assert.False(call.Allowed);
        Assert.False(popups.Source.IsAllowed);
    }

    [Fact]
    public async Task Allowed_apps_are_listed_separately_and_can_be_revoked_separately()
    {
        var popups = new FakePopupMonitor();
        popups.Allowed.Add("notepad");
        popups.Allowed.Add("acrobat");

        var vm = new ProtectionViewModel(
            new FakeProtectionService(),
            new FakeRepairService(),
            new FakeBrowserService(),
            new TestLogger(),
            popups: popups);

        await vm.LoadAsync();

        Assert.True(vm.HasAllowedPopups);
        Assert.Equal(new[] { "acrobat", "notepad" }, vm.AllowedPopups);
        Assert.Empty(vm.PopupSources);

        await vm.RevokePopupAllowCommand.ExecuteAsync("notepad");

        Assert.Equal(new[] { "acrobat" }, popups.Allowed);
        Assert.Equal(new[] { "acrobat" }, vm.AllowedPopups);
        Assert.True(vm.HasAllowedPopups);
        Assert.Contains("removed from the allow list", vm.PopupSummary);

        await vm.RevokePopupAllowCommand.ExecuteAsync("acrobat");

        Assert.Empty(vm.AllowedPopups);
        Assert.False(vm.HasAllowedPopups);
    }

    [Fact]
    public async Task ClearPopupHistoryCommand_clears_local_counts_and_scan_results()
    {
        var popups = new FakePopupMonitor();
        var vm = new ProtectionViewModel(
            new FakeProtectionService(),
            new FakeRepairService(),
            new FakeBrowserService(),
            new TestLogger(),
            popups: popups);

        await vm.ScanPopupsCommand.ExecuteAsync(null);
        Assert.True(vm.HasPopupResults);
        Assert.Single(vm.PopupSources);

        await vm.ClearPopupHistoryCommand.ExecuteAsync(null);

        Assert.Equal(1, popups.HistoryClears);
        Assert.False(vm.HasPopupResults);
        Assert.Empty(vm.PopupSources);
        Assert.Contains("sighting history cleared", vm.PopupSummary, StringComparison.OrdinalIgnoreCase);
    }
}
