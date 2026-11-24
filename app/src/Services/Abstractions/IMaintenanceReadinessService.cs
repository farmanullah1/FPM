using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Verifies application maintenance readiness: helper executable presence, log writability,
/// rule-pack validity, and storage thresholds. Read-only diagnostic.
/// </summary>
public interface IMaintenanceReadinessService
{
    Task<MaintenanceReadinessResult> CheckReadinessAsync(CancellationToken cancellationToken = default);
}
