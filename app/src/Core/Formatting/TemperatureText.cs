using System.Globalization;
using PCManager.Core.Models;

namespace PCManager.Core.Formatting;

/// <summary>T2 thresholds: ok below 70 °C, warm 70–85, hot above 85.</summary>
public static class TemperatureThresholds
{
    public const double WarmCelsius = 70;
    public const double HotCelsius = 85;
}

/// <summary>Severity buckets used for text and colour; never inferred in the views.</summary>
public enum TemperatureSeverity
{
    Normal,
    Warm,
    Hot,
}

/// <summary>
/// Every temperature string, unit, threshold and conversion lives here. The value shown is the
/// sampled value; absence stays "Unavailable" with the probe's reason.
/// </summary>
public static class TemperatureText
{
    public const string Unavailable = "Unavailable";
    public const string SystemZoneName = "System thermal zone";
    public const string GpuName = "GPU";
    public const string SystemZoneSource = "ACPI thermal zone (root\\WMI MSAcpi_ThermalZoneTemperature)";
    public const string NvmlSource = "NVIDIA NVML";
    public const string NoZoneReason = "Windows exposes no ACPI thermal zone";
    public const string NeedsAdminReason =
        "Windows blocks thermal zone access; hardware sensors need administrator rights";
    public const string NoNvmlReason = "NVIDIA NVML is not present on this machine";
    public const string CpuNotClaimed =
        "System thermal zone (ACPI) is not a CPU temperature";

    public static string UnitSymbol(TemperatureUnit unit) =>
        unit == TemperatureUnit.Fahrenheit ? "°F" : "°C";

    public static double Convert(double celsius, TemperatureUnit unit) =>
        unit == TemperatureUnit.Fahrenheit ? (celsius * 9 / 5) + 32 : celsius;

    public static string Format(double? celsius, TemperatureUnit unit) =>
        celsius is { } value
            ? $"{Convert(value, unit).ToString("0", CultureInfo.InvariantCulture)} {UnitSymbol(unit)}"
            : Unavailable;

    public static TemperatureSeverity Severity(double? celsius) =>
        celsius switch
        {
            null => TemperatureSeverity.Normal,
            >= TemperatureThresholds.HotCelsius => TemperatureSeverity.Hot,
            >= TemperatureThresholds.WarmCelsius => TemperatureSeverity.Warm,
            _ => TemperatureSeverity.Normal,
        };

    public static string SeverityLabel(TemperatureSeverity severity) => severity switch
    {
        TemperatureSeverity.Hot => "Hot",
        TemperatureSeverity.Warm => "Warm",
        _ => "Normal",
    };

    public static string SampledAt(DateTime sampledAt) => $"Sampled {sampledAt:HH:mm:ss}";
}
