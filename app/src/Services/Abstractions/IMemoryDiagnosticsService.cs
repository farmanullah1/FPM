using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Provides read-only memory diagnostics without arbitrary RAM cleaning claims.
/// </summary>
public interface IMemoryDiagnosticsService
{
    /// <summary>
    /// Gets a detailed snapshot of system memory, commit charge, and top memory consumers.
    /// </summary>
    ExtendedMemorySnapshot GetExtendedSnapshot();
}

/// <summary>
/// Provides safe application closure for user-selected processes with graceful close first.
/// Never kills system, security, or protected processes.
/// </summary>
public interface IMemoryReliefService
{
    /// <summary>
    /// Inspects and returns user applications that are safe to propose for closing.
    /// </summary>
    IReadOnlyList<ProcessMemoryItem> GetEligibleProcesses();

    /// <summary>
    /// Executes a user-approved plan to close selected applications safely.
    /// </summary>
    Task<MemoryReliefResult> ExecuteReliefPlanAsync(MemoryReliefPlan plan, CancellationToken cancellationToken = default);
}
