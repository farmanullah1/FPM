using System.Diagnostics;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.HealthCheck;

/// <summary>
/// Runs every registered <see cref="IHealthCheckModule"/> and applies fixes.
/// A failing module never aborts the scan: the failure is reported as its own row so
/// the UI can show what went wrong.
/// </summary>
public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IReadOnlyList<IHealthCheckModule> _modules;

    public HealthCheckService(IEnumerable<IHealthCheckModule> modules)
    {
        _modules = modules.ToList();
    }

    public async Task<HealthCheckSummary> ScanAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var summary = new HealthCheckSummary { ScannedAt = DateTime.Now };
        var stopwatch = Stopwatch.StartNew();

        foreach (IHealthCheckModule module in _modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Scanning {module.Title.ToLowerInvariant()}...");

            try
            {
                await module.ScanAsync(summary.Items, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                summary.Items.Add(new HealthCheckItem
                {
                    Id = module.Id,
                    ModuleId = module.Id,
                    Title = module.Title,
                    Description = "This scan could not be completed.",
                    Details = ex.Message,
                    Status = HealthItemStatus.Warning,
                    CanFix = false
                });
            }
        }

        stopwatch.Stop();
        summary.Duration = stopwatch.Elapsed;
        progress?.Report("Scan complete");
        return summary;
    }

    public async Task<long> FixAsync(HealthCheckSummary summary, CancellationToken cancellationToken = default)
    {
        long freed = 0;

        var targets = summary.Items
            .Where(i => i.IsSelected && i.CanFix && !i.FixApplied)
            .ToList();

        foreach (HealthCheckItem item in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IHealthCheckModule? module = _modules.FirstOrDefault(m => m.Id == item.ModuleId);
            if (module is null)
            {
                continue;
            }

            try
            {
                if (await module.FixAsync(item, cancellationToken).ConfigureAwait(false))
                {
                    item.FixApplied = true;
                    item.Status = HealthItemStatus.Good;
                    freed += Math.Max(0, item.Bytes);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                item.Details = $"Cleanup failed: {ex.Message}";
            }
        }

        summary.BytesFreed += freed;
        return freed;
    }
}
