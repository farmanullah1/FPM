using PCManager.Core.Debloat;
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
        IReadOnlyList<string>? domains = null,
        CancellationToken cancellationToken = default);

    /// <summary>One typed debloat command; ids and restore values are validated by the helper.</summary>
    Task<DebloatHelperOutcome> RunDebloatAsync(
        HelperCommand command,
        string tweakId,
        IReadOnlyList<RegistryValueState>? restore = null,
        string targetUser = "",
        CancellationToken cancellationToken = default);
}

/// <summary>Offers a System Restore checkpoint before destructive cleanup.</summary>
public interface IRestorePointService
{
    Task<ElevationOutcome> CreateAsync(CancellationToken cancellationToken = default);
}
