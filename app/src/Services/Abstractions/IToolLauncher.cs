namespace PCManager.Services.Abstractions;

/// <summary>Launching external programs and settings pages.</summary>
public interface IToolLauncher
{
    /// <summary>Starts a process with <c>UseShellExecute = true</c>.</summary>
    void Start(string fileName, string? arguments = null);

    /// <summary>Opens a <c>ms-settings:</c> page or any URI through the shell.</summary>
    void OpenUri(string uri);

    /// <summary>Opens a folder in Explorer.</summary>
    void OpenFolder(string path);
}
