using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Sensors;

/// <summary>
/// One timer for every sensor surface (T1 battery, T2 temperatures). The service owns the samples
/// and the inputs to every label; probes are the only Windows-specific parts, so the decisions are
/// unit-tested. 5 s while a surface is visible, 30 s otherwise.
/// </summary>
public sealed class SensorService : ISensorService
{
    private readonly IBatteryProbe _batteryProbe;
    private readonly ITemperatureProbe _temperatureProbe;
    private readonly IAppLogger _logger;
    private readonly object _sync = new();

    private BatteryReading? _battery;
    private IReadOnlyList<TemperatureReading> _temperatures = Array.Empty<TemperatureReading>();
    private Timer? _timer;
    private bool _sampled;
    private bool _disposed;

    public SensorService(
        IBatteryProbe batteryProbe,
        ITemperatureProbe temperatureProbe,
        IAppLogger logger)
    {
        _batteryProbe = batteryProbe;
        _temperatureProbe = temperatureProbe;
        _logger = logger;
    }

    public event EventHandler<BatteryReading>? BatteryChanged;

    public event EventHandler<IReadOnlyList<TemperatureReading>>? TemperatureChanged;

    public BatteryReading Battery
    {
        get
        {
            lock (_sync)
            {
                if (_battery is null)
                {
                    SampleAll();
                }

                return _battery!;
            }
        }
    }

    public IReadOnlyList<TemperatureReading> Temperatures
    {
        get
        {
            lock (_sync)
            {
                if (!_sampled)
                {
                    SampleAll();
                }

                return _temperatures;
            }
        }
    }

    public void Refresh()
    {
        BatteryReading battery;
        IReadOnlyList<TemperatureReading> temperatures;

        lock (_sync)
        {
            (battery, temperatures) = SampleAll();
        }

        BatteryChanged?.Invoke(this, battery);
        TemperatureChanged?.Invoke(this, temperatures);
    }

    public void SetDemand(bool visible)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            TimeSpan interval = visible ? SensorCadence.Visible : SensorCadence.Background;
            _timer ??= new Timer(_ => Refresh(), null, Timeout.Infinite, Timeout.Infinite);

