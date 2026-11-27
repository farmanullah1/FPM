using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// One shared battery sampler (T1): a single timer for the whole app, 5 s while a surface is
/// visible and 30 s in the background. Readings are typed; absence is reported, never faked.
/// </summary>
public interface ISensorService : IDisposable
{
    BatteryReading Battery { get; }

    /// <summary>T2: zero or more temperature readings; an unavailable state is one reading with the reason.</summary>
    IReadOnlyList<TemperatureReading> Temperatures { get; }

    event EventHandler<BatteryReading>? BatteryChanged;

    event EventHandler<IReadOnlyList<TemperatureReading>>? TemperatureChanged;

    /// <summary>True while at least one surface displays the battery (5 s cadence).</summary>
    void SetDemand(bool visible);

    /// <summary>Take a sample now and raise <see cref="BatteryChanged"/>.</summary>
    void Refresh();
}
