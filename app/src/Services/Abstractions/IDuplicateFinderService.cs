using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Finds byte-for-byte duplicate files inside a folder tree.</summary>
public interface IDuplicateFinderService
{
    Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        string root,
        long minimumFileSizeBytes = 1024 * 1024,
        CancellationToken cancellationToken = default);
}
