using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Processes;

/// <summary>
/// Facade used by the UI. All heavy work runs on the thread pool so the WPF
/// dispatcher stays responsive.
/// </summary>
public sealed class ProcessService : IProcessService
{
    private readonly IProcessManager _processManager;
    private readonly IStartupManager _startupManager;

    public ProcessService(IProcessManager processManager, IStartupManager startupManager)
    {
        _processManager = processManager;
        _startupManager = startupManager;
    }

    public Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default)
        => Task.Run(_processManager.GetProcesses, cancellationToken);

    public Task<OperationResult> EndProcessAsync(int processId, CancellationToken cancellationToken = default)
        => Task.Run(() => _processManager.EndProcess(processId), cancellationToken);

    public Task<IReadOnlyList<StartupApp>> GetStartupAppsAsync(CancellationToken cancellationToken = default)
        => Task.Run(_startupManager.GetStartupApps, cancellationToken);

    public Task<OperationResult> SetStartupAppEnabledAsync(StartupApp app, bool enabled, CancellationToken cancellationToken = default)
        => Task.Run(() => _startupManager.SetEnabled(app, enabled), cancellationToken);
}
