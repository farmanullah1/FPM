using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Apps;

public sealed class AppLeftoverScanner : IAppLeftoverScanner
{
    private static readonly string[] ProtectedNames =
    {
        "Windows", "Microsoft", "System32", "SysWOW64", "Common Files",
        "Internet Explorer", "Windows NT", "WindowsApps", "FPM", "FPM.Helper",
        "Microsoft.MicrosoftPCManager", "DotNet", "dotnet", "PowerShell"
    };

    private readonly ISafetyGuard _safetyGuard;
    private readonly IAppLogger _logger;

    public AppLeftoverScanner(ISafetyGuard safetyGuard, IAppLogger logger)
    {
        _safetyGuard = safetyGuard;
        _logger = logger;
    }

    public Task<AppLeftoverScanResult> ScanLeftoversAsync(
        string appName,
        string? publisher = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => ScanLeftoversCore(appName, publisher, cancellationToken), cancellationToken);

    private AppLeftoverScanResult ScanLeftoversCore(
        string appName,
        string? publisher,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appName) || appName.Trim().Length < 3)
        {
            return new AppLeftoverScanResult(appName, Array.Empty<AppLeftoverItem>(), 0, 0, false);
        }

        string cleanName = appName.Trim();

        // Safety verification: never scan or clean protected system components
        if (IsProtected(cleanName))
        {
            _logger.Warn($"Refused leftover scan for protected name: {cleanName}");
            return new AppLeftoverScanResult(cleanName, Array.Empty<AppLeftoverItem>(), 0, 0, false);
        }

        var items = new List<AppLeftoverItem>();

        // 1. Filesystem Candidate Locations
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(localAppData, cleanName),
            Path.Combine(appData, cleanName),
            Path.Combine(commonAppData, cleanName),
            Path.Combine(localAppData, "Programs", cleanName)
        };

        if (!string.IsNullOrWhiteSpace(publisher) && !IsProtected(publisher))
        {
            candidateDirs.Add(Path.Combine(localAppData, publisher.Trim(), cleanName));
            candidateDirs.Add(Path.Combine(appData, publisher.Trim(), cleanName));
        }

        foreach (string dir in candidateDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (Directory.Exists(dir))
                {
                    // Double check with SafetyGuard
                    SafetyDecision decision = _safetyGuard.EvaluatePath(dir);
                    if (decision.IsAllowed)
                    {
                        long size = CalculateDirectorySize(dir, cancellationToken);
                        items.Add(new AppLeftoverItem(
                            Path.GetFileName(dir),
                            dir,
                            AppLeftoverKind.Directory,
                            size,
                            ByteFormatter.Format(size),
                            isSafeToRemove: true,
                            safetyReason: "Non-system application data folder"));
                    }
                    else
                    {
                        items.Add(new AppLeftoverItem(
                            Path.GetFileName(dir),
                            dir,
                            AppLeftoverKind.Directory,
                            0,
                            "-",
                            isSafeToRemove: false,
                            safetyReason: decision.Reason));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Error probing directory '{dir}': {ex.Message}");
            }
        }

        // 2. Registry Candidate Locations
        var candidateRegKeys = new List<(RegistryHive Hive, string SubKey)>
        {
            (RegistryHive.CurrentUser, $@"Software\{cleanName}"),
            (RegistryHive.LocalMachine, $@"SOFTWARE\{cleanName}")
        };

        if (!string.IsNullOrWhiteSpace(publisher) && !IsProtected(publisher))
        {
            candidateRegKeys.Add((RegistryHive.CurrentUser, $@"Software\{publisher.Trim()}\{cleanName}"));
            candidateRegKeys.Add((RegistryHive.LocalMachine, $@"SOFTWARE\{publisher.Trim()}\{cleanName}"));
        }

        foreach (var (hive, subKey) in candidateRegKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using RegistryKey? target = baseKey.OpenSubKey(subKey);
                if (target is not null)
                {
                    int valueCount = target.ValueCount;
                    int subKeyCount = target.SubKeyCount;
                    string fullKeyPath = $@"{hive}\{subKey}";

                    items.Add(new AppLeftoverItem(
                        cleanName,
                        fullKeyPath,
                        AppLeftoverKind.RegistryKey,
                        0,
                        $"{valueCount} value(s), {subKeyCount} subkey(s)",
                        isSafeToRemove: true,
                        safetyReason: "Isolated application software key"));
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Error probing registry '{hive}\\{subKey}': {ex.Message}");
            }
        }

        long totalBytes = items.Where(i => i.Kind == AppLeftoverKind.Directory).Sum(i => i.SizeBytes);
        int regCount = items.Count(i => i.Kind == AppLeftoverKind.RegistryKey);

        return new AppLeftoverScanResult(cleanName, items, totalBytes, regCount, items.Count > 0);
    }

    public Task<OperationResult> ExportRegBackupAsync(
        IEnumerable<string> registryKeys,
        string backupFilePath,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            try
            {
                string? dir = Path.GetDirectoryName(backupFilePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                foreach (string key in registryKeys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Invoke reg.exe export
                    ProcessStartInfo psi = new("reg.exe", $"export \"{key}\" \"{backupFilePath}\" /y")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };

                    using Process? proc = Process.Start(psi);
                    proc?.WaitForExit(5000);
                }

                return File.Exists(backupFilePath)
                    ? OperationResult.Ok($"Exported registry backup to: {backupFilePath}")
                    : OperationResult.Fail("Registry export command did not produce a file.");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Registry export failed: {ex.Message}");
            }
        }, cancellationToken);

    public Task<OperationResult> CleanLeftoversAsync(
        IEnumerable<AppLeftoverItem> items,
        bool createRegBackup = true,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            int deletedFolders = 0;
            int deletedRegKeys = 0;
            var errors = new List<string>();

            var selectedItems = items.Where(i => i.IsSelected && i.IsSafeToRemove).ToList();
            if (selectedItems.Count == 0)
            {
                return OperationResult.Ok("No items were selected for leftover cleanup.");
            }

            // Create backup if registry keys are being removed
            var regKeys = selectedItems.Where(i => i.Kind == AppLeftoverKind.RegistryKey).Select(i => i.TargetPath).ToList();
            if (regKeys.Count > 0 && createRegBackup)
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FPM", "Backups");
                string backupPath = Path.Combine(backupDir, $"reg_leftovers_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
                ExportRegBackupAsync(regKeys, backupPath, cancellationToken).GetAwaiter().GetResult();
            }

            foreach (AppLeftoverItem item in selectedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.Kind == AppLeftoverKind.Directory)
                {
                    try
                    {
                        if (Directory.Exists(item.TargetPath))
                        {
                            // Send to Recycle Bin (safe & non-destructive)
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                                item.TargetPath,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            deletedFolders++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Folder '{item.TargetPath}': {ex.Message}");
                    }
                }
                else if (item.Kind == AppLeftoverKind.RegistryKey)
                {
                    try
                    {
                        string[] parts = item.TargetPath.Split(new[] { '\\' }, 2);
                        if (parts.Length == 2)
                        {
                            RegistryHive hive = parts[0].Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
                                ? RegistryHive.LocalMachine
                                : RegistryHive.CurrentUser;

                            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                            baseKey.DeleteSubKeyTree(parts[1], throwOnMissingSubKey: false);
                            deletedRegKeys++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Registry '{item.TargetPath}': {ex.Message}");
                    }
                }
            }

            string summary = $"Cleaned {deletedFolders} folder(s) to Recycle Bin and removed {deletedRegKeys} registry key(s).";
            if (errors.Count > 0)
            {
                summary += $" Errors encountered: {string.Join("; ", errors)}";
            }

            return OperationResult.Ok(summary);
        }, cancellationToken);

    private static bool IsProtected(string name)
    {
        return ProtectedNames.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
    }

    private static long CalculateDirectorySize(string path, CancellationToken cancellationToken)
    {
        long bytes = 0;
        try
        {
            var dir = new DirectoryInfo(path);
            foreach (FileInfo file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                bytes += file.Length;
            }
        }
        catch
        {
            // Ignore inaccessible items during size calc
        }

        return bytes;
    }
}
