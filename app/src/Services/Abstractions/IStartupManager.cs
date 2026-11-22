using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Reads and toggles startup entries from Run keys, Startup folders and Task Scheduler.</summary>
public interface IStartupManager
{
    IReadOnlyList<StartupApp> GetStartupApps();
    OperationResult SetEnabled(StartupApp app, bool enabled);
}
