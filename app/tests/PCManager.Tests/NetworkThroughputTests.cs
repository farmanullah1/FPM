using PCManager.Core.Models;
using PCManager.Services.Network;
using Xunit;

namespace PCManager.Tests;

public sealed class NetworkThroughputTests
{
    [Fact]
    public void NetworkThroughputService_ReturnsValidSnapshot()
    {
        using var service = new NetworkThroughputService();

        NetworkThroughputSnapshot snapshot = service.GetCurrentThroughput();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.BytesReceivedPerSecond >= 0);
        Assert.True(snapshot.BytesSentPerSecond >= 0);
        Assert.NotNull(snapshot.FormattedDownloadSpeed);
        Assert.NotNull(snapshot.FormattedUploadSpeed);
        Assert.NotNull(snapshot.ActiveAdapterName);
    }

    [Fact]
    public void NetworkThroughputService_SubsequentRefresh_CalculatesDeltas()
    {
        using var service = new NetworkThroughputService();

        service.Refresh();
        NetworkThroughputSnapshot first = service.GetCurrentThroughput();
        Assert.NotNull(first);

        // Sleep briefly to simulate time delta
        Thread.Sleep(60);
        service.Refresh();
        NetworkThroughputSnapshot second = service.GetCurrentThroughput();

        Assert.NotNull(second);
        Assert.True(second.BytesReceivedPerSecond >= 0);
        Assert.True(second.BytesSentPerSecond >= 0);
    }

    [Fact]
    public void EmptySnapshot_HasReasonableDefaults()
    {
        var empty = NetworkThroughputSnapshot.Empty;

        Assert.Equal(0, empty.BytesReceivedPerSecond);
        Assert.Equal(0, empty.BytesSentPerSecond);
        Assert.Equal("0 KB/s", empty.FormattedDownloadSpeed);
        Assert.Equal("0 KB/s", empty.FormattedUploadSpeed);
    }
}
