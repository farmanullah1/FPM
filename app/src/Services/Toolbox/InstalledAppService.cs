using System.Diagnostics;
using Microsoft.Win32;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Toolbox;

/// <summary>
/// Installed applications, read from the three Uninstall registry locations Windows keeps
/// (HKLM 64-bit, HKLM 32-bit and HKCU) — the same source Settings > Apps uses for desktop
/// apps. Store/MSIX packages have no UninstallString, so they are opened in Settings.
/// </summary>
public sealed class InstalledAppService : IInstalledAppService
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly IToolLauncher _launcher;

    public InstalledAppService(IToolLauncher launcher) => _launcher = launcher;

    public Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<InstalledApp>>(() =>
        {
            var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

            cancellationToken.ThrowIfCancellationRequested();

            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using RegistryKey? root = baseKey.OpenSubKey(UninstallKey);
                ReadApps(root, apps, isStore: false, cancellationToken);
            }

            using (RegistryKey? userRoot = Registry.CurrentUser.OpenSubKey(UninstallKey))
            {
                ReadApps(userRoot, apps, isStore: false, cancellationToken);
            }

            return apps.Values
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, cancellationToken);

    private static void ReadApps(
        RegistryKey? root,
        IDictionary<string, InstalledApp> target,
        bool isStore,
        CancellationToken cancellationToken)
    {
        if (root is null)
        {
            return;
        }

        foreach (string subKeyName in root.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using RegistryKey? entry = root.OpenSubKey(subKeyName);
                if (entry is null)
                {
                    continue;
                }

                string name = entry.GetValue("DisplayName") as string ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // Updates, patches and system components are not real applications.
                if (entry.GetValue("SystemComponent") is int systemComponent && systemComponent == 1)
                {
                    continue;
                }

                if (entry.GetValue("ParentKeyName") is string parent && !string.IsNullOrWhiteSpace(parent))
                {
                    continue;
                }

                if (entry.GetValue("ReleaseType") is string release &&
                    (release.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                     release.Contains("Hotfix", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string uninstall = entry.GetValue("UninstallString") as string
                    ?? entry.GetValue("QuietUninstallString") as string
                    ?? string.Empty;

                long sizeBytes = 0;
                if (entry.GetValue("EstimatedSize") is int kb)
                {
                    sizeBytes = kb * 1024L;
                }
                else if (entry.GetValue("EstimatedSize") is long kb64)
                {
                    sizeBytes = kb64 * 1024L;
                }

                string key = $"{name}|{entry.GetValue("DisplayVersion")}";
                if (target.ContainsKey(key))
                {
                    continue;
                }

                target[key] = new InstalledApp
                {
                    Name = name,
                    Version = entry.GetValue("DisplayVersion") as string ?? string.Empty,
                    Publisher = entry.GetValue("Publisher") as string ?? string.Empty,
                    InstallLocation = entry.GetValue("InstallLocation") as string ?? string.Empty,
                    UninstallCommand = uninstall,
                    IsStoreApp = isStore,
                    SizeBytes = sizeBytes
                };
            }
            catch (Exception)
            {
                // One malformed key must not hide the rest of the list.
            }
        }
    }

    public Task<OperationResult> UninstallAsync(InstalledApp app, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            try
            {
                if (app.IsStoreApp || string.IsNullOrWhiteSpace(app.UninstallCommand))
                {
                    // Packaged apps are removed through Settings, which owns the MSIX lifecycle.
                    _launcher.OpenUri("ms-settings:appsfeatures");
                    return OperationResult.Ok($"Open Settings to remove {app.Name}.");
                }

                (string fileName, string arguments) = SplitCommand(app.UninstallCommand);

                // MSI installers register "MsiExec.exe /I{GUID}" for install and uninstall; the
                // remove verb is /X, so rewrite it before launching.
                if (fileName.Contains("msiexec", StringComparison.OrdinalIgnoreCase) &&
                    arguments.StartsWith("/I", StringComparison.OrdinalIgnoreCase))
                {
                    arguments = "/X" + arguments[2..];
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                });

                return OperationResult.Ok($"The uninstaller for {app.Name} is starting.");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }, cancellationToken);

    /// <summary>Splits a registry command line into executable and arguments, honouring quotes.</summary>
    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        command = command.Trim();

        if (command.StartsWith('"'))
        {
            int closing = command.IndexOf('"', 1);
            if (closing > 0)
            {
                string exe = command[1..closing];
                string args = command[(closing + 1)..].Trim();
                return (exe, args);
            }
        }

        int space = command.IndexOf(' ');
        return space < 0
            ? (command, string.Empty)
            : (command[..space], command[(space + 1)..].Trim());
    }
}
