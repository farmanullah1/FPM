using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.HealthCheck.Modules;

/// <summary>Free-space warning for the drive Windows is installed on.</summary>
public sealed class DiskSpaceModule : IHealthCheckModule
{
    private readonly IStorageAnalyzer _analyzer;

    public DiskSpaceModule(IStorageAnalyzer analyzer) => _analyzer = analyzer;

    public string Id => "disk-space";

    public string Title => "System drive";

    public Task ScanAsync(IList<HealthCheckItem> items, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            string systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";

            DriveSpace? drive = _analyzer.GetDrives()
                .FirstOrDefault(d => string.Equals(d.Name, systemRoot, StringComparison.OrdinalIgnoreCase));

            if (drive is null || !drive.IsReady)
            {
                items.Add(new HealthCheckItem
                {
                    Id = Id,
                    ModuleId = Id,
                    Title = Title,
                    Description = "System drive information is unavailable",
                    Status = HealthItemStatus.Warning
                });
                return;
            }

            double freePercent = drive.TotalBytes == 0 ? 100 : (double)drive.FreeBytes / drive.TotalBytes * 100.0;

            items.Add(new HealthCheckItem
            {
                Id = Id,
                ModuleId = Id,
                Title = Title,
                Description = $"{ByteFormatter.Format(drive.FreeBytes)} free of {ByteFormatter.Format(drive.TotalBytes)} ({freePercent:0}%)",
                Details = $"{drive.Name} {drive.Format} volume \"{drive.Label}\"",
                Status = freePercent < 10
                    ? HealthItemStatus.Critical
                    : freePercent < 20
                        ? HealthItemStatus.Warning
                        : HealthItemStatus.Good,
                CanFix = false
            });
        }, cancellationToken);

    public Task<bool> FixAsync(HealthCheckItem item, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
