using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Thin presentation wrapper for the shared sensor service (T1). Home, the floating toolbar and
/// Settings all bind to this one object, so there is exactly one subscription and one cadence.
/// </summary>
public sealed partial class BatteryStatusViewModel : ObservableObject, IDisposable
{
    private readonly ISensorService _sensors;
    private readonly HashSet<string> _owners = new(StringComparer.Ordinal);
    private readonly object _ownerSync = new();

    public BatteryStatusViewModel(ISensorService sensors)
    {
        _sensors = sensors;
        _reading = sensors.Battery;
        _sensors.BatteryChanged += OnBatteryChanged;
    }

    [ObservableProperty]
    private BatteryReading _reading;

    partial void OnReadingChanged(BatteryReading value) => RaiseDerived();

    public bool HasBattery => Reading.HasBattery;

    public string PercentText => BatteryText.Percent(Reading.Percent);

    public string DetailText => BatteryText.Detail(Reading);

    public string TimeText => Reading.Status switch
    {
        SensorStatus.Ok => Reading.PowerLine switch
        {
            BatteryPowerLine.Online when Reading.Charging =>
                BatteryText.Time(Reading.TimeRemaining, Reading.TimeEstimated),
            BatteryPowerLine.Online when Reading.Percent >= 100 => BatteryText.FullyCharged,
            BatteryPowerLine.Online => BatteryText.OnAc,
            _ => BatteryText.Time(Reading.TimeRemaining, Reading.TimeEstimated),
        },
        _ => BatteryText.Unavailable,
    };

    public string HealthText => BatteryText.Health(Reading.HealthPercent);

    public string HealthBadgeText => $"{BatteryText.HealthLabel} {HealthText}";

    public string SampledText => BatteryText.SampledAt(Reading.SampledAt);

    public string Tooltip =>
        $"{DetailText}\nTime: {TimeText}\n{BatteryText.HealthLabel}: {HealthText}\n" +
        $"Source: {Reading.Source}\n{SampledText}";

    public string AutomationName => $"Battery {PercentText}, {DetailText}";

    /// <summary>Registers a surface; the shared sampler runs at 5 s while any owner is visible.</summary>
    public void SetVisible(string owner, bool visible)
    {
        lock (_ownerSync)
        {
            if (visible)
            {
                _owners.Add(owner);
            }
            else
            {
                _owners.Remove(owner);
            }

            _sensors.SetDemand(_owners.Count > 0);
        }
    }

    private void OnBatteryChanged(object? sender, BatteryReading reading)
    {
        void Apply() => Reading = reading;

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Apply);
            return;
        }

        Apply();
    }

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(HasBattery));
        OnPropertyChanged(nameof(PercentText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(HealthText));
        OnPropertyChanged(nameof(HealthBadgeText));
        OnPropertyChanged(nameof(SampledText));
        OnPropertyChanged(nameof(Tooltip));
        OnPropertyChanged(nameof(AutomationName));
    }

    public void Dispose() => _sensors.BatteryChanged -= OnBatteryChanged;
}
