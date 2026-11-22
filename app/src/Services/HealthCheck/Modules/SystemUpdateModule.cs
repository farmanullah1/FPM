using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.HealthCheck.Modules;

/// <summary>Windows Update service state and pending-reboot flags.</summary>
public sealed class SystemUpdateModule : IHealthCheckModule
{
    private readonly IWindowsUpdateStatusService _updateService;

    public SystemUpdateModule(IWindowsUpdateStatusService updateService) => _updateService = updateService;

    public string Id => "windows-update";

    public string Title => "Windows Update";

    public Task ScanAsync(IList<HealthCheckItem> items, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            UpdateStatus status = _updateService.GetStatus();

            HealthItemStatus state = HealthItemStatus.Good;
            if (status.PendingReboot)
            {
                state = HealthItemStatus.Warning;
            }
            else if (!status.UpdateServiceRunning)
            {
                state = HealthItemStatus.Critical;
            }

            items.Add(new HealthCheckItem
            {
                Id = Id,
                ModuleId = Id,
                Title = Title,
                Description = status.PendingReboot
                    ? "A restart is pending to finish installing updates"
                    : status.UpdateServiceRunning
                        ? "Update service is running"
                        : "Windows Update service is stopped",
                Details = status.LastInstalled.Count > 0
                    ? "Recently installed: " + string.Join(", ", status.LastInstalled)
                    : "No recently installed hotfixes were reported.",
                Status = state,
                CanFix = false
            });
        }, cancellationToken);

    public Task<bool> FixAsync(HealthCheckItem item, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
