using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Sensors;

/// <summary>
/// One timer for every battery surface (T1). The service owns the sample and the derived labels'
/// inputs; the probe is the only Windows-specific part, so the decision logic is unit-tested.
/// </summary>
public sealed class BatterySensorService : ISensorService
{
    private readonly IBatteryProbe _probe;
    private readonly IAppLogger _logger;
    private readonly object _sync = new();

    private BatteryReading? _battery;
    private Timer? _timer;
    private bool _sampled;
    private bool _disposed;

    public BatterySensorService(IBatteryProbe probe, IAppLogger logger)
    {
        _probe = probe;
        _logger = logger;
    }

    public event EventHandler<BatteryReading>? BatteryChanged;

    public BatteryReading Battery
    {
        get
        {
            lock (_sync)
            {
                if (_battery is null)
                {
                    _battery = Sample();
                    _sampled = true;
                }

                return _battery;
            }
        }
    }

    public void Refresh()
    {
        BatteryReading reading;

        lock (_sync)
        {
            reading = Sample();
            _battery = reading;
            _sampled = true;
        }

        BatteryChanged?.Invoke(this, reading);
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

            if (_sampled)
            {
                return;
            }
        }
    }

    private BatteryReading Sample()
    {
        try
        {
            BatteryReading reading = Build(_probe.Read(), DateTime.Now);

            if (!_sampled || _battery is null || Changed(_battery, reading))
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
