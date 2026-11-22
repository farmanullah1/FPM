using System.Management;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Protection;

/// <summary>
/// Windows Defender status.
///
/// Primary source: the Defender WMI provider class MSFT_MpComputerStatus in
/// root\Microsoft\Windows\Defender, which exposes AntivirusEnabled,
/// RealTimeProtectionEnabled, the signature version and the last quick-scan time.
/// Fallback: root\SecurityCenter2 AntiVirusProduct, whose productState bitfield encodes
/// (byte 2) whether the scanner is on and (byte 0) whether definitions are current.
/// </summary>
public sealed class DefenderStatusService : IDefenderStatusService
{
    public DefenderStatus GetStatus()
    {
        if (TryDefenderProvider(out DefenderStatus status))
        {
            return status;
        }

        if (TrySecurityCenter(out status))
        {
            return status;
        }

        return new DefenderStatus
        {
            StatusText = "Defender status is not available. Another antivirus may be managing protection."
        };
    }

    private static bool TryDefenderProvider(out DefenderStatus status)
    {
        status = new DefenderStatus();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Defender",
                "SELECT AntivirusEnabled, RealTimeProtectionEnabled, AntivirusSignatureVersion, " +
                "AntivirusSignatureLastUpdated, QuickScanStartTime FROM MSFT_MpComputerStatus");

            foreach (ManagementBaseObject result in searcher.Get())
            {
                using (result)
                {
                    status.AntivirusEnabled = GetBool(result, "AntivirusEnabled");
                    status.RealTimeEnabled = GetBool(result, "RealTimeProtectionEnabled");
                    status.SignatureVersion = GetString(result, "AntivirusSignatureVersion");
                    status.StatusText = Describe(status);
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Provider unavailable (Defender disabled, third-party AV, or non-Windows SKU).
        }

        return false;
    }

    private static bool TrySecurityCenter(out DefenderStatus status)
    {
        status = new DefenderStatus();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2",
                "SELECT displayName, productState FROM AntiVirusProduct");

            foreach (ManagementBaseObject result in searcher.Get())
            {
                using (result)
                {
                    string name = GetString(result, "displayName");
                    int state = GetInt(result, "productState");

                    // productState = 0x00AA00BB
                    //   BB = definition status: 0x00 up to date, 0x10 outdated
                    //   AA = scanner state:    0x10 enabled,     0x00 disabled
                    int definitionState = state & 0xFF;
                    int scannerState = (state >> 8) & 0xFF;

                    status.AntivirusEnabled = scannerState == 0x10 || scannerState == 0x11;
                    status.RealTimeEnabled = status.AntivirusEnabled;
                    status.SignatureVersion = definitionState == 0x00 ? "Up to date" : "Outdated";
                    status.StatusText = status.AntivirusEnabled
                        ? $"{name} is protecting this PC."
                        : $"{name} is installed but protection is turned off.";
                    return true;
                }
            }
        }
        catch (Exception) { }

        return false;
    }

    private static string Describe(DefenderStatus status)
    {
        if (!status.AntivirusEnabled)
        {
            return "Antivirus protection is turned off.";
        }

        if (!status.RealTimeEnabled)
        {
            return "Real-time protection is turned off.";
        }

        return "Real-time protection is on and definitions are current.";
    }

    private static bool GetBool(ManagementBaseObject obj, string property, bool fallback = false)
    {
        try
        {
            object? value = obj[property];
            return value is null ? fallback : Convert.ToBoolean(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static int GetInt(ManagementBaseObject obj, string property, int fallback = 0)
    {
        try
        {
            object? value = obj[property];
            return value is null ? fallback : Convert.ToInt32(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static string GetString(ManagementBaseObject obj, string property)
    {
        try
        {
            return obj[property]?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
