namespace PCManager.Services.Sensors;

/// <summary>Raw battery data from Windows. Nullable fields mean "Windows did not report it".</summary>
public sealed class BatteryProbeResult
{
    public bool HasBattery { get; init; }

    public bool? AcOnline { get; init; }

    public bool Charging { get; init; }

    public int? Percent { get; init; }

    /// <summary>BatteryLifeTime seconds; 0xFFFFFFFF means Windows does not know.</summary>
    public TimeSpan? LifeRemaining { get; init; }

    public uint? RateMilliwatts { get; init; }

    public uint? RemainingMilliwattHours { get; init; }

    public uint? DesignedMilliwattHours { get; init; }

    public uint? FullChargedMilliwattHours { get; init; }
}

/// <summary>Seam for tests: the real implementation talks to kernel32 and root\WMI.</summary>
public interface IBatteryProbe
{
    BatteryProbeResult Read();
}
