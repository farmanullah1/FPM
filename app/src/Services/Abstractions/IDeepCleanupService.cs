using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// The Storage page's "Deep cleanup": measures Windows Update downloads, system logs,
/// the recycle bin, temporary files and browser caches, then removes only the categories
/// the user selected.
/// </summary>
public interface IDeepCleanupService
{
    Task<IReadOnlyList<CleanupCategory>> ScanAsync(CancellationToken cancellationToken = default);

    Task<CleanupOutcome> CleanAsync(IEnumerable<CleanupCategory> categories, CancellationToken cancellationToken = default);
}
