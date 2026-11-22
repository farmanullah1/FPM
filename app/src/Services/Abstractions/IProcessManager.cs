using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Process enumeration with CPU deltas, plus End Task.</summary>
public interface IProcessManager
{
    IReadOnlyList<ProcessInfo> GetProcesses();
    OperationResult EndProcess(int processId);
}
