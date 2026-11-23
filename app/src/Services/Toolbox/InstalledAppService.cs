using System.Diagnostics;
using Microsoft.Win32;
using PCManager.Core.Formatting;
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

    /// <summary>How long a started uninstaller may run before the result becomes "still running".</summary>
    private static readonly TimeSpan UninstallerTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// After the uninstaller process exits, how long the registry entry may take to disappear.
    /// Some installers hand off to another process, so "process gone" is not "app gone".
    /// </summary>
    private static readonly TimeSpan ExitGrace = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IToolLauncher _launcher;

    public InstalledAppService(IToolLauncher launcher) => _launcher = launcher;

    public Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => ReadInstalledApps(cancellationToken), cancellationToken);

    private static IReadOnlyList<InstalledApp> ReadInstalledApps(CancellationToken cancellationToken)
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
    }

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
        => Task.Run(() => UninstallCoreAsync(app, cancellationToken), cancellationToken);

    private async Task<OperationResult> UninstallCoreAsync(InstalledApp app, CancellationToken cancellationToken)
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

            Process? process;
            try
            {
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"Could not start the uninstaller for {app.Name}: {ex.Message}");
            }

            if (process is null)
            {
                return OperationResult.Fail(
                    $"Windows did not start the uninstaller for {app.Name}. Use Settings > Apps.");
            }

            using (process)
            {
                return await WaitForRemovalAsync(app, process, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Waits for the uninstaller, then for the app's Uninstall registry entry to disappear,
    /// and reports the measured leftovers instead of guessing from the exit code.
    /// </summary>
    private static async Task<OperationResult> WaitForRemovalAsync(
        InstalledApp app,
        Process process,
        CancellationToken cancellationToken)
    {
        bool processExited = false;

        try
        {
            using var timeout = new CancellationTokenSource(UninstallerTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            processExited = true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationResult.Ok(
                $"The uninstaller for {app.Name} is still running after {UninstallerTimeout.TotalMinutes:0} minutes. Refresh the list when it finishes.");
        }
        catch (InvalidOperationException)
        {
            // Started through the shell (for example after a UAC prompt): there is no wait
            // handle, so the registry list disappearing is the only completion signal.
        }

        DateTime deadline = DateTime.UtcNow + (processExited ? ExitGrace : UninstallerTimeout);

        while (StillInstalled(app))
        {
            if (DateTime.UtcNow >= deadline)
            {
                return OperationResult.Fail(processExited
                    ? $"{app.Name} is still listed after its uninstaller closed. It may have been cancelled; refresh to re-check."
                    : $"{app.Name} is still listed after {UninstallerTimeout.TotalMinutes:0} minutes. It may still be uninstalling; refresh later.");
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        return OperationResult.Ok(DescribeRemoval(app));
    }

    /// <summary>True while the app's own registry entry is still listed.</summary>
    private static bool StillInstalled(InstalledApp app)
    {
        try
        {
            return ReadInstalledApps(CancellationToken.None).Any(candidate =>
                candidate.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase) &&
                candidate.Version.Equals(app.Version, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            // If the list cannot be read, do not claim the app is gone.
            return true;
        }
    }

    /// <summary>
    /// Reports what the uninstaller left behind after the registry entry disappeared. This is
    /// a read-only measurement: nothing under InstallLocation is deleted by this app.
    /// </summary>
    private static string DescribeRemoval(InstalledApp app)
    {
        string removed = $"{app.Name} was removed. It is no longer in the installed apps list.";

        if (string.IsNullOrWhiteSpace(app.InstallLocation))
        {
            return removed;
        }

        string location = Environment.ExpandEnvironmentVariables(app.InstallLocation).TrimEnd('\\', '/');

        // Never walk a whole drive or a path that is only its root.
        string? root = location.Length == 0 ? null : Path.GetPathRoot(location);
        if (string.IsNullOrEmpty(root) || location.TrimEnd('\\', '/') == root.TrimEnd('\\', '/') ||
            !Directory.Exists(location))
        {
            return removed;
        }

        (long bytes, int files) = MeasureFolder(location);

        return files == 0
            ? $"{removed} The folder {location} still exists and was left untouched."
            : $"{removed} {ByteFormatter.Format(bytes)} in {files} file(s) remain in {location} and were left untouched.";
    }

    private static (long Bytes, int Files) MeasureFolder(string path)
    {
        long bytes = 0;
        int files = 0;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false
        };

        try
        {
            foreach (FileInfo file in new DirectoryInfo(path).EnumerateFiles("*", options))
            {
                bytes += file.Length;
                files++;
            }
        }
        catch (Exception)
        {
            // A partial measurement is more useful than failing the whole result.
        }

        return (bytes, files);
    }

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
