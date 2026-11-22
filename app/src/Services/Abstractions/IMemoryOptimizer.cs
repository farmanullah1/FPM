using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Physical memory measurements and working-set trimming.</summary>
public interface IMemoryOptimizer
{
    /// <summary>Reads GlobalMemoryStatusEx.</summary>
    MemorySnapshot GetSnapshot();

    /// <summary>Trims every trimmable process working set and reports the difference.</summary>
    MemorySnapshot Optimize();
}
