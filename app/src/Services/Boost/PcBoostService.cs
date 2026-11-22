using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Boost;

/// <summary>PC Boost facade: memory snapshots, working-set trim and temp purge.</summary>
public sealed class PcBoostService : IPcBoostService
{
    private readonly IMemoryOptimizer _optimizer;
    private readonly ITempFileCleaner _tempCleaner;

    public PcBoostService(IMemoryOptimizer optimizer, ITempFileCleaner tempCleaner)
    {
        _optimizer = optimizer;
        _tempCleaner = tempCleaner;
    }

    public MemorySnapshot GetMemorySnapshot() => _optimizer.GetSnapshot();

    public Task<MemorySnapshot> OptimizeAsync(CancellationToken cancellationToken = default)
        => Task.Run(_optimizer.Optimize, cancellationToken);

    public Task<long> CleanTempFilesAsync(CancellationToken cancellationToken = default)
        => Task.Run(_tempCleaner.Clean, cancellationToken);
}