            // A demand change samples immediately so a newly shown surface never displays a stale
            // reading, then keeps the shared cadence.
            _timer.Change(TimeSpan.Zero, interval);
        }
    }

    private (BatteryReading Battery, IReadOnlyList<TemperatureReading> Temperatures) SampleAll()
    {
        BatteryReading battery = SampleBattery();
        IReadOnlyList<TemperatureReading> temperatures = SampleTemperatures();

        _battery = battery;
        _temperatures = temperatures;
        _sampled = true;

        return (battery, temperatures);
    }

    private BatteryReading SampleBattery()
    {
        try
        {
            BatteryReading reading = Build(_batteryProbe.Read(), DateTime.Now);

            if (_battery is null || Changed(_battery, reading))
            {
                _logger.Info(
                    $"Battery: {BatteryText.Percent(reading.Percent)}, {BatteryText.Detail(reading)}, " +
                    $"time {BatteryText.Time(reading.TimeRemaining, reading.TimeEstimated)}, " +
                    $"health {BatteryText.Health(reading.HealthPercent)} ({reading.Source}).");
            }

            return reading;
        }
        catch (Exception ex)
        {
            _logger.Error("Battery sample failed.", ex);
            return new BatteryReading
            {
                Status = SensorStatus.Error,
                Reason = "Battery sample failed",
                HasBattery = true,
                Source = BatteryText.PowerStatusSource,
                SampledAt = DateTime.Now,
            };
        }
    }

    private IReadOnlyList<TemperatureReading> SampleTemperatures()
    {
        DateTime now = DateTime.Now;

        try
        {
            TemperatureProbeResult raw = _temperatureProbe.Read();
            IReadOnlyList<TemperatureReading> readings = raw.Status == SensorStatus.Ok
                ? raw.Readings
                    .Select(reading => new TemperatureReading
                    {
                        Status = SensorStatus.Ok,
                        Name = reading.Name,
                        Celsius = reading.Celsius,
                        Source = reading.Source,
                        SampledAt = now,
                    })
                    .ToList()
                : new[]
                {
                    new TemperatureReading
                    {
                        Status = raw.Status,
                        Reason = raw.Reason,
                        Name = TemperatureText.SystemZoneName,
                        Source = TemperatureText.SystemZoneSource,
                        SampledAt = now,
                    },
                };

            string summary = readings.Count > 0 && readings[0].Celsius is { } celsius
                ? $"{readings[0].Name}: {celsius:0.0} °C ({readings[0].Source})"
                : $"{raw.Status}: {raw.Reason}";

            if (_temperatures.Count == 0 || Changed(_temperatures, readings))
            {
                _logger.Info($"Temperature: {summary}.");
            }

            return readings;
        }
        catch (Exception ex)
        {
            _logger.Error("Temperature sample failed.", ex);
            return new[]
            {
                new TemperatureReading
                {
                    Status = SensorStatus.Error,
                    Reason = "Temperature sample failed",
                    Name = TemperatureText.SystemZoneName,
                    SampledAt = now,
                },
            };
        }
    }

    private static bool Changed(IReadOnlyList<TemperatureReading> previous, IReadOnlyList<TemperatureReading> next)
    {
        if (previous.Count != next.Count)
        {
            return true;
        }

        for (int i = 0; i < previous.Count; i++)
        {
            if (previous[i].Status != next[i].Status ||
                previous[i].Celsius != next[i].Celsius ||
                previous[i].Reason != next[i].Reason)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Changed(BatteryReading previous, BatteryReading next) =>
        previous.Status != next.Status
        || previous.Percent != next.Percent
        || previous.PowerLine != next.PowerLine
        || previous.Charging != next.Charging
        || previous.TimeRemaining != next.TimeRemaining
        || previous.HealthPercent != next.HealthPercent;

    internal static BatteryReading Build(BatteryProbeResult raw, DateTime sampledAt)
    {
        if (!raw.HasBattery)
        {
            return new BatteryReading
            {
                Status = SensorStatus.Unavailable,
                Reason = BatteryText.NoBattery,
                HasBattery = false,
                PowerLine = BatteryPowerLine.Unknown,
                Source = BatteryText.PowerStatusSource,
                SampledAt = sampledAt,
            };
        }

        BatteryPowerLine line = raw.AcOnline switch
        {
            true => BatteryPowerLine.Online,
            false => BatteryPowerLine.Offline,
            null => BatteryPowerLine.Unknown,
        };

        if (raw.Percent is null && raw.LifeRemaining is null && line == BatteryPowerLine.Unknown)
        {
            return new BatteryReading
            {
                Status = SensorStatus.Unavailable,
                Reason = BatteryText.UnknownReason,
                HasBattery = true,
                PowerLine = line,
                Source = BatteryText.PowerStatusSource,
                SampledAt = sampledAt,
            };
        }

        TimeSpan? time = raw.LifeRemaining;
        bool estimated = false;

        if (time is null && raw.RateMilliwatts is > 0)
        {
            uint rate = raw.RateMilliwatts.Value;

            if (raw.Charging && raw.FullChargedMilliwattHours is { } full &&
                raw.RemainingMilliwattHours is { } remaining && full > remaining)
            {
                time = TimeSpan.FromHours((full - remaining) / (double)rate);
                estimated = true;
            }
            else if (!raw.Charging && line == BatteryPowerLine.Offline &&
                     raw.RemainingMilliwattHours is { } capacity && capacity > 0)
            {
                time = TimeSpan.FromHours(capacity / (double)rate);
                estimated = true;
            }
        }

        double? health = raw.DesignedMilliwattHours is > 0 && raw.FullChargedMilliwattHours is { } charged
            ? Math.Clamp(charged * 100.0 / raw.DesignedMilliwattHours.Value, 0, 100)
            : null;

        return new BatteryReading
        {
            Status = SensorStatus.Ok,
            HasBattery = true,
            Percent = raw.Percent,
            PowerLine = line,
            Charging = raw.Charging,
            TimeRemaining = time,
            TimeEstimated = estimated,
            HealthPercent = health,
            Source = raw.DesignedMilliwattHours is null && raw.FullChargedMilliwattHours is null
                ? BatteryText.PowerStatusSource
                : BatteryText.WmiSource,
            SampledAt = sampledAt,
        };
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
