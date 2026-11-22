using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>PC Boost: live memory figures, working-set trimming and temp purge.</summary>
public interface IPcBoostService
{
    /// <summary>Current memory snapshot (fast, safe to poll from a timer).</summary>
    MemorySnapshot GetMemorySnapshot();

    /// <summary>
    /// Trims the working set of every non-system process with
    /// <c>SetProcessWorkingSetSizeEx(h, -1, -1)</c>. The result carries the before/after
    /// memory readings and the signed available-memory delta — trimming does not "free"
    /// memory, so no reclaimed-bytes figure is reported.
    /// </summary>
    Task<MemoryTrimResult> OptimizeAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes temporary files older than 48 hours and returns the bytes actually removed.</summary>
    Task<long> CleanTempFilesAsync(CancellationToken cancellationToken = default);
}
