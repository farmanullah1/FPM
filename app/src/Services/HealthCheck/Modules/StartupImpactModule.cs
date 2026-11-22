using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.HealthCheck.Modules;

/// <summary>
/// Reports how many programs are registered to start with Windows. This module is
/// read-only on purpose: silently disabling startup entries is destructive, so the
/// user gets a link to the Process &amp; Startup page instead.
/// </summary>
public sealed class StartupImpactModule : IHealthCheckModule
{
    private readonly IStartupManager _startupManager;

    public StartupImpactModule(IStartupManager startupManager) => _startupManager = startupManager;

    public string Id => "startup-impact";

    public string Title => "Startup impact";

    public Task ScanAsync(IList<HealthCheckItem> items, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            IReadOnlyList<StartupApp> apps = _startupManager.GetStartupApps();
            var enabled = apps.Where(a => a.IsEnabled && !a.IsSystem).ToList();

            items.Add(new HealthCheckItem
            {
                Id = Id,
                ModuleId = Id,
                Title = Title,
                Description = enabled.Count == 0
                    ? "No third-party programs start with Windows"
                    : $"{enabled.Count} program{(enabled.Count == 1 ? string.Empty : "s")} start with Windows",
                Details = enabled.Count == 0
                    ? "Run keys, Startup folders and scheduled tasks were checked."
                    : string.Join(", ", enabled.Take(8).Select(a => a.Name))
                      + (enabled.Count > 8 ? $" and {enabled.Count - 8} more" : string.Empty),
                Status = enabled.Count > 8
                    ? HealthItemStatus.Critical
                    : enabled.Count > 4
                        ? HealthItemStatus.Warning
                        : HealthItemStatus.Good,
                CanFix = false,
                Bytes = 0
            });
        }, cancellationToken);

    public Task<bool> FixAsync(HealthCheckItem item, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
