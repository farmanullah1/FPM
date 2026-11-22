using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Protection page actions: notification (pop-up) settings, default-app pages and a
/// real Explorer restart that rebuilds the taskbar when it stops responding.
/// </summary>
public interface ISystemRepairService
{
    OperationResult OpenNotificationSettings();

    OperationResult OpenDefaultAppsSettings();

    OperationResult OpenWindowsUpdateSettings();

    OperationResult OpenAppsFeatures();

    OperationResult RestartExplorer();
}
