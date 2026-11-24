namespace PCManager.Core.Models;

/// <summary>
/// Hardware and configuration details of an active network adapter.
/// </summary>
public sealed record NetworkAdapterDetails(
    string Name,
    string Description,
    string IPv4Address,
    string Gateway,
    string DnsServers,
    string LinkSpeed,
    bool IsConnected);

/// <summary>
/// Latency measurement to a destination endpoint.
/// </summary>
public sealed record PingResultItem(
    string TargetName,
    string HostOrIp,
    bool Success,
    long RoundtripTimeMs,
    string StatusText);

/// <summary>
/// Aggregate result of a network diagnostic test.
/// </summary>
public sealed record NetworkDiagnosticsSummary(
    NetworkAdapterDetails? Adapter,
    IReadOnlyList<PingResultItem> Pings,
    bool HasInternet,
    string OverallStatus);
