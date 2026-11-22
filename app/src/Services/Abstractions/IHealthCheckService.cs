using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// One isolated system scan. Add a new health check by implementing this interface
/// and registering it in <c>ServiceCollectionExtensions</c>.
/// </summary>
public interface IHealthCheckModule
{
    /// <summary>Stable identifier, also written into <see cref="HealthCheckItem.ModuleId"/>.</summary>
    string Id { get; }

    /// <summary>Human readable name shown in the scan progress line.</summary>
    string Title { get; }

    /// <summary>
    /// Scans the machine and appends the resulting rows to <paramref name="items"/>.
    /// Implementations should never throw for expected conditions (access denied,
    /// missing folders); they should report the condition as a row instead.
    /// </summary>
    Task ScanAsync(IList<HealthCheckItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the repair for one row. Returns <c>true</c> when the problem was
    /// resolved, and writes stats back into <paramref name="item"/> (bytes freed).
    /// </summary>
    Task<bool> FixAsync(HealthCheckItem item, CancellationToken cancellationToken = default);
}

/// <summary>Runs every registered <see cref="IHealthCheckModule"/> and applies fixes.</summary>
public interface IHealthCheckService
{
    Task<HealthCheckSummary> ScanAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Repairs the selected, fixable rows of a previous scan and returns how many
    /// bytes were released.
    /// </summary>
    Task<long> FixAsync(HealthCheckSummary summary, CancellationToken cancellationToken = default);
}
