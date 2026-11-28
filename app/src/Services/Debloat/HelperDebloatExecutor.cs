using PCManager.Core.Debloat;
using PCManager.Core.Elevation;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Debloat;

/// <summary>Every debloat write and read-state call goes through the typed helper contract.</summary>
public sealed class HelperDebloatExecutor : IDebloatExecutor
{
    private readonly IElevationService _elevation;

    public HelperDebloatExecutor(IElevationService elevation) => _elevation = elevation;

    public Task<DebloatHelperOutcome> DetectAsync(
        DebloatTweak tweak,
        string targetUser = "",
        CancellationToken cancellationToken = default) =>
        _elevation.RunDebloatAsync(
            HelperCommand.DetectTweak, tweak.Id, targetUser: targetUser, cancellationToken: cancellationToken);

    public Task<DebloatHelperOutcome> ApplyAsync(
        DebloatTweak tweak,
        string targetUser = "",
        CancellationToken cancellationToken = default) =>
        _elevation.RunDebloatAsync(
            HelperCommand.ApplyTweak, tweak.Id, targetUser: targetUser, cancellationToken: cancellationToken);

    public Task<DebloatHelperOutcome> RevertAsync(
        DebloatTweak tweak,
        IReadOnlyList<RegistryValueState> before,
        string targetUser = "",
        CancellationToken cancellationToken = default) =>
        _elevation.RunDebloatAsync(
            HelperCommand.RevertTweak, tweak.Id, before, targetUser, cancellationToken);
}
