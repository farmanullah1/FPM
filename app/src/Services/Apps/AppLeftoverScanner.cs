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
            _logger.Info($"Refused leftover scan for protected name: {cleanName}");
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
                    SafetyVerdict verdict = _safetyGuard.EvaluateRoot(dir, new[] { dir });
                    if (verdict.Allowed)
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
                            safetyReason: verdict.Reason));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Info($"Error probing directory '{dir}': {ex.Message}");
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
                _logger.Info($"Error probing registry '{hive}\\{subKey}': {ex.Message}");
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
                List<string> keys = registryKeys
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (keys.Count == 0)
                {
                    return OperationResult.Fail("No registry keys were supplied for the backup.");
                }

                string? dir = Path.GetDirectoryName(backupFilePath);
                if (string.IsNullOrWhiteSpace(dir))
                {
                    return OperationResult.Fail("The registry backup path has no folder.");
                }

                Directory.CreateDirectory(dir);
                string baseName = Path.GetFileNameWithoutExtension(backupFilePath);
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    baseName = "reg_leftovers";
                }

                var written = new List<string>();
                var failures = new List<string>();

                for (int index = 0; index < keys.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // One file per key: reg.exe export overwrites, so sharing one name would
                    // silently lose every key except the last.
                    string fileName = keys.Count == 1
                        ? $"{baseName}.reg"
                        : $"{baseName}_{index + 1:00}_{SanitizeKeyName(keys[index])}.reg";
                    string target = Path.Combine(dir, fileName);

                    ProcessStartInfo psi = new("reg.exe")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };
                    psi.ArgumentList.Add("export");
                    psi.ArgumentList.Add(keys[index]);
                    psi.ArgumentList.Add(target);
                    psi.ArgumentList.Add("/y");

                    using Process? proc = Process.Start(psi);
                    if (proc is null)
                    {
                        failures.Add(keys[index]);
                        continue;
                    }

                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(10000);

                    if (proc.ExitCode != 0 || !File.Exists(target))
                    {
                        failures.Add(string.IsNullOrWhiteSpace(error) ? keys[index] : $"{keys[index]} ({error.Trim()})");
                        continue;
                    }

                    written.Add(target);
                }

                if (failures.Count > 0)
                {
                    return OperationResult.Fail(
                        $"Exported {written.Count} of {keys.Count} registry key(s); failed: {string.Join("; ", failures)}");
                }

                return OperationResult.Ok(
                    keys.Count == 1 ? $"Exported registry backup to: {written[0]}" : $"Exported {written.Count} registry backups to: {dir}");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Registry export failed: {ex.Message}");
            }
        }, cancellationToken);

    private static string SanitizeKeyName(string key)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(key.Length);
        foreach (char c in key)
        {
            builder.Append(c == '\\' || Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        string result = builder.ToString().Trim('_');
        return result.Length > 60 ? result[^60..] : result;
    }

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
                OperationResult backup = ExportRegBackupAsync(regKeys, backupPath, cancellationToken).GetAwaiter().GetResult();
                if (!backup.Success)
                {
                    // Fail closed: no registry key may disappear without a readable backup.
                    return OperationResult.Fail($"Leftover cleanup stopped because the registry backup failed. {backup.Message}");
                }
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
