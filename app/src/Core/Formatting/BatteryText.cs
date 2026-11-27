namespace PCManager.Core.Formatting;

/// <summary>One shared sampling cadence for every sensor surface (T1).</summary>
public static class SensorCadence
{
    public static readonly TimeSpan Visible = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan Background = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Every battery string, unit, interval and formatting rule lives here so Home, the toolbar and
/// Settings cannot drift apart. Unavailable states are explicit; there is no fabricated value.
/// </summary>
public static class BatteryText
{
    public const string NoBattery = "No battery detected";
    public const string Unavailable = "Unavailable";
    public const string Calculating = "Calculating…";
    public const string OnAc = "On AC power";
    public const string FullyCharged = "Fully charged";
    public const string Charging = "Charging";
    public const string OnBattery = "On battery";
    public const string EstimatedSuffix = " (estimated)";
    public const string HealthLabel = "Health";
    public const string PowerStatusSource = "GetSystemPowerStatus";
    public const string WmiSource = "root\\WMI battery classes";
    public const string UnknownReason = "Windows did not report a battery state";

    public static string Percent(int? percent) => percent is { } value ? $"{value}%" : Unavailable;

    public static string Time(TimeSpan? time, bool estimated)
    {
        if (time is not { } value)
        {
            return Calculating;
        }

        int totalMinutes = (int)Math.Round(value.TotalMinutes);
        string text = totalMinutes >= 60
            ? $"{totalMinutes / 60} h {totalMinutes % 60:00} m"
            : $"{totalMinutes} m";

        return estimated ? text + EstimatedSuffix : text;
    }

    public static string Health(double? healthPercent) =>
        healthPercent is { } value ? $"{value:0}%" : Unavailable;

    public static string SampledAt(DateTime sampledAt) =>
        $"Sampled {sampledAt:HH:mm:ss}";

    public static string Detail(Models.BatteryReading reading) => reading.Status switch
    {
        Models.SensorStatus.Ok => reading.PowerLine == Models.BatteryPowerLine.Online
            ? OnAc
            : reading.Charging ? Charging : OnBattery,
        _ => reading.Reason.Length > 0 ? reading.Reason : Unavailable,
    };
}
