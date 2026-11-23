using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Real measured NIC byte-counter deltas (KB/s, MB/s) across active adapters.
/// </summary>
public interface INetworkThroughputService : IDisposable
{
    NetworkThroughputSnapshot GetCurrentThroughput();
    event EventHandler<NetworkThroughputSnapshot>? ThroughputUpdated;
    void Refresh();
}
