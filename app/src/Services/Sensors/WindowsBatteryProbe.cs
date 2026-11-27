using System.Management;
using System.Runtime.InteropServices;

namespace PCManager.Services.Sensors;

/// <summary>
/// Battery data without elevation: GetSystemPowerStatus for percent/AC/charging/time and the
/// root\WMI battery classes for remaining capacity, discharge rate and design/full capacity
/// (battery health). Capacity values do not change while FPM runs, so they are cached once.
/// </summary>
public sealed class WindowsBatteryProbe : IBatteryProbe
{
    private const uint UnknownLifeSeconds = 0xFFFFFFFF;
    private const byte NoBatteryFlag = 0x80;
    private const byte ChargingFlag = 0x08;
    private const byte PercentUnknown = 0xFF;
    private const byte AcUnknown = 0xFF;

    private readonly object _sync = new();
    private bool _capacitiesRead;
    private uint? _designed;
    private uint? _full;

    public BatteryProbeResult Read()
    {
        SystemPowerStatus status = default;
        bool ok = GetSystemPowerStatus(out status);
        bool hasBattery = ok && (status.BatteryFlag & NoBatteryFlag) == 0;
        int? percent = ok && status.BatteryLifePercent != PercentUnknown
            ? status.BatteryLifePercent
            : null;
        bool? acOnline = ok
            ? status.ACLineStatus switch
            {
                0 => false,
                1 => true,
                _ => null,
            }
            : null;

        TimeSpan? life = ok && status.BatteryLifeTime != UnknownLifeSeconds
            ? TimeSpan.FromSeconds(status.BatteryLifeTime)
            : null;

        uint? rate = null;
        uint? remaining = null;

        if (hasBattery)
        {
            (remaining, rate) = ReadStatus();
            ReadCapacities();
        }

        return new BatteryProbeResult
        {
            HasBattery = hasBattery,
            AcOnline = acOnline,
            Charging = ok && (status.BatteryFlag & ChargingFlag) != 0,
            Percent = percent,
            LifeRemaining = life,
            RateMilliwatts = rate,
            RemainingMilliwattHours = remaining,
            DesignedMilliwattHours = _designed,
            FullChargedMilliwattHours = _full,
        };
    }

    private static (uint? Remaining, uint? Rate) ReadStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT RemainingCapacity, Rate FROM BatteryStatus");

            foreach (ManagementBaseObject item in searcher.Get())
            {
                using (item)
                {
                    return (ToUInt(item["RemainingCapacity"]), ToUInt(item["Rate"]));
                }
            }
        }
        catch (ManagementException)
        {
            // Some firmware exposes no BatteryStatus class; the probe reports the gap.
        }

        return (null, null);
    }

    private void ReadCapacities()
    {
        lock (_sync)
        {
            if (_capacitiesRead)
            {
                return;
            }

            _capacitiesRead = true;
            _designed = ReadScalar("BatteryStaticData", "DesignedCapacity");
            _full = ReadScalar("BatteryFullChargedCapacity", "FullChargedCapacity");
        }
    }

    private static uint? ReadScalar(string className, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                $"SELECT {property} FROM {className}");

            foreach (ManagementBaseObject item in searcher.Get())
            {
                using (item)
                {
                    return ToUInt(item[property]);
                }
            }
        }
        catch (ManagementException)
        {
            // Class absent on this firmware.
        }

        return null;
    }

    private static uint? ToUInt(object? value) => value switch
    {
        uint number => number,
        int number => (uint)Math.Max(0, number),
        long number => (uint)Math.Max(0, number),
        _ => null,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
