using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Storage;

/// <summary>Storage facade: drive usage, large-file search and the Storage Sense switch.</summary>
public sealed class StorageService : IStorageService
{
    private readonly IStorageAnalyzer _analyzer;
    private readonly IStorageSenseService _storageSense;

    public StorageService(IStorageAnalyzer analyzer, IStorageSenseService storageSense)
    {
        _analyzer = analyzer;
        _storageSense = storageSense;
    }

    public Task<IReadOnlyList<DriveSpace>> GetDrivesAsync(CancellationToken cancellationToken = default)
        => Task.Run(_analyzer.GetDrives, cancellationToken);

    public Task<IReadOnlyList<LargeFile>> FindLargeFilesAsync(string path, long minSizeBytes, CancellationToken cancellationToken = default)
        => Task.Run(() => _analyzer.FindLargeFiles(path, minSizeBytes, maxResults: 200, cancellationToken), cancellationToken);

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
