using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Storage;

public sealed class StorageIntelligenceService : IStorageIntelligenceService
{
    private readonly IAppLogger _logger;

    public StorageIntelligenceService(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<DriveHealthInfo>> GetDrivesHealthAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var list = new List<DriveHealthInfo>();

            try
            {
                // Query physical disks via PowerShell
                string script = "Get-PhysicalDisk | Select-Object DeviceId, FriendlyName, MediaType, BusType, HealthStatus, OperationalStatus, Size | ConvertTo-Json -Compress";
                ProcessResult result = ProcessRunner.RunPowerShell(script, TimeSpan.FromSeconds(15));

                bool? trimEnabled = CheckTrimStatus();

                if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    string json = result.StandardOutput.Trim();

                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement elem in doc.RootElement.EnumerateArray())
                        {
                            list.Add(ParseDiskElement(elem, trimEnabled));
                        }
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        list.Add(ParseDiskElement(doc.RootElement, trimEnabled));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to query physical disk health.", ex);
            }

            // Fallback if physical disk query is empty
            if (list.Count == 0)
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    list.Add(new DriveHealthInfo(
                        DeviceId: drive.Name,
                        Model: string.IsNullOrWhiteSpace(drive.VolumeLabel) ? $"Local Disk ({drive.Name})" : drive.VolumeLabel,
                        MediaType: "Fixed Drive",
                        BusType: "Local",
                        HealthStatus: "Healthy",
                        TrimEnabled: null,
                        OperationalStatus: "OK",
                        SizeBytes: drive.TotalSize));
                }
            }

            return (IReadOnlyList<DriveHealthInfo>)list;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static DriveHealthInfo ParseDiskElement(JsonElement elem, bool? trimEnabled)
    {
        string id = elem.TryGetProperty("DeviceId", out var pId) ? pId.ToString() : "0";
        string model = elem.TryGetProperty("FriendlyName", out var pModel) ? pModel.GetString() ?? "Physical Disk" : "Physical Disk";
        string media = elem.TryGetProperty("MediaType", out var pMedia) ? pMedia.GetString() ?? "Unknown" : "Unknown";
        string bus = elem.TryGetProperty("BusType", out var pBus) ? pBus.GetString() ?? "Unknown" : "Unknown";
        string health = elem.TryGetProperty("HealthStatus", out var pHealth) ? pHealth.GetString() ?? "Healthy" : "Healthy";
        string op = elem.TryGetProperty("OperationalStatus", out var pOp) ? pOp.GetString() ?? "OK" : "OK";
        long size = elem.TryGetProperty("Size", out var pSize) && pSize.TryGetInt64(out long s) ? s : 0;

        return new DriveHealthInfo(id, model, media, bus, health, trimEnabled, op, size);
    }

    private bool? CheckTrimStatus()
    {
        try
        {
            ProcessResult res = ProcessRunner.Run("fsutil.exe", "behavior query DisableDeleteNotify", TimeSpan.FromSeconds(5));
            if (res.Success)
            {
                // DisableDeleteNotify = 0 indicates TRIM is ENABLED
                if (res.StandardOutput.Contains("= 0"))
                {
                    return true;
                }
                if (res.StandardOutput.Contains("= 1"))
                {
                    return false;
                }
            }
        }
        catch
        {
            // fsutil may require elevation or fail
        }
        return null;
    }

    public async Task<DismAnalysisResult> AnalyzeComponentStoreAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                ProcessResult res = ProcessRunner.Run("dism.exe", "/Online /Cleanup-Image /AnalyzeComponentStore", TimeSpan.FromSeconds(60));

                if (!res.Success)
                {
                    return DismAnalysisResult.Failed(res.Message.Length > 0 ? res.Message : "DISM execution failed or requires elevation.");
                }

                string outText = res.StandardOutput;

                string compSize = MatchRegex(outText, @"Windows Explorer Reported Size of Component Store\s*:\s*(.+)");
                string actSize = MatchRegex(outText, @"Actual Size of Component Store\s*:\s*(.+)");
                string shared = MatchRegex(outText, @"Shared with Windows\s*:\s*(.+)");
                string backups = MatchRegex(outText, @"Backups and Disabled Features\s*:\s*(.+)");
                string cache = MatchRegex(outText, @"Cache and Temporary Data\s*:\s*(.+)");
                string cleanRec = MatchRegex(outText, @"Component Store Cleanup Recommended\s*:\s*(.+)");

                bool cleanupRecommended = cleanRec.StartsWith("Yes", StringComparison.OrdinalIgnoreCase);

                return new DismAnalysisResult(
                    Success: true,
                    RawOutput: outText,
                    ComponentStoreSize: string.IsNullOrWhiteSpace(compSize) ? "Measured by DISM" : compSize,
                    ActualStoreSize: string.IsNullOrWhiteSpace(actSize) ? "Measured by DISM" : actSize,
                    SharedWithWindows: string.IsNullOrWhiteSpace(shared) ? "N/A" : shared,
                    BackupsAndDisabledFeatures: string.IsNullOrWhiteSpace(backups) ? "N/A" : backups,
                    CacheAndTempData: string.IsNullOrWhiteSpace(cache) ? "N/A" : cache,
                    CleanupRecommended: cleanupRecommended,
                    ErrorMessage: string.Empty);
            }
            catch (Exception ex)
            {
                _logger.Error("DISM Component Store analysis failed.", ex);
                return DismAnalysisResult.Failed(ex.Message);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string MatchRegex(string text, string pattern)
    {
        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    public async Task<IReadOnlyList<StorageFolderItem>> AnalyzeStorageMapAsync(string driveRoot, int topCount = 10, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<StorageFolderItem>();

            try
            {
                var dir = new DirectoryInfo(driveRoot);
                if (!dir.Exists)
                {
                    return results;
                }

                var driveInfo = new DriveInfo(driveRoot);
                long totalDriveBytes = driveInfo.TotalSize > 0 ? driveInfo.TotalSize : 1;

                foreach (DirectoryInfo subDir in dir.GetDirectories())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Skip reparse points and system volume info
                    if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    long bytes = CalculateDirectorySize(subDir, maxDepth: 2, cancellationToken);
                    if (bytes <= 0) continue;

                    string category = ClassifyFolder(subDir.Name);
                    double percent = (double)bytes / totalDriveBytes * 100.0;

                    results.Add(new StorageFolderItem(
                        Name: subDir.Name,
                        FullPath: subDir.FullName,
                        Bytes: bytes,
                        FormattedSize: ByteFormatter.Format(bytes),
                        Category: category,
                        PercentOfDrive: Math.Round(percent, 1)));
                }

                results = results.OrderByDescending(r => r.Bytes).Take(topCount).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to analyze storage map for {driveRoot}.", ex);
            }

            return (IReadOnlyList<StorageFolderItem>)results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string ClassifyFolder(string name)
    {
        string lower = name.ToLowerInvariant();
        if (lower is "windows" or "recovery" or "$recycle.bin" or "system volume information")
            return "System";
        if (lower.StartsWith("program files") || lower is "programdata")
            return "Apps";
        if (lower is "users")
            return "Personal";
        return "Other";
    }

    private static long CalculateDirectorySize(DirectoryInfo dir, int maxDepth, CancellationToken ct)
    {
        if (maxDepth <= 0 || ct.IsCancellationRequested) return 0;

        long size = 0;
        try
        {
            foreach (FileInfo file in dir.GetFiles())
            {
                size += file.Length;
            }

            foreach (DirectoryInfo sub in dir.GetDirectories())
            {
                if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    size += CalculateDirectorySize(sub, maxDepth - 1, ct);
                }
            }
        }
        catch
        {
            // Access denied or skipped folders
        }
        return size;
    }
}
