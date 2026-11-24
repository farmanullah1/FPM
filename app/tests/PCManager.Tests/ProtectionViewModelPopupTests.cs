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
