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

        public Task<IReadOnlyList<PopupSourceInfo>> ScanAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PopupSourceInfo>>(new[] { Source });

        public Task<bool> IsAllowedAsync(string processName)
            => Task.FromResult(Source.IsAllowed);

        public Task SetAllowedAsync(string processName, bool allowed)
        {
            Calls.Add((processName, allowed));
            Source = Source with { IsAllowed = allowed };
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
}
