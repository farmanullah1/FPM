using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Registers this application to start with Windows.
///
/// The value name is "FarmanullahPCManager" under
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run, so it can never overwrite or be
/// overwritten by Microsoft PC Manager's own startup entry.
/// </summary>
public interface IAppStartupService
{
    bool IsEnabled();

    OperationResult SetEnabled(bool enabled);
}
