using System.Net.NetworkInformation;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Network;

/// <summary>
/// Monitors real NIC byte-counter deltas to report accurate download/upload speeds.
/// Handles multiple adapters, counter wraps/resets, and disconnected states.
/// </summary>
public sealed class NetworkThroughputService : INetworkThroughputService
{
    private readonly object _sync = new();
    private NetworkThroughputSnapshot _current = NetworkThroughputSnapshot.Empty;
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastTimestamp = DateTime.UtcNow;
    private bool _initialized;

    public event EventHandler<NetworkThroughputSnapshot>? ThroughputUpdated;

    public NetworkThroughputSnapshot GetCurrentThroughput()
    {
        lock (_sync)
        {
            if (!_initialized)
            {
                Refresh();
            }
            return _current;
        }
    }

    public void Refresh()
    {
        lock (_sync)
        {
            DateTime now = DateTime.UtcNow;
            double elapsedSeconds = (now - _lastTimestamp).TotalSeconds;

            if (elapsedSeconds <= 0.05 && _initialized)
            {
                return;
            }

            long totalReceived = 0;
            long totalSent = 0;
            string primaryAdapter = "None";

            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (NetworkInterface ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    IPInterfaceStatistics stats = ni.GetIPStatistics();
                    totalReceived += stats.BytesReceived;
                    totalSent += stats.BytesSent;

                    if (primaryAdapter == "None" &&
                        ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)
                    {
                        primaryAdapter = ni.Name;
                    }
                }

                if (primaryAdapter == "None" && interfaces.Length > 0)
                {
                    primaryAdapter = interfaces[0].Name;
                }
            }
            catch
            {
                // Network stats can fail on locked-down environments
            }

            if (!_initialized)
            {
                _lastBytesReceived = totalReceived;
                _lastBytesSent = totalSent;
                _lastTimestamp = now;
                _initialized = true;
                _current = new NetworkThroughputSnapshot(0, 0, "0 KB/s", "0 KB/s", primaryAdapter, now);
                ThroughputUpdated?.Invoke(this, _current);
                return;
            }

            long deltaReceived = totalReceived - _lastBytesReceived;
            long deltaSent = totalSent - _lastBytesSent;

            // Handle system reboot, NIC disconnect, or counter resets
            if (deltaReceived < 0) deltaReceived = 0;
            if (deltaSent < 0) deltaSent = 0;

            long bpsReceived = elapsedSeconds > 0 ? (long)(deltaReceived / elapsedSeconds) : 0;
            long bpsSent = elapsedSeconds > 0 ? (long)(deltaSent / elapsedSeconds) : 0;

            _lastBytesReceived = totalReceived;
            _lastBytesSent = totalSent;
            _lastTimestamp = now;

            _current = new NetworkThroughputSnapshot(
                bpsReceived,
                bpsSent,
                FormatSpeed(bpsReceived),
                FormatSpeed(bpsSent),
                primaryAdapter,
                now);

            ThroughputUpdated?.Invoke(this, _current);
        }
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return "0 KB/s";
        }
        return $"{ByteFormatter.Format(bytesPerSecond)}/s";
    }

    public void Dispose()
    {
        // No unmanaged resources
    }
}
