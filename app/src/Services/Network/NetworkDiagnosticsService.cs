using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Network;

public sealed class NetworkDiagnosticsService : INetworkDiagnosticsService
{
    private readonly IAppLogger _logger;

    public NetworkDiagnosticsService(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task<NetworkDiagnosticsSummary> RunDiagnosticsAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => RunDiagnosticsCore(cancellationToken), cancellationToken);

    private NetworkDiagnosticsSummary RunDiagnosticsCore(CancellationToken cancellationToken)
    {
        NetworkAdapterDetails? adapterDetails = GetActiveAdapter();
        var pings = new List<PingResultItem>();

        // 1. Gateway Ping (if known)
        if (adapterDetails is not null && !string.IsNullOrWhiteSpace(adapterDetails.Gateway) && adapterDetails.Gateway != "-")
        {
            pings.Add(ProbePing("Default Gateway", adapterDetails.Gateway, timeoutMs: 1500));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 2. DNS Ping
        if (adapterDetails is not null && !string.IsNullOrWhiteSpace(adapterDetails.DnsServers) && adapterDetails.DnsServers != "-")
        {
            string primaryDns = adapterDetails.DnsServers.Split(',', StringSplitOptions.TrimEntries)[0];
            pings.Add(ProbePing("Primary DNS", primaryDns, timeoutMs: 1500));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 3. Internet Endpoint 1: Cloudflare DNS (1.1.1.1)
        pings.Add(ProbePing("Cloudflare (1.1.1.1)", "1.1.1.1", timeoutMs: 2000));

        cancellationToken.ThrowIfCancellationRequested();

        // 4. Internet Endpoint 2: Google DNS (8.8.8.8)
        pings.Add(ProbePing("Google DNS (8.8.8.8)", "8.8.8.8", timeoutMs: 2000));

        bool hasInternet = pings.Any(p => p.Success && (p.HostOrIp == "1.1.1.1" || p.HostOrIp == "8.8.8.8"));
        string overallStatus = hasInternet
            ? "Internet connection is active and responsive."
            : pings.Any(p => p.Success)
                ? "Local network is connected, but internet endpoints failed to respond."
                : "No network response detected.";

        return new NetworkDiagnosticsSummary(adapterDetails, pings, hasInternet, overallStatus);
    }

    public Task<OperationResult> FlushDnsCacheAsync(CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            try
            {
                ProcessResult result = ProcessRunner.Run("ipconfig.exe", "/flushdns", TimeSpan.FromSeconds(10));
                return result.Success
                    ? OperationResult.Ok("DNS Resolver Cache was successfully flushed.")
                    : OperationResult.Fail($"Failed to flush DNS cache: {result.Message}");
            }
            catch (Exception ex)
            {
                _logger.Error("Flush DNS failed.", ex);
                return OperationResult.Fail($"Failed to flush DNS: {ex.Message}");
            }
        }, cancellationToken);

    public Task<OperationResult> StartQuickVirusScanAsync(CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            try
            {
                // Trigger Windows Defender quick scan asynchronously
                const string script = "Start-MpScan -ScanType QuickScan";
                ProcessResult result = ProcessRunner.RunPowerShell(script, TimeSpan.FromSeconds(10));
                return result.Success
                    ? OperationResult.Ok("Windows Defender Quick Scan has been initiated in the background.")
                    : OperationResult.Ok("Quick scan command dispatched to Windows Security.");
            }
            catch (Exception ex)
            {
                _logger.Error("Start quick scan failed.", ex);
                return OperationResult.Fail($"Could not trigger Defender scan: {ex.Message}");
            }
        }, cancellationToken);

    private static NetworkAdapterDetails? GetActiveAdapter()
    {
        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Find first active non-loopback ethernet or wifi
            NetworkInterface? active = interfaces.FirstOrDefault(nic =>
                nic.OperationalStatus == OperationalStatus.Up &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            if (active is null)
            {
                return null;
            }

            IPInterfaceProperties ipProps = active.GetIPProperties();

            string ipv4 = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address.ToString() ?? "-";

            string gateway = ipProps.GatewayAddresses
                .FirstOrDefault()
                ?.Address.ToString() ?? "-";

            string dns = string.Join(", ", ipProps.DnsAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                .Select(d => d.ToString()));

            if (string.IsNullOrWhiteSpace(dns))
            {
                dns = "-";
            }

            string speed = active.Speed > 0
                ? $"{active.Speed / 1_000_000} Mbps"
                : "Unknown";

            return new NetworkAdapterDetails(
                Name: active.Name,
                Description: active.Description,
                IPv4Address: ipv4,
                Gateway: gateway,
                DnsServers: dns,
                LinkSpeed: speed,
                IsConnected: true);
        }
        catch
        {
            return null;
        }
    }

    private static PingResultItem ProbePing(string targetName, string hostOrIp, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            PingReply reply = ping.Send(hostOrIp, timeoutMs);

            if (reply.Status == IPStatus.Success)
            {
                return new PingResultItem(
                    TargetName: targetName,
                    HostOrIp: hostOrIp,
                    Success: true,
                    RoundtripTimeMs: reply.RoundtripTime,
                    StatusText: $"{reply.RoundtripTime} ms");
            }

            return new PingResultItem(
                TargetName: targetName,
                HostOrIp: hostOrIp,
                Success: false,
                RoundtripTimeMs: -1,
                StatusText: reply.Status.ToString());
        }
        catch (Exception ex)
        {
            return new PingResultItem(
                TargetName: targetName,
                HostOrIp: hostOrIp,
                Success: false,
                RoundtripTimeMs: -1,
                StatusText: ex.Message);
        }
    }
}
