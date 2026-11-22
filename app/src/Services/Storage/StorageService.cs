using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Storage;

/// <summary>
/// Storage Manager facade combining the analyzer, the isolated cleaners and the
/// Storage Sense switch.
/// </summary>
public sealed class StorageService : IStorageService
{
    private readonly IStorageAnalyzer _analyzer;
    private readonly ITempFileCleaner _tempCleaner;
    private readonly IBrowserCacheCleaner _cacheCleaner;
    private readonly IRecycleBinService _recycleBin;
    private readonly IStorageSenseService _storageSense;

    public StorageService(
        IStorageAnalyzer analyzer,
        ITempFileCleaner tempCleaner,
        IBrowserCacheCleaner cacheCleaner,
        IRecycleBinService recycleBin,
        IStorageSenseService storageSense)
    {
        _analyzer = analyzer;
        _tempCleaner = tempCleaner;
        _cacheCleaner = cacheCleaner;
        _recycleBin = recycleBin;
        _storageSense = storageSense;
    }

    public Task<IReadOnlyList<DriveSpace>> GetDrivesAsync(CancellationToken cancellationToken = default)
        => Task.Run(_analyzer.GetDrives, cancellationToken);

    public Task<IReadOnlyList<FolderSize>> ScanFolderAsync(string path, CancellationToken cancellationToken = default)
        => Task.Run(() => _analyzer.ScanFolder(path, maxDepth: 3, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<LargeFile>> FindLargeFilesAsync(string path, long minSizeBytes, CancellationToken cancellationToken = default)
        => Task.Run(() => _analyzer.FindLargeFiles(path, minSizeBytes, maxResults: 200, cancellationToken), cancellationToken);

    public Task<long> RunCleanupAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();

        long freed = _tempCleaner.Clean();
        freed += _cacheCleaner.Clean();

        cancellationToken.ThrowIfCancellationRequested();
        freed += _recycleBin.Empty();

        return freed;
    }, cancellationToken);

    public long GetRecycleBinSize() => _recycleBin.GetSize();

    public Task<OperationResult> EmptyRecycleBinAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        long freed = _recycleBin.Empty();
        return freed > 0
            ? OperationResult.Ok($"Recycle bin emptied, {Core.Formatting.ByteFormatter.Format(freed)} released.")
            : OperationResult.Ok("The recycle bin was already empty.");
    }, cancellationToken);

    public bool GetStorageSenseEnabled() => _storageSense.GetEnabled();

    public OperationResult SetStorageSenseEnabled(bool enabled)
    {
        try
        {
            _storageSense.SetEnabled(enabled);
            return OperationResult.Ok($"Storage Sense {(enabled ? "enabled" : "disabled")}.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}
