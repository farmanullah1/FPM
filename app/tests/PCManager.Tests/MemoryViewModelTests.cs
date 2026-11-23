using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.ViewModels;
using Xunit;

namespace PCManager.Tests;

public sealed class MemoryViewModelTests
{
    private sealed class FakeMemoryDiagnostics : IMemoryDiagnosticsService
    {
        private int _calls;

        public ExtendedMemorySnapshot GetExtendedSnapshot()
        {
            _calls++;
            // Call 1 is LoadAsync (50%)
            // Call 2 is before relief (50%)
            // Call 3+ is after relief (46%)
            uint load = _calls >= 3 ? 46u : 50u;
            ulong avail = _calls >= 3 ? 8_500_000_000u : 8_000_000_000u;

            return new ExtendedMemorySnapshot(
                TotalPhysicalBytes: 16_000_000_000,
                AvailableBytes: avail,
                LoadPercent: load,
                CommittedBytes: 10_000_000_000,
                CommitLimitBytes: 20_000_000_000,
                PressureLevel: MemoryPressureLevel.Healthy,
                Timestamp: DateTime.UtcNow,
                TopConsumers: Array.Empty<ProcessMemoryItem>());
        }
    }

    private sealed class FakeMemoryRelief : IMemoryReliefService
    {
        public List<ProcessMemoryItem> Processes { get; set; } = new()
        {
            new ProcessMemoryItem(100, "AppA", 200_000_000, 150_000_000, IsProtected: false, CanRelieve: true),
            new ProcessMemoryItem(200, "AppB", 300_000_000, 250_000_000, IsProtected: false, CanRelieve: true),
            new ProcessMemoryItem(4, "System", 500_000_000, 500_000_000, IsProtected: true, CanRelieve: false)
        };

        public IReadOnlyList<ProcessMemoryItem> GetEligibleProcesses() => Processes;

        public Task<MemoryReliefResult> ExecuteReliefPlanAsync(MemoryReliefPlan plan, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MemoryReliefResult(
                Attempted: plan.ProcessIdsToClose.Count,
                Succeeded: plan.ProcessIdsToClose.Count,
                Failed: 0,
                EstimatedBytesFreed: 500_000_000,
                ClosedProcesses: new[] { "AppA", "AppB" },
                Summary: "Closed 2 applications."));
        }
    }

    private sealed class FakeConfirmation : IUserConfirmationService
    {
        public bool ResultToReturn { get; set; } = true;
        public bool Confirm(string title, string message) => ResultToReturn;
    }

    [Fact]
    public async Task MemoryViewModel_LoadAsync_PopulatesMetricsAndProcesses()
    {
        var diag = new FakeMemoryDiagnostics();
        var relief = new FakeMemoryRelief();
        var confirm = new FakeConfirmation();
        var logger = new TestLogger();

        var vm = new MemoryViewModel(diag, relief, confirm, logger);
        await vm.LoadAsync();

        Assert.Equal(50u, vm.LoadPercent);
        Assert.Contains("available", vm.AvailablePhysicalText);
        Assert.Equal(3, vm.Processes.Count);
        Assert.Equal("Healthy", vm.PressureText);
    }

    [Fact]
    public async Task MemoryViewModel_SelectAllSafe_OnlySelectsRelievableProcesses()
    {
        var diag = new FakeMemoryDiagnostics();
        var relief = new FakeMemoryRelief();
        var confirm = new FakeConfirmation();
        var logger = new TestLogger();

        var vm = new MemoryViewModel(diag, relief, confirm, logger);
        await vm.LoadAsync();

        vm.SelectAllSafeCommand.Execute(null);

        Assert.True(vm.Processes.First(p => p.ProcessName == "AppA").IsSelected);
        Assert.True(vm.Processes.First(p => p.ProcessName == "AppB").IsSelected);
        Assert.False(vm.Processes.First(p => p.ProcessName == "System").IsSelected);
    }

    [Fact]
    public async Task MemoryViewModel_RelieveSelectedAsync_ExecutesPlanAndReportsMeasurements()
    {
        var diag = new FakeMemoryDiagnostics();
        var relief = new FakeMemoryRelief();
        var confirm = new FakeConfirmation();
        var logger = new TestLogger();

        var vm = new MemoryViewModel(diag, relief, confirm, logger);
        await vm.LoadAsync();

        vm.SelectAllSafeCommand.Execute(null);

        await vm.RelieveSelectedCommand.ExecuteAsync(null);

        Assert.True(vm.HasReliefReport);
        Assert.Contains("Relief complete: Closed 2 app(s)", vm.ReliefReportText);
        Assert.Contains("50% to 46%", vm.ReliefReportText);
    }
}
