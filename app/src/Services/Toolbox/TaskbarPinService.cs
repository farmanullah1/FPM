using System.Diagnostics;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Toolbox;

/// <summary>
/// Lists taskbar-pinned shortcuts.
///
/// Windows keeps pins as .lnk files in the per-user "Quick Launch\User Pinned\TaskBar"
/// folder (and the all-users equivalent). Programmatic pinning was removed in
/// Windows 10, so the app reports the pins and opens the Taskbar settings page instead
/// of pretending it can pin something.
/// </summary>
public sealed class TaskbarPinService : ITaskbarPinService
{
    private readonly IToolLauncher _launcher;

    public TaskbarPinService(IToolLauncher launcher) => _launcher = launcher;

    public IReadOnlyList<TaskbarPinEntry> GetPinnedApps()
    {
        var entries = new List<TaskbarPinEntry>();
        HashSet<string> running = GetRunningProcessNames();

        foreach (string folder in Folders())
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (string file in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(file);

                    entries.Add(new TaskbarPinEntry
                    {
                        Name = name,
                        Path = file,
                        IsPinned = true,
                        IsRunning = running.Contains(name)
                    });
                }
            }
            catch (Exception) { }
        }

        return entries
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public OperationResult OpenTaskbarSettings()
    {
        try
        {
            _launcher.OpenUri("ms-settings:taskbar");
            return OperationResult.Ok("Opened Taskbar settings.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private static IEnumerable<string> Folders()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
    }

    private static HashSet<string> GetRunningProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                names.Add(process.ProcessName);
            }
            catch (Exception) { }
            finally
            {
                process.Dispose();
            }
        }

        return names;
    }
}
