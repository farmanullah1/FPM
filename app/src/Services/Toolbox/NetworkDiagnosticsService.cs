using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Toolbox;

/// <summary>
/// Adapter enumeration (NetworkInterface) and DNS resolution (Dns.GetHostAddressesAsync).
/// </summary>
public sealed class NetworkDiagnosticsService : INetworkDiagnosticsService
{
    public IReadOnlyList<NetworkAdapterInfo> GetAdapters()
    {
        var adapters = new List<NetworkAdapterInfo>();

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            // Loopback and tunnel adapters add noise without telling the user anything.
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                IPInterfaceProperties properties = nic.GetIPProperties();

                string ipv4 = properties.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?
                    .Address.ToString() ?? string.Empty;

                string ipv6 = properties.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal)?
                    .Address.ToString() ?? string.Empty;

                string gateway = properties.GatewayAddresses
                    .Select(g => g.Address.ToString())
                    .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a) && a != "0.0.0.0") ?? string.Empty;

                adapters.Add(new NetworkAdapterInfo
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    IPv4 = ipv4,
                    IPv6 = ipv6,
                    Gateway = gateway,
                    DnsServers = string.Join(", ", properties.DnsAddresses.Select(d => d.ToString())),
                    MacAddress = string.Join("-", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))),
                    SpeedBitsPerSecond = nic.Speed,
                    IsUp = nic.OperationalStatus == OperationalStatus.Up
                });
            }
            catch (Exception)
            {
                // Adapter was removed mid-enumeration.
            }
        }

        return adapters
            .OrderByDescending(a => a.IsUp)
            .ThenBy(a => a.Name)
            .ToList();
    }

    public async Task<DnsLookupResult> LookupAsync(string hostName, CancellationToken cancellationToken = default)
    {
        var result = new DnsLookupResult { HostName = hostName };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostName, cancellationToken).ConfigureAwait(false);
            result.Addresses = addresses.Select(a => a.ToString()).Distinct().ToList();

            if (result.Addresses.Count == 0)
            {
                result.Error = "No addresses were returned.";
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }
}
