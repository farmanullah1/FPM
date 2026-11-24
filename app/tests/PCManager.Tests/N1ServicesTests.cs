using System.Net;
using System.Net.Sockets;
using System.Text;
using PCManager.Services.Network;
using PCManager.Services.Protection;
using Xunit;

namespace PCManager.Tests;

public sealed class SpeedTestServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file")]
    [InlineData("https://")]
    public void TryValidateEndpoint_RejectsInvalidValues(string endpoint)
    {
        bool valid = NetworkSpeedTestService.TryValidateEndpoint(endpoint, out _, out string error);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task RunAsync_MeasuresBytesFromLocalEndpoint()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Task server = Task.Run(async () =>
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();

            var request = new byte[4096];
            await stream.ReadAsync(request);

            byte[] body = new byte[8 * 1024 * 1024];
            string headers =
                $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n";

            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers));
            await stream.WriteAsync(body);
            await stream.FlushAsync();
        });

        try
        {
            var service = new NetworkSpeedTestService(new TestLogger());
            var result = await service.RunAsync($"http://127.0.0.1:{port}/speed");

            Assert.True(result.Success, result.Message);
            Assert.True(result.BytesDownloaded > 0);
            Assert.True(result.MegabitsPerSecond > 0);
            Assert.Equal(8 * 1024 * 1024, result.BytesDownloaded);
        }
        finally
        {
            listener.Stop();
            await Task.WhenAny(server, Task.Delay(TimeSpan.FromSeconds(5)));
        }
    }
}

public sealed class HostsBlockServiceTests
{
    [Fact]
    public void TryParseDomains_TrimsDeduplicatesAndLowercases()
    {
        bool ok = HostsBlockService.TryParseDomains(
            " Ads.Example.com, example.org\nads.example.com ",
            out IReadOnlyList<string> domains,
            out string error);

        Assert.True(ok, error);
        Assert.Equal(new[] { "ads.example.com", "example.org" }, domains);
    }

    [Theory]
    [InlineData("not a domain")]
    [InlineData("localhost")]
    [InlineData("-bad.example.com")]
    [InlineData("bad_.example.com")]
    public void TryParseDomains_RejectsInvalidNames(string text)
    {
        bool ok = HostsBlockService.TryParseDomains(text, out _, out string error);

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void CountBlockedDomains_CountsOnlyInsideTheFpmSection()
    {
        string[] lines =
        {
            "127.0.0.1 localhost",
            "# BEGIN FPM",
            "0.0.0.0 ads.example.com",
            "0.0.0.0 tracker.example.org",
            "# END FPM",
            "0.0.0.0 outside.example.net"
        };

        Assert.Equal(2, HostsBlockService.CountBlockedDomains(lines));
    }
}
