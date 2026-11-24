using System.IO;
using Microsoft.Win32;
using PCManager.Core;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Infrastructure;

public sealed class MaintenanceReadinessService : IMaintenanceReadinessService
{
    private readonly ICleanupRuleCatalog _ruleCatalog;
    private readonly IAppLogger _logger;

    public MaintenanceReadinessService(ICleanupRuleCatalog ruleCatalog, IAppLogger logger)
    {
        _ruleCatalog = ruleCatalog;
        _logger = logger;
    }

    public Task<MaintenanceReadinessResult> CheckReadinessAsync(CancellationToken cancellationToken = default)
        => Task.Run(CheckReadinessCore, cancellationToken);

    private MaintenanceReadinessResult CheckReadinessCore()
    {
        var checks = new List<ReadinessCheckItem>();

        // 1. Data Folder Writability
        try
        {
            string testFile = Path.Combine(AppInfo.DataFolder, ".readiness_probe");
            Directory.CreateDirectory(AppInfo.DataFolder);
            File.WriteAllText(testFile, "OK");
            File.Delete(testFile);
            checks.Add(new ReadinessCheckItem("Data Folder Writability", "Storage", true, $"Writable: {AppInfo.DataFolder}"));
        }
        catch (Exception ex)
        {
            checks.Add(new ReadinessCheckItem("Data Folder Writability", "Storage", false, $"Failed to write to data folder: {ex.Message}"));
        }

        // 2. Free Disk Space on System Volume
        try
        {
            string sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(sysRoot);
            long freeBytes = drive.AvailableFreeSpace;
            bool ok = freeBytes >= 100L * 1024 * 1024; // 100 MB threshold
            checks.Add(new ReadinessCheckItem(
                "System Drive Headroom",
                "Storage",
                ok,
                $"{ByteFormatter.Format(freeBytes)} free on {sysRoot}"));
        }
        catch (Exception ex)
        {
            checks.Add(new ReadinessCheckItem("System Drive Headroom", "Storage", false, ex.Message));
        }

        // 3. Elevated Helper Executable
        try
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string helperPath = Path.Combine(appDir, "FPM.Helper.exe");
            bool exists = File.Exists(helperPath);
            checks.Add(new ReadinessCheckItem(
                "Helper Subsystem",
                "Elevation",
                exists,
                exists ? "FPM.Helper.exe detected and ready" : "FPM.Helper.exe not found in base directory"));
        }
        catch (Exception ex)
        {
            checks.Add(new ReadinessCheckItem("Helper Subsystem", "Elevation", false, ex.Message));
        }

        // 4. Rule Catalog Integrity
        try
        {
            IReadOnlyList<CleanupRule> rules = _ruleCatalog.GetRules();
            bool ok = rules.Count > 0;
            checks.Add(new ReadinessCheckItem(
                "Rule Pack Integrity",
                "Cleanup Engine",
                ok,
                ok ? $"{rules.Count} active cleanup rules loaded" : "No cleanup rules loaded in catalog"));
        }
        catch (Exception ex)
        {
            checks.Add(new ReadinessCheckItem("Rule Pack Integrity", "Cleanup Engine", false, ex.Message));
        }

        // 5. Registry Accessibility
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\FPM", writable: true);
            checks.Add(new ReadinessCheckItem(
                "Configuration Registry",
                "System",
                key is not null,
                "HKCU\\Software\\FPM accessible"));
        }
        catch (Exception ex)
        {
            checks.Add(new ReadinessCheckItem("Configuration Registry", "System", false, ex.Message));
        }

        bool allReady = checks.All(c => c.Passed);
        string summary = allReady
            ? "All 5 readiness checks passed. FPM subsystems are operating normally."
            : $"{checks.Count(c => !c.Passed)} readiness check(s) flagged issues.";

        return new MaintenanceReadinessResult(allReady, checks, summary);
    }
}
