namespace PCManager.Core;

/// <summary>
/// Product identity in one place.
///
/// Everything here is deliberately distinct from Microsoft PC Manager so the two
/// utilities can be installed side by side: different executable name, different
/// single-instance mutex, different settings folder, different registry root and a
/// different Run value name. No URI protocol handler is registered.
/// </summary>
public static class AppInfo
{
    /// <summary>Name shown in the window title, tray tooltip and About card.</summary>
    public const string ProductName = "Farmanullah PC Manager";

    /// <summary>Assembly / executable name (FarmanullahPCManager.exe).</summary>
    public const string ExecutableName = "FarmanullahPCManager";

    /// <summary>Per-user AppUserModelID used for taskbar grouping and notifications.</summary>
    public const string AppUserModelId = "FarmanullahPCManager.Desktop.8A4F2C1E";

    /// <summary>
    /// Global\... mutex: "Global\" makes it machine-wide so a second launch in another
    /// session cannot start a duplicate copy either.
    /// </summary>
    public const string SingleInstanceMutexName = @"Global\FarmanullahPCManager_SingleInstance_8A4F2C1E";

    /// <summary>Folder name under %LOCALAPPDATA%.</summary>
    public const string DataFolderName = "FarmanullahPCManager";

    /// <summary>HKCU root for every value this app writes.</summary>
    public const string RegistryRoot = @"Software\FarmanullahPCManager";

    /// <summary>Value name used under the per-user Run key (never "PCManager").</summary>
    public const string RunValueName = "FarmanullahPCManager";

    /// <summary>HKCU Run key path.</summary>
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Build number mirrored in the UI.</summary>
    public const string Version = "3.23.3";

    /// <summary>%LOCALAPPDATA%\FarmanullahPCManager — settings, logs and caches.</summary>
    public static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        DataFolderName);

    /// <summary>%LOCALAPPDATA%\FarmanullahPCManager\settings.json</summary>
    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

    /// <summary>%LOCALAPPDATA%\FarmanullahPCManager\logs</summary>
    public static string LogFolder => Path.Combine(DataFolder, "logs");

    /// <summary>%LOCALAPPDATA%\FarmanullahPCManager\cache</summary>
    public static string CacheFolder => Path.Combine(DataFolder, "cache");

    /// <summary>Process names that must never be trimmed or terminated by this app.</summary>
    public static readonly string[] ProtectedProcessNames =
    {
        "MSPCManager",          // Microsoft PC Manager shell
        "MSPCManagerService",   // Microsoft PC Manager service
        "MPCMDRUN",             // Microsoft PC Manager helper
        "FarmanullahPCManager"  // us
    };

    /// <summary>
    /// True for our own process and for every Microsoft PC Manager process. Working-set
    /// trims and End Task skip these so neither utility can freeze or kill the other.
    /// </summary>
    public static bool IsProtectedProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return ProtectedProcessNames.Any(name =>
            processName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            (name.StartsWith("MSPCManager", StringComparison.OrdinalIgnoreCase) &&
             processName.StartsWith("MSPCManager", StringComparison.OrdinalIgnoreCase)));
    }
}
