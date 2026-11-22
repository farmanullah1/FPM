using PCManager.Core.Elevation;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Launches the requireAdministrator helper on demand and exchanges one typed command over a
/// current-user-only named pipe. A declined UAC prompt is reported, never retried in a loop.
/// </summary>
public interface IElevationService
{
    bool IsHelperAvailable { get; }

    Task<ElevationOutcome> RunAsync(
        HelperCommand command,
        string? ruleId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Offers a System Restore checkpoint before destructive cleanup.</summary>
public interface IRestorePointService
{
    Task<ElevationOutcome> CreateAsync(CancellationToken cancellationToken = default);
}
