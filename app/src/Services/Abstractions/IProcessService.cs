using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Process list, End Task and startup-program toggles.</summary>
public interface IProcessService
{
    /// <summary>
    /// Returns all readable processes. CPU percentages are measured against the
    /// previous call, so the first call after start-up reports 0 for CPU.
    /// </summary>
    Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default);

    /// <summary>Terminates one process (never a session-0/critical process).</summary>
    Task<OperationResult> EndProcessAsync(int processId, CancellationToken cancellationToken = default);

    /// <summary>Reads Run keys, Startup folders and Task Scheduler tasks.</summary>
    Task<IReadOnlyList<StartupApp>> GetStartupAppsAsync(CancellationToken cancellationToken = default);

    /// <summary>Enables or disables one startup entry.</summary>
    Task<OperationResult> SetStartupAppEnabledAsync(StartupApp app, bool enabled, CancellationToken cancellationToken = default);
}
