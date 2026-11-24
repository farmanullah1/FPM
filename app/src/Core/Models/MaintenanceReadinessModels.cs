namespace PCManager.Core.Models;

/// <summary>
/// Status of an individual system maintenance readiness check.
/// </summary>
public sealed record ReadinessCheckItem(
    string Name,
    string Category,
    bool Passed,
    string Details);

/// <summary>
/// Aggregate result of the FPM maintenance readiness diagnostics.
/// </summary>
public sealed record MaintenanceReadinessResult(
    bool IsReady,
    IReadOnlyList<ReadinessCheckItem> Checks,
    string Summary);
