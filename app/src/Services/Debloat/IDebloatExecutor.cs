using PCManager.Core.Debloat;

namespace PCManager.Services.Debloat;

/// <summary>
/// Execution seam. The real implementation is the elevated helper; tests substitute a sandbox
/// that mutates an in-memory registry so no test touches the machine's registry.
/// </summary>
public interface IDebloatExecutor
{
    Task<DebloatHelperOutcome> DetectAsync(DebloatTweak tweak, CancellationToken cancellationToken = default);

    Task<DebloatHelperOutcome> ApplyAsync(DebloatTweak tweak, CancellationToken cancellationToken = default);

    Task<DebloatHelperOutcome> RevertAsync(
        DebloatTweak tweak,
        IReadOnlyList<RegistryValueState> before,
        CancellationToken cancellationToken = default);
}
