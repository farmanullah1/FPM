using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Thin wrapper for the shared sensor service's temperature readings (T2). Home, the toolbar and
/// Settings bind to this one object; the ACPI zone is labelled as a system zone, never "CPU".
/// </summary>
public sealed partial class TemperatureStatusViewModel : ObservableObject, IDisposable
{
    private readonly ISensorService _sensors;
    private readonly ISettingsService _settings;

    private IReadOnlyList<TemperatureReading> _readings;

    public TemperatureStatusViewModel(ISensorService sensors, ISettingsService settings)
    {
        _sensors = sensors;
        _settings = settings;
        _readings = sensors.Temperatures;
        _sensors.TemperatureChanged += OnTemperatureChanged;
    }

    public TemperatureReading Primary => _readings.Count > 0 ? _readings[0] : new TemperatureReading();

    public bool IsAvailable => Primary.Status == SensorStatus.Ok && Primary.Celsius is not null;

    public string ValueText => TemperatureText.Format(Primary.Celsius, Unit);

    public string Label => Primary.Name.Length > 0 ? Primary.Name : TemperatureText.SystemZoneName;

    public string StatusText => IsAvailable
        ? TemperatureText.SeverityLabel(Severity)
        : Primary.Reason.Length > 0 ? Primary.Reason : TemperatureText.Unavailable;

    public TemperatureSeverity Severity => TemperatureText.Severity(Primary.Celsius);

    public string SampledText => TemperatureText.SampledAt(Primary.SampledAt);

    public string AutomationName => $"Temperature {ValueText}, {Label}";

    public string Tooltip =>
        $"{Label}\n{ValueText} · {StatusText}\nSource: {Primary.Source}\n{SampledText}\n" +
        TemperatureText.CpuNotClaimed;

    private TemperatureUnit Unit => _settings.Current.TemperatureUnit;

    /// <summary>Called when the °C/°F setting changes; the next sample follows the same unit.</summary>
    public void RefreshUnits()
    {
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(AutomationName));
        OnPropertyChanged(nameof(Tooltip));
    }

    private void OnTemperatureChanged(object? sender, IReadOnlyList<TemperatureReading> readings)
    {
        void Apply()
        {
            _readings = readings;
            OnPropertyChanged(nameof(Primary));
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(ValueText));
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(Severity));
            OnPropertyChanged(nameof(SampledText));
            OnPropertyChanged(nameof(AutomationName));
            OnPropertyChanged(nameof(Tooltip));
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Apply);
            return;
        }

        Apply();
    }

    public void Dispose() => _sensors.TemperatureChanged -= OnTemperatureChanged;
}
