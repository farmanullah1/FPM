using System.Diagnostics;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Toolbox;

/// <summary>Launches programs, settings pages and folders through the shell.</summary>
public sealed class ToolLauncher : IToolLauncher
{
    public void Start(string fileName, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    public void OpenUri(string uri)
    {
        // ms-settings: pages must go through the shell (explorer) to be routed to Settings.
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
