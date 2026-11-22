using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>PC Boost: live memory figures, working-set trimming and temp purge.</summary>
public interface IPcBoostService
{
    /// <summary>Current memory snapshot (fast, safe to poll from a timer).</summary>
    MemorySnapshot GetMemorySnapshot();

    /// <summary>
    /// Trims the working set of every non-system process with
    /// <c>SetProcessWorkingSetSizeEx(h, -1, -1)</c> and returns the resulting snapshot,
    /// including how many working sets were trimmed and how much memory came back.
    /// </summary>
    Task<MemorySnapshot> OptimizeAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes temporary files older than 48 hours, returns bytes freed.</summary>
    Task<long> CleanTempFilesAsync(CancellationToken cancellationToken = default);
}
