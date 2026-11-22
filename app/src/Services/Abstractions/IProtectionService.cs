using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Windows Defender and Windows Update status cards.</summary>
public interface IProtectionService
{
    DefenderStatus GetDefenderStatus();
    UpdateStatus GetUpdateStatus();
}
