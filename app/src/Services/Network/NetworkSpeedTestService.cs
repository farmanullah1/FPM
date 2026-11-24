using System.Diagnostics;
using System.Net.Http;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Network;

/// <summary>
/// Downloads from the configured endpoint for at most 20 seconds and reports the measured
/// throughput. No result is cached or reused; a failed request reports the HTTP or transport error.
/// </summary>
public sealed class NetworkSpeedTestService : INetworkSpeedTestService
{
    private const int MaxSeconds = 20;
    private const int BufferSize = 64 * 1024;

    private static readonly HttpClient Client = CreateClient();

    private readonly IAppLogger _logger;

    public NetworkSpeedTestService(IAppLogger logger) => _logger = logger;

    public async Task<SpeedTestResult> RunAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (!TryValidateEndpoint(endpoint, out Uri uri, out string endpointError))
        {
            return SpeedTestResult.Failed(endpointError, endpoint);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(MaxSeconds));

            var stopwatch = Stopwatch.StartNew();
            using HttpResponseMessage response = await Client
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return SpeedTestResult.Failed(
                    $"The endpoint returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                    uri.ToString());
            }

            await using Stream stream = await response.Content
                .ReadAsStreamAsync(timeout.Token)
                .ConfigureAwait(false);

            byte[] buffer = new byte[BufferSize];
            long total = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                total += read;

                if (stopwatch.Elapsed.TotalSeconds >= MaxSeconds)
                {
                    break;
                }
            }

            stopwatch.Stop();
            double seconds = stopwatch.Elapsed.TotalSeconds;

            if (total <= 0 || seconds <= 0.01)
            {
                return SpeedTestResult.Failed(
                    "The endpoint returned no measurable data.",
                    uri.ToString());
            }

            double megabits = total * 8.0 / seconds / 1_000_000.0;
            _logger.Info($"Speed test {uri}: {total} bytes in {seconds:0.00}s ({megabits:0.0} Mbps).");

            return new SpeedTestResult(
                true,
                Math.Round(megabits, 1),
                total,
                Math.Round(seconds, 2),
                uri.ToString(),
                $"{megabits:0.0} Mbps - {ByteFormatter.Format(total)} in {seconds:0.0}s from {uri.Host}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SpeedTestResult.Failed(
                $"The speed test did not finish within {MaxSeconds} seconds.",
                uri.ToString());
        }
        catch (Exception ex)
        {
            _logger.Error("Speed test failed.", ex);
            return SpeedTestResult.Failed($"Speed test failed: {ex.Message}", uri.ToString());
        }
    }

    /// <summary>Exposed for tests: only absolute http(s) endpoints with a host are accepted.</summary>
    public static bool TryValidateEndpoint(string endpoint, out Uri uri, out string error)
    {
        uri = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            error = "Enter a speed-test endpoint URL.";
            return false;
        }

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(parsed.Host))
        {
            error = "The speed-test endpoint must be an absolute http(s) URL.";
            return false;
        }

        uri = parsed!;
        return true;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FPM-SpeedTest/1.0");
        return client;
    }
}
