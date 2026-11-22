using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.HealthCheck.Modules;

/// <summary>
/// Temporary files: reports the bytes that <see cref="ITempFileCleaner"/> could
/// release and deletes them on fix.
/// </summary>
public sealed class TempFileModule : IHealthCheckModule
{
    private readonly ITempFileCleaner _cleaner;

    public TempFileModule(ITempFileCleaner cleaner) => _cleaner = cleaner;

    public string Id => "temp-files";

    public string Title => "Temporary files";

    public Task ScanAsync(IList<HealthCheckItem> items, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            long bytes = _cleaner.GetReclaimableBytes();

            items.Add(new HealthCheckItem
            {
                Id = Id,
                ModuleId = Id,
                Title = Title,
                Description = bytes > 0
                    ? $"{ByteFormatter.Format(bytes)} of temporary files older than 48 hours"
                    : "No stale temporary files found",
                Details = "Scanned %TEMP%, %WINDIR%\\Temp and %LOCALAPPDATA%\\Temp for files not written to in the last 48 hours.",
                Status = bytes >= 1024L * 1024 * 1024
                    ? HealthItemStatus.Critical
                    : bytes >= 256L * 1024 * 1024
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
            item.Details = "Files that were in use by a running program were skipped.";

            // Report failure when nothing was removed so the row is not marked as fixed.
            return freed > 0;
        }, cancellationToken);
}
