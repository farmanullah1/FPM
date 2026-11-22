using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.HealthCheck.Modules;

/// <summary>Browser and WinINet caches (Edge, Chrome, Brave).</summary>
public sealed class BrowserCacheModule : IHealthCheckModule
{
    private readonly IBrowserCacheCleaner _cleaner;

    public BrowserCacheModule(IBrowserCacheCleaner cleaner) => _cleaner = cleaner;

    public string Id => "browser-cache";

    public string Title => "Browser cache";

    public Task ScanAsync(IList<HealthCheckItem> items, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            long bytes = _cleaner.GetReclaimableBytes();
            IReadOnlyList<string> roots = _cleaner.GetCacheRoots();

            items.Add(new HealthCheckItem
            {
                Id = Id,
                ModuleId = Id,
                Title = Title,
                Description = bytes > 0
                    ? $"{ByteFormatter.Format(bytes)} of cached web content"
                    : "Browser caches are already clean",
                Details = roots.Count == 0
                    ? "No Chromium-based browser cache folder was found."
                    : string.Join(Environment.NewLine, roots),
                Status = bytes >= 2L * 1024 * 1024 * 1024
                    ? HealthItemStatus.Critical
                    : bytes >= 512L * 1024 * 1024
                        ? HealthItemStatus.Warning
                        : HealthItemStatus.Good,
                CanFix = bytes > 0,
                Bytes = bytes
            });
        }, cancellationToken);

    public Task<bool> FixAsync(HealthCheckItem item, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            long freed = _cleaner.Clean();
            item.Bytes = freed;
            item.Description = $"{ByteFormatter.Format(freed)} released";
            item.Details = "Cache files only. Cookies, history and saved passwords were left untouched.";

            return freed > 0;
        }, cancellationToken);
}
