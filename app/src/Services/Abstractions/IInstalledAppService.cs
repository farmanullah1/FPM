using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Reads installed Win32 applications from the registry, starts their uninstallers and waits
/// (bounded) for the Uninstall entry to disappear before reporting the measured leftovers.
/// </summary>
public interface IInstalledAppService
{
    Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> UninstallAsync(InstalledApp app, CancellationToken cancellationToken = default);
}
