using PCManager.Core.Models;
using PCManager.Services.Network;
using Xunit;

namespace PCManager.Tests;

public sealed class NetworkDiagnosticsTests
{
    [Fact]
    public async Task RunDiagnosticsAsync_ReturnsPingsAndSummary()
    {
        var logger = new TestLogger();
        var service = new NetworkDiagnosticsService(logger);

        NetworkDiagnosticsSummary summary = await service.RunDiagnosticsAsync();

        Assert.NotNull(summary);
        Assert.NotNull(summary.Pings);
        Assert.NotEmpty(summary.Pings);
        Assert.False(string.IsNullOrWhiteSpace(summary.OverallStatus));
    }

    [Fact]
    public void PingResultItem_CalculatesFieldsCorrectly()
    {
        var ping = new PingResultItem("Gateway", "192.168.1.1", true, 5, "5 ms");

        Assert.Equal("Gateway", ping.TargetName);
        Assert.Equal("192.168.1.1", ping.HostOrIp);
        Assert.True(ping.Success);
        Assert.Equal(5, ping.RoundtripTimeMs);
        Assert.Equal("5 ms", ping.StatusText);
    }

    [Fact]
    public void NetworkAdapterDetails_StoresProperties()
    {
        var adapter = new NetworkAdapterDetails(
            "Ethernet0",
            "Intel Wi-Fi 6",
            "192.168.1.50",
            "192.168.1.1",
            "1.1.1.1, 8.8.8.8",
            "1000 Mbps",
            true);

        Assert.Equal("Ethernet0", adapter.Name);
        Assert.True(adapter.IsConnected);
        Assert.Equal("192.168.1.50", adapter.IPv4Address);
    }
}
