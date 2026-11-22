using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// The Storage page's "Deep cleanup": measures Windows update and log caches, the installer
/// patch cache, the recycle bin, temporary files, browser caches and developer package caches
/// (npm, NuGet, pip, Yarn, pnpm), then removes only the categories the user selected.
/// </summary>
public interface IDeepCleanupService
{
    Task<IReadOnlyList<CleanupCategory>> ScanAsync(CancellationToken cancellationToken = default);

    Task<CleanupOutcome> CleanAsync(IEnumerable<CleanupCategory> categories, CancellationToken cancellationToken = default);
}
