using System.Management;
using System.Runtime.InteropServices;
using PCManager.Core.Formatting;
using PCManager.Core.Models;

namespace PCManager.Services.Sensors;

/// <summary>
/// T2 without elevation: an NVIDIA GPU temperature through nvml.dll when it is installed, then the
/// ACPI thermal zone. Windows denies the thermal-zone WMI class to standard users on many
/// machines, so AccessDenied becomes NeedsPermission with a reason, never a fake value.
/// </summary>
public sealed class WindowsTemperatureProbe : ITemperatureProbe
{
    private const int NvmlSuccess = 0;
    private const uint NvmlTemperatureGpu = 0;

    public TemperatureProbeResult Read()
    {
        double? gpu = ReadNvmlGpu();
        var readings = new List<TemperatureProbeReading>();

        if (gpu is { } gpuCelsius)
        {
            readings.Add(new TemperatureProbeReading(
                TemperatureText.GpuName, gpuCelsius, TemperatureText.NvmlSource));
        }

        TemperatureProbeResult? zone = ReadAcpiZone();
        if (zone is not null)
        {
            if (zone.Status == SensorStatus.Ok)
            {
                readings.AddRange(zone.Readings);
            }
            else if (readings.Count == 0)
            {
                return zone;
            }
        }

        if (readings.Count > 0)
        {
            return new TemperatureProbeResult { Status = SensorStatus.Ok, Readings = readings };
        }

        return zone ?? new TemperatureProbeResult
        {
            Status = SensorStatus.Unavailable,
            Reason = TemperatureText.NoZoneReason,
        };
    }

    /// <summary>ACPI reports tenths of Kelvin; convert before anything else sees the value.</summary>
    public static double FromTenthsKelvin(int raw) => (raw / 10.0) - 273.15;

    private static TemperatureProbeResult? ReadAcpiZone()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT CurrentTemperature, Active FROM MSAcpi_ThermalZoneTemperature");

            foreach (ManagementBaseObject item in searcher.Get())
            {
                using (item)
                {
                    if (item["CurrentTemperature"] is not uint raw)
                    {
                        continue;
                    }

                    double celsius = FromTenthsKelvin((int)raw);
                    if (celsius is <= 0 or > 150)
                    {
                        // Firmware sometimes reports uninitialised zones; do not display them.
                        continue;
                    }

                    return new TemperatureProbeResult
                    {
                        Status = SensorStatus.Ok,
                        Readings = new[]
                        {
                            new TemperatureProbeReading(
                                TemperatureText.SystemZoneName,
                                celsius,
                                TemperatureText.SystemZoneSource),
                        },
                    };
                }
            }

            return new TemperatureProbeResult
            {
                Status = SensorStatus.Unavailable,
                Reason = TemperatureText.NoZoneReason,
            };
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            return new TemperatureProbeResult
            {
                Status = SensorStatus.NeedsPermission,
                Reason = TemperatureText.NeedsAdminReason,
            };
        }
        catch (ManagementException)
        {
            return new TemperatureProbeResult
            {
                Status = SensorStatus.Unavailable,
                Reason = TemperatureText.NoZoneReason,
            };
        }
    }

    private static double? ReadNvmlGpu()
    {
        IntPtr library = IntPtr.Zero;

        try
        {
            library = NativeLibrary.Load("nvml.dll");
            if (library == IntPtr.Zero)
            {
                return null;
            }

            var init = GetDelegate<NvmlInit>(library, "nvmlInit_v2");
            var count = GetDelegate<NvmlDeviceGetCount>(library, "nvmlDeviceGetCount_v2");
            var handle = GetDelegate<NvmlDeviceGetHandle>(library, "nvmlDeviceGetHandleByIndex_v2");
            var temperature = GetDelegate<NvmlDeviceGetTemperature>(library, "nvmlDeviceGetTemperature");

            if (init is null || count is null || handle is null || temperature is null ||
                init() != NvmlSuccess || count(out uint devices) != NvmlSuccess || devices == 0)
            {
                return null;
            }

            if (handle(0, out IntPtr device) != NvmlSuccess)
            {
                return null;
            }

            return temperature(device, NvmlTemperatureGpu, out uint celsius) == NvmlSuccess
                ? celsius
                : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return null;
        }
        finally
        {
            if (library != IntPtr.Zero)
            {
                NativeLibrary.Free(library);
            }
        }
    }

    private static T? GetDelegate<T>(IntPtr library, string name) where T : Delegate
    {
        IntPtr address = NativeLibrary.GetExport(library, name);
        return address == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlInit();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlDeviceGetCount(out uint count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlDeviceGetHandle(uint index, out IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvmlDeviceGetTemperature(IntPtr device, uint sensorType, out uint celsius);
}
