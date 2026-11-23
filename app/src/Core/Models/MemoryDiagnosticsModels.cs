namespace PCManager.Core.Models;

public enum MemoryPressureLevel
{
    Healthy,
    Moderate,
    High
}

public sealed record ExtendedMemorySnapshot(
    ulong TotalPhysicalBytes,
    ulong AvailableBytes,
    uint LoadPercent,
    ulong CommittedBytes,
    ulong CommitLimitBytes,
    MemoryPressureLevel PressureLevel,
    DateTime Timestamp,
    IReadOnlyList<ProcessMemoryItem> TopConsumers);

public sealed record ProcessMemoryItem(
    int ProcessId,
    string ProcessName,
    long WorkingSetBytes,
    long PrivateBytes,
    bool IsProtected,
    bool CanRelieve);

public sealed record MemoryReliefPlan(
    IReadOnlyList<int> ProcessIdsToClose,
    bool GracefulCloseFirst = true);

public sealed record MemoryReliefResult(
    int Attempted,
    int Succeeded,
    int Failed,
    long EstimatedBytesFreed,
    IReadOnlyList<string> ClosedProcesses,
    string Summary);
