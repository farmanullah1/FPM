using Xunit;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Sensors;

namespace PCManager.Tests;

/// <summary>
/// T2: conversions, thresholds, the honest unavailable/permission states and the shared sampler.
/// The presence path is exercised with recorded-style fixtures because this machine denies ACPI
/// thermal-zone access to standard users.
/// </summary>
public sealed class TemperatureSensorTests
{
    [Fact]
    public void AcpiTenthsKelvinConvertToCelsius()
    {
        // ACPI reports tenths of Kelvin: 3000 = 300.0 K = 26.85 °C.
        Assert.Equal(26.85, WindowsTemperatureProbe.FromTenthsKelvin(3000), 2);
        Assert.Equal(46.85, WindowsTemperatureProbe.FromTenthsKelvin(3200), 2);
    }

    [Fact]
    public void FormatsCelsiusAndFahrenheitFromOneValue()
    {
        Assert.Equal("62 °C", TemperatureText.Format(62.4, TemperatureUnit.Celsius));
        Assert.Equal("144 °F", TemperatureText.Format(62.4, TemperatureUnit.Fahrenheit));
        Assert.Equal(TemperatureText.Unavailable, TemperatureText.Format(null, TemperatureUnit.Celsius));
    }

    [Theory]
    [InlineData(69.9, TemperatureSeverity.Normal)]
    [InlineData(70, TemperatureSeverity.Warm)]
    [InlineData(84.9, TemperatureSeverity.Warm)]
    [InlineData(85, TemperatureSeverity.Hot)]
    public void ThresholdsMatchTheCard(double celsius, TemperatureSeverity expected)
        => Assert.Equal(expected, TemperatureText.Severity(celsius));

    [Fact]
    public void PresentZoneBecomesATypedReading()
    {
        using var service = Service(new TemperatureProbeResult
        {
            Status = SensorStatus.Ok,
            Readings = new[]
            {
                new TemperatureProbeReading(
                    TemperatureText.SystemZoneName, 46.85, TemperatureText.SystemZoneSource),
            },
        });

        TemperatureReading reading = service.Temperatures[0];

        Assert.Equal(SensorStatus.Ok, reading.Status);
        Assert.Equal(46.85, reading.Celsius);
        Assert.Equal(TemperatureText.SystemZoneName, reading.Name);
        Assert.DoesNotContain("CPU", reading.Name);
    }

    [Fact]
    public void AccessDeniedKeepsNeedsPermissionAndTheReason()
    {
        using var service = Service(new TemperatureProbeResult
        {
            Status = SensorStatus.NeedsPermission,
            Reason = TemperatureText.NeedsAdminReason,
        });

        TemperatureReading reading = service.Temperatures[0];

        Assert.Equal(SensorStatus.NeedsPermission, reading.Status);
        Assert.Equal(TemperatureText.NeedsAdminReason, reading.Reason);
        Assert.Null(reading.Celsius);
    }

    [Fact]
    public void NoZoneIsUnavailableNotZero()
    {
        using var service = Service(new TemperatureProbeResult
        {
            Status = SensorStatus.Unavailable,
            Reason = TemperatureText.NoZoneReason,
        });

        Assert.Equal(SensorStatus.Unavailable, service.Temperatures[0].Status);
        Assert.Null(service.Temperatures[0].Celsius);
    }

    [Fact]
    public void RefreshRaisesTemperatureChanged()
    {
        using var service = Service(new TemperatureProbeResult
        {
            Status = SensorStatus.Ok,
            Readings = new[]
            {
                new TemperatureProbeReading(TemperatureText.GpuName, 64, TemperatureText.NvmlSource),
            },
        });
        IReadOnlyList<TemperatureReading>? seen = null;
        service.TemperatureChanged += (_, readings) => seen = readings;

        service.Refresh();

        Assert.NotNull(seen);
        Assert.Equal(64, seen![0].Celsius);
    }

    private static SensorService Service(TemperatureProbeResult result) =>
        new(new NoBatteryProbe(), new FakeTemperatureProbe(result), new TestLogger());

    private sealed class NoBatteryProbe : IBatteryProbe
    {
        public BatteryProbeResult Read() => new() { HasBattery = false };
    }

    private sealed class FakeTemperatureProbe(TemperatureProbeResult result) : ITemperatureProbe
    {
        public TemperatureProbeResult Read() => result;
    }
}
