using System.Diagnostics;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Toolbox;

/// <summary>
/// Protection page actions.
///
/// Restarting Explorer is the supported way to rebuild a broken taskbar: Windows recreates
/// explorer.exe (shell, taskbar, Start) automatically, so the user's open windows survive.
/// </summary>
public sealed class SystemRepairService : ISystemRepairService
{
    private readonly IToolLauncher _launcher;

    public SystemRepairService(IToolLauncher launcher) => _launcher = launcher;

    public OperationResult OpenNotificationSettings()
    {
        try
        {
            _launcher.OpenUri("ms-settings:notifications");
            return OperationResult.Ok("Opened notification and pop-up settings.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult OpenDefaultAppsSettings()
    {
        try
        {
            _launcher.OpenUri("ms-settings:defaultapps");
            return OperationResult.Ok("Opened Default apps settings.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult OpenWindowsUpdateSettings()
    {
        try
        {
            _launcher.OpenUri("ms-settings:windowsupdate");
            return OperationResult.Ok("Opened Windows Update.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult OpenAppsFeatures()
    {
        try
        {
            _launcher.OpenUri("ms-settings:appsfeatures");
            return OperationResult.Ok("Opened Installed apps.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult RestartExplorer()
    {
        try
        {
            // /f closes the shell immediately; Windows starts it again on demand.
            ProcessResult kill = ProcessRunner.Run("taskkill.exe", "/f /im explorer.exe", TimeSpan.FromSeconds(10));

            if (!kill.Success && !kill.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail(kill.Message);
            }

            // Starting explorer.exe directly also works when the shell did not auto-restart.
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Explorer usually restarts on its own; a failure here is not fatal.
            }

            return OperationResult.Ok("Explorer (taskbar and Start) restarted.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}
