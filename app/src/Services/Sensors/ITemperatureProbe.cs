using PCManager.Core.Models;

namespace PCManager.Services.Sensors;

/// <summary>One probe reading with its own label and source.</summary>
public sealed record TemperatureProbeReading(string Name, double Celsius, string Source);

/// <summary>
/// Result of one temperature probe pass. A status other than Ok carries the honest reason;
/// readings are empty in that case.
/// </summary>
public sealed class TemperatureProbeResult
{
    public SensorStatus Status { get; init; } = SensorStatus.Unavailable;

    public string Reason { get; init; } = string.Empty;

    public IReadOnlyList<TemperatureProbeReading> Readings { get; init; } = Array.Empty<TemperatureProbeReading>();
}

/// <summary>Seam for tests; the real probe talks to NVML and root\WMI.</summary>
public interface ITemperatureProbe
{
    TemperatureProbeResult Read();
}
