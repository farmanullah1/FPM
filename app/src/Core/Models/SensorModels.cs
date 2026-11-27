namespace PCManager.Core.Models;

/// <summary>Outcome of one sensor sample. Unavailable is a real answer, never a placeholder value.</summary>
public enum SensorStatus
{
    Ok,
    Unavailable,
    NeedsPermission,
    Error,
}

public enum BatteryPowerLine
{
    Unknown,
    Online,
    Offline,
}

/// <summary>
/// One battery sample. Percent is null whenever Windows did not report it; the UI must show
/// "Unavailable" with <see cref="Reason"/> rather than inventing a number.
/// </summary>
public sealed class BatteryReading
{
    public SensorStatus Status { get; init; } = SensorStatus.Unavailable;

    public string Reason { get; init; } = string.Empty;

    public bool HasBattery { get; init; }

    public int? Percent { get; init; }

    public BatteryPowerLine PowerLine { get; init; }

    public bool Charging { get; init; }

    public TimeSpan? TimeRemaining { get; init; }

    public bool TimeEstimated { get; init; }

    public double? HealthPercent { get; init; }

    public string Source { get; init; } = string.Empty;

    public DateTime SampledAt { get; init; }

    public static BatteryReading NoBattery(DateTime sampledAt) => new()
    {
        Status = SensorStatus.Unavailable,
        Reason = PCManager.Core.Formatting.BatteryText.NoBattery,
        HasBattery = false,
        PowerLine = BatteryPowerLine.Unknown,
        Source = PCManager.Core.Formatting.BatteryText.PowerStatusSource,
        SampledAt = sampledAt,
    };
}
