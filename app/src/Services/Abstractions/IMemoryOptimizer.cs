using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Memory readings and working-set trimming.</summary>
public interface IMemoryOptimizer
{
    /// <summary>Current physical memory figures (fast, safe to poll from a timer).</summary>
    MemorySnapshot GetSnapshot();

    /// <summary>
    /// Asks Windows to trim the working set of every eligible process and reports the before
    /// and after memory readings plus the signed available-memory delta.
    /// </summary>
    MemoryTrimResult Optimize();
}
