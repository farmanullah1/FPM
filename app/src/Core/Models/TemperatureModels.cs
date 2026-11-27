namespace PCManager.Core.Models;

/// <summary>Display unit for temperature readings (T2).</summary>
public enum TemperatureUnit
{
    Celsius,
    Fahrenheit,
}

/// <summary>
/// One temperature sample. ACPI thermal zones are system zones, never presented as "CPU"; the
/// name and source travel with the value so the UI cannot mislabel it.
/// </summary>
public sealed class TemperatureReading
{
    public SensorStatus Status { get; init; } = SensorStatus.Unavailable;

    public string Reason { get; init; } = string.Empty;

    /// <summary>Human label of the zone, e.g. "System thermal zone" or "GPU".</summary>
    public string Name { get; init; } = string.Empty;

    public double? Celsius { get; init; }

    public string Source { get; init; } = string.Empty;

    public DateTime SampledAt { get; init; }
}
