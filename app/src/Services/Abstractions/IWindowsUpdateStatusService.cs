using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>wuauserv state, pending-reboot flags and recently installed hotfixes.</summary>
public interface IWindowsUpdateStatusService
{
    UpdateStatus GetStatus();
}
