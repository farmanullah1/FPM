using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Lists and launches taskbar-pinned shortcuts.</summary>
public interface ITaskbarPinService
{
    IReadOnlyList<TaskbarPinEntry> GetPinnedApps();
    OperationResult OpenTaskbarSettings();
}
