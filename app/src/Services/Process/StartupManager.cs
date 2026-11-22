using System.Text.Json;
using Microsoft.Win32;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Processes;

/// <summary>
/// Reads and toggles programs that start with Windows.
///
/// Three sources are used:
///  1. The Run / RunOnce registry keys (HKCU and HKLM, including WOW6432Node).
///     Windows remembers whether an entry is enabled in the sibling
///     "Explorer\StartupApproved\Run" key as a 12-byte binary blob: byte 0 is even
///     (0x02, 0x06) for enabled and odd (0x03, 0x07) for disabled.
///  2. The per-user and all-users Startup folders. Disabling a shortcut renames it to
///     "name.lnk.disabled", the same convention Task Manager uses.
///  3. Task Scheduler. Tasks are queried and toggled through the ScheduledTasks
///     PowerShell module, whose State values (Ready/Disabled) are not localised.
/// </summary>
public sealed class StartupManager : IStartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string RunWowKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRun32Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    private const string ApprovedStartupFolderKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public IReadOnlyList<StartupApp> GetStartupApps()
    {
        var apps = new List<StartupApp>();

        AddRegistryEntries(apps, Registry.CurrentUser, "HKCU", RunKey, StartupSource.RegistryCurrentUser, ApprovedRunKey);
        AddRegistryEntries(apps, Registry.CurrentUser, "HKCU", RunOnceKey, StartupSource.RegistryCurrentUser, ApprovedRunKey);
        AddRegistryEntries(apps, Registry.LocalMachine, "HKLM", RunKey, StartupSource.RegistryLocalMachine, ApprovedRunKey);
        AddRegistryEntries(apps, Registry.LocalMachine, "HKLM", RunWowKey, StartupSource.RegistryLocalMachine, ApprovedRun32Key);

        AddStartupFolderEntries(apps, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "HKCU");
        AddStartupFolderEntries(apps, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "HKLM");

        apps.AddRange(GetScheduledTasks());

        return apps.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public OperationResult SetEnabled(StartupApp app, bool enabled)
    {
        try
        {
            return app.Source switch
            {
                StartupSource.RegistryCurrentUser or StartupSource.RegistryLocalMachine => SetRegistryEntry(app, enabled),
                StartupSource.StartupFolder => SetStartupFolderEntry(app, enabled),
                StartupSource.TaskScheduler => SetScheduledTask(app, enabled),
                _ => OperationResult.Fail("Unknown startup source.")
            };
        }
        catch (UnauthorizedAccessException)
        {
            return OperationResult.Fail("Access denied. Restart PC Manager as administrator to change this entry.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // Registry Run keys
    // ------------------------------------------------------------------

    private static void AddRegistryEntries(
        ICollection<StartupApp> apps,
        RegistryKey hive,
        string scope,
        string keyPath,
        StartupSource source,
        string approvedKeyPath)
    {
        try
        {
            using RegistryKey? key = hive.OpenSubKey(keyPath);
            if (key is null)
            {
                return;
            }

            foreach (string valueName in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(valueName))
                {
                    continue;
                }

                if (key.GetValue(valueName) is not string command || string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                apps.Add(new StartupApp
                {
                    Id = $"run:{scope}:{keyPath}:{valueName}",
                    Name = valueName,
                    Command = command,
                    Path = ExtractExecutablePath(command),
                    IsEnabled = IsRegistryEntryEnabled(scope, approvedKeyPath, valueName),
                    Source = source,
                    Scope = scope,
                    Location = keyPath,
                    EntryName = valueName,
                    Impact = StartupImpact.NotMeasured
                });
            }
        }
        catch (Exception)
        {
            // HKLM can be unreadable for standard users on hardened machines.
        }
    }

    private static bool IsRegistryEntryEnabled(string scope, string approvedKeyPath, string valueName)
    {
        try
        {
            RegistryKey hive = scope == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
            using RegistryKey? key = hive.OpenSubKey(approvedKeyPath);
            if (key?.GetValue(valueName) is byte[] blob && blob.Length > 0)
            {
                return (blob[0] & 1) == 0;
            }
        }
        catch (Exception) { }

        // No StartupApproved value means Windows has never disabled it.
        return true;
    }

    private static OperationResult SetRegistryEntry(StartupApp app, bool enabled)
    {
        string approvedPath = app.Location.Contains("WOW6432Node", StringComparison.OrdinalIgnoreCase)
            ? ApprovedRun32Key
            : ApprovedRunKey;

        RegistryKey hive = app.Scope == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;

        using RegistryKey key = hive.CreateSubKey(approvedPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the StartupApproved key.");

        // Byte 0 must be even to enable and odd to disable; the remaining 11 bytes hold
        // the FILETIME of the change, which Windows tolerates as zeros.
        var blob = new byte[12];
        blob[0] = enabled ? (byte)0x02 : (byte)0x03;
        key.SetValue(app.EntryName, blob, RegistryValueKind.Binary);

        return OperationResult.Ok($"{app.Name} will {(enabled ? "start" : "not start")} with Windows.");
    }

    // ------------------------------------------------------------------
    // Startup folders
    // ------------------------------------------------------------------

    private static void AddStartupFolderEntries(ICollection<StartupApp> apps, string folder, string scope)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                bool disabled = file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                bool shortcut = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                                || (disabled && file.EndsWith(".lnk.disabled", StringComparison.OrdinalIgnoreCase));

                if (!shortcut)
                {
                    continue;
                }

                apps.Add(new StartupApp
                {
                    Id = $"folder:{scope}:{file}",
                    Name = Path.GetFileName(file).Replace(".lnk.disabled", string.Empty, StringComparison.OrdinalIgnoreCase)
                                                     .Replace(".lnk", string.Empty, StringComparison.OrdinalIgnoreCase),
                    Command = file,
                    Path = file,
                    IsEnabled = !disabled,
                    Source = StartupSource.StartupFolder,
                    Scope = scope,
                    Location = folder,
                    EntryName = Path.GetFileName(file),
                    Impact = StartupImpact.NotMeasured
                });
            }
        }
        catch (Exception) { }
    }

    private static OperationResult SetStartupFolderEntry(StartupApp app, bool enabled)
    {
        string fullPath = Path.Combine(app.Location, app.EntryName);
        const string disabledSuffix = ".disabled";

        if (enabled)
        {
            if (fullPath.EndsWith(disabledSuffix, StringComparison.OrdinalIgnoreCase))
            {
                string restored = fullPath[..^disabledSuffix.Length];
                File.Move(fullPath, restored, overwrite: false);
            }
        }
        else
        {
            if (!fullPath.EndsWith(disabledSuffix, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(fullPath, fullPath + disabledSuffix, overwrite: false);
            }
        }

        return OperationResult.Ok($"{app.Name} will {(enabled ? "start" : "not start")} with Windows.");
    }

    // ------------------------------------------------------------------
    // Task Scheduler
    // ------------------------------------------------------------------

    private static IReadOnlyList<StartupApp> GetScheduledTasks()
    {
        const string script = """
$ErrorActionPreference = 'Stop'
Get-ScheduledTask | ForEach-Object {
    [pscustomobject]@{
        Name  = $_.TaskName
        Path  = $_.TaskPath
        State = [string]$_.State
    }
} | ConvertTo-Json -Compress
""";

        ProcessResult result = ProcessRunner.RunPowerShell(script, TimeSpan.FromSeconds(30));
        if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return Array.Empty<StartupApp>();
        }

        var apps = new List<StartupApp>();

        try
        {
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
            IEnumerable<JsonElement> elements = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray()
                : new[] { document.RootElement };

            foreach (JsonElement element in elements)
            {
                string name = element.TryGetProperty("Name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
                string path = element.TryGetProperty("Path", out JsonElement pathElement) ? pathElement.GetString() ?? "\\" : "\\";
                string state = element.TryGetProperty("State", out JsonElement stateElement) ? stateElement.GetString() ?? string.Empty : string.Empty;

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                apps.Add(new StartupApp
                {
                    Id = $"task:{path}{name}",
                    Name = name,
                    Command = path + name,
                    Path = path,
                    IsEnabled = !state.Equals("Disabled", StringComparison.OrdinalIgnoreCase),
                    Source = StartupSource.TaskScheduler,
                    Scope = "TASK",
                    Location = path,
                    EntryName = name,
                    IsSystem = path.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase),
                    Impact = StartupImpact.NotMeasured
                });
            }
        }
        catch (JsonException)
        {
            return Array.Empty<StartupApp>();
        }

        return apps;
    }

    private static OperationResult SetScheduledTask(StartupApp app, bool enabled)
    {
        if (app.IsSystem)
        {
            return OperationResult.Fail("Built-in Windows tasks are managed by Windows and cannot be changed here.");
        }

        string verb = enabled ? "Enable-ScheduledTask" : "Disable-ScheduledTask";
        string taskPath = app.Location.Replace("'", "''");
        string taskName = app.EntryName.Replace("'", "''");

        string script = $"""
$ErrorActionPreference = 'Stop'
{verb} -TaskPath '{taskPath}' -TaskName '{taskName}' | Out-Null
""";

        ProcessResult result = ProcessRunner.RunPowerShell(script, TimeSpan.FromSeconds(30));

        return result.Success
            ? OperationResult.Ok($"{app.Name} will {(enabled ? "run" : "not run")} at logon.")
            : OperationResult.Fail(result.Message);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Pulls the executable out of a command line:
    ///   "C:\Program Files\App\app.exe" --startup  ->  C:\Program Files\App\app.exe
    ///   C:\Tools\tool.exe /bg                     ->  C:\Tools\tool.exe
    /// </summary>
    private static string ExtractExecutablePath(string command)
    {
        command = command.Trim();

        if (command.StartsWith('"'))
        {
            int closing = command.IndexOf('"', 1);
            return closing > 0 ? command[1..closing] : command.Trim('"');
        }

        int space = command.IndexOf(' ');
        return space > 0 ? command[..space] : command;
    }
}
