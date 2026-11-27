using Xunit;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Sensors;

namespace PCManager.Tests;

/// <summary>T1: the battery decision logic, exercised through the real service with a fake probe.</summary>
public sealed class BatterySensorTests
{
    [Fact]
    public void NoBatteryReportsUnavailableAndNoTile()
    {
        using var service = Service(new BatteryProbeResult { HasBattery = false });

        BatteryReading reading = service.Battery;

        Assert.Equal(SensorStatus.Unavailable, reading.Status);
        Assert.False(reading.HasBattery);
        Assert.Equal(BatteryText.NoBattery, reading.Reason);
        Assert.Null(reading.Percent);
    }

    [Fact]
    public void AcPowerKeepsPercentAndSaysCalculatingUntilWindowsKnows()
    {
        using var service = Service(new BatteryProbeResult
        {
            HasBattery = true,
            AcOnline = true,
            Percent = 87,
        });

        BatteryReading reading = service.Battery;

        Assert.Equal(SensorStatus.Ok, reading.Status);
        Assert.Equal(87, reading.Percent);
        Assert.Equal(BatteryPowerLine.Online, reading.PowerLine);
        Assert.Equal(BatteryText.OnAc, BatteryText.Detail(reading));
        Assert.Null(reading.TimeRemaining);
    }

    [Fact]
    public void DischargeUsesTheOsTimeWhenItIsKnown()
    {
        using var service = Service(new BatteryProbeResult
        {
            HasBattery = true,
            AcOnline = false,
            Percent = 47,
            LifeRemaining = TimeSpan.FromMinutes(130),
        });

        BatteryReading reading = service.Battery;

        Assert.Equal(TimeSpan.FromMinutes(130), reading.TimeRemaining);
        Assert.False(reading.TimeEstimated);
        Assert.Equal("2 h 10 m", BatteryText.Time(reading.TimeRemaining, reading.TimeEstimated));
    }

    [Fact]
    public void UnknownTimeIsEstimatedFromRateAndLabelled()
    {
        using var service = Service(new BatteryProbeResult
        {
            HasBattery = true,
            AcOnline = false,
            Percent = 50,
            RateMilliwatts = 10000,
            RemainingMilliwattHours = 20000,
        });

        BatteryReading reading = service.Battery;

        Assert.True(reading.TimeEstimated);
        Assert.Equal(TimeSpan.FromHours(2), reading.TimeRemaining);
        Assert.Equal("2 h 00 m (estimated)", BatteryText.Time(reading.TimeRemaining, reading.TimeEstimated));
    }

    [Fact]
    public void ChargingEstimatesTimeToFullFromCapacityAndRate()
    {
        using var service = Service(new BatteryProbeResult
        {
            HasBattery = true,
            AcOnline = true,
            Charging = true,
            Percent = 50,
            RateMilliwatts = 15000,
            RemainingMilliwattHours = 15000,
            FullChargedMilliwattHours = 30000,
        });

        BatteryReading reading = service.Battery;

        Assert.True(reading.Charging);
        Assert.True(reading.TimeEstimated);
        Assert.Equal(TimeSpan.FromHours(1), reading.TimeRemaining);
    }

    [Fact]
    public void HealthComesFromDesignAndFullCapacity()
    {
        using var service = Service(new BatteryProbeResult
        {
            HasBattery = true,
            AcOnline = true,
            Percent = 100,
            DesignedMilliwattHours = 40000,
            FullChargedMilliwattHours = 30844,
        });

        BatteryReading reading = service.Battery;

        Assert.NotNull(reading.HealthPercent);
        Assert.Equal(77.11, reading.HealthPercent!.Value, 2);
    }

    [Fact]
    public void MissingEverythingIsUnavailableNotZero()
    {
        using var service = Service(new BatteryProbeResult { HasBattery = true });

        BatteryReading reading = service.Battery;

        Assert.Equal(SensorStatus.Unavailable, reading.Status);
        Assert.Equal(BatteryText.UnknownReason, reading.Reason);
        Assert.Null(reading.Percent);
    }

    [Fact]
    public void RefreshRaisesTheChangeEvent()
    {
        var probe = new FakeProbe(new BatteryProbeResult
        {
            HasBattery = true,
            AcOnline = true,
            Percent = 80,
        });
        using var service = new SensorService(probe, new NoTemperatureProbe(), new TestLogger());
        BatteryReading? seen = null;
        service.BatteryChanged += (_, reading) => seen = reading;

        service.Refresh();

        Assert.NotNull(seen);
        Assert.Equal(80, seen!.Percent);
        Assert.Equal(1, probe.Reads);
    }

    private static SensorService Service(BatteryProbeResult result) =>
        new(new FakeProbe(result), new NoTemperatureProbe(), new TestLogger());

    private sealed class NoTemperatureProbe : ITemperatureProbe
    {
        public TemperatureProbeResult Read() => new()
        {
            Status = SensorStatus.Unavailable,
            Reason = TemperatureText.NoZoneReason,
        };
    }

    private sealed class FakeProbe(BatteryProbeResult result) : IBatteryProbe
    {
        public int Reads { get; private set; }

        public BatteryProbeResult Read()
        {
            Reads++;
            return result;
        }
    }
}
