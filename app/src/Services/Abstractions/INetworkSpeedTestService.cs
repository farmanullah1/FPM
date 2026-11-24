using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Measures download throughput from a user-supplied http(s) endpoint. It never runs on its own:
/// the caller starts it, and the endpoint is displayed with the result.
/// </summary>
public interface INetworkSpeedTestService
{
    Task<SpeedTestResult> RunAsync(string endpoint, CancellationToken cancellationToken = default);
}
