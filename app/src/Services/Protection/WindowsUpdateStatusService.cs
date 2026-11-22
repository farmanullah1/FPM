using System.Management;
using System.ServiceProcess;
using Microsoft.Win32;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Protection;

/// <summary>
/// Windows Update status:
///  - wuauserv state through ServiceController,
///  - pending-reboot flags from the three registry locations Windows uses,
///  - recently installed hotfixes from Win32_QuickFixEngineering.
/// </summary>
public sealed class WindowsUpdateStatusService : IWindowsUpdateStatusService
{
    private const string CbsRebootPending =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending";

    private const string WuRebootRequired =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired";

    private const string SessionManager =
        @"SYSTEM\CurrentControlSet\Control\Session Manager";

    public UpdateStatus GetStatus()
    {
        var status = new UpdateStatus
        {
            UpdateServiceRunning = IsUpdateServiceRunning(),
            PendingReboot = IsRebootPending()
        };

        status.StatusText = status.PendingReboot
            ? "A restart is required to finish installing updates."
            : status.UpdateServiceRunning
                ? "Windows Update is running normally."
                : "The Windows Update service (wuauserv) is stopped.";

        return status;
    }

    private static bool IsUpdateServiceRunning()
    {
        try
        {
            using var controller = new ServiceController("wuauserv");
            return controller.Status == ServiceControllerStatus.Running;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsRebootPending()
    {
        try
        {
            using (RegistryKey? cbs = Registry.LocalMachine.OpenSubKey(CbsRebootPending))
            {
                if (cbs is not null)
                {
                    return true;
                }
            }

            using (RegistryKey? wu = Registry.LocalMachine.OpenSubKey(WuRebootRequired))
            {
                if (wu is not null)
                {
                    return true;
                }
            }

            // PendingFileRenameOperations is written by installers that must replace
            // in-use files on the next boot.
            using RegistryKey? session = Registry.LocalMachine.OpenSubKey(SessionManager);
            if (session?.GetValue("PendingFileRenameOperations") is string[] { Length: > 0 })
            {
                return true;
            }
        }
        catch (Exception) { }

        return false;
    }
}
