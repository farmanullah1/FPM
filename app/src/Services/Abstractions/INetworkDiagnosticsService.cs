using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Adapter list and DNS resolution for the Network tool.</summary>
public interface INetworkDiagnosticsService
{
    IReadOnlyList<NetworkAdapterInfo> GetAdapters();
    Task<DnsLookupResult> LookupAsync(string hostName, CancellationToken cancellationToken = default);
}
