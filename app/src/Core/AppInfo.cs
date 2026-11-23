namespace PCManager.Core;

/// <summary>
/// Product identity in one place.
///
/// Everything here is deliberately distinct from Microsoft PC Manager so the two
/// utilities can be installed side by side: different executable name, different
/// single-instance mutex, different settings folder, different registry root and a
/// different Run value name.
/// </summary>
public static class AppInfo
{
    /// <summary>Name shown in the window title, tray tooltip and About card.</summary>
    public const string ProductName = "FPM";

    /// <summary>Product subtitle / short description.</summary>
    public const string ProductSubtitle = "Windows PC Manager";

    /// <summary>Per-user AppUserModelID used for taskbar grouping and notifications.</summary>
    public const string AppUserModelId = "FPM.Desktop.8A4F2C1E";

    /// <summary>
    /// Global\... mutex: "Global\" makes it machine-wide so a second launch in another
    /// session cannot start a duplicate copy either.
    /// </summary>
    public const string SingleInstanceMutexName = @"Global\FPM_SingleInstance_8A4F2C1E";

    /// <summary>Folder name under %LOCALAPPDATA%.</summary>
    public const string DataFolderName = "FPM";

    /// <summary>Value name used under the per-user Run key (never "PCManager").</summary>
    public const string RunValueName = "FPM";

    /// <summary>HKCU Run key path.</summary>
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Registry root path for FPM in HKCU.</summary>
    public const string RegistryRootKey = @"Software\FPM";

    /// <summary>Build number mirrored in the UI.</summary>
    public const string Version = "3.23.3";

    /// <summary>%LOCALAPPDATA%\FPM — settings, logs and caches.</summary>
    public static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        DataFolderName);

    /// <summary>%LOCALAPPDATA%\FPM\settings.json</summary>
    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

    /// <summary>%LOCALAPPDATA%\FPM\logs</summary>
    public static string LogFolder => Path.Combine(DataFolder, "logs");

    /// <summary>Legacy folder name under %LOCALAPPDATA% for migration.</summary>
    public const string LegacyDataFolderName = "FarmanullahPCManager";

    /// <summary>Legacy single instance mutex name.</summary>
    public const string LegacySingleInstanceMutexName = @"Global\FarmanullahPCManager_SingleInstance_8A4F2C1E";

    /// <summary>Process names that must never be trimmed or terminated by this app.</summary>
    public static readonly string[] ProtectedProcessNames =
    {
        "MSPCManager",              // Microsoft PC Manager shell
        "MSPCManagerService",       // Microsoft PC Manager service
        "MPCMDRUN",                 // Microsoft PC Manager helper
        "FarmanullahPCManager",      // legacy identity
        "FarmanullahPCManager.Helper",
        "FPM",                      // us
        "FPM.Helper",
        "System",
        "Idle",
        "Registry",
        "smss",
        "csrss",
        "wininit",
        "winlogon",
        "services",
        "lsass",
        "svchost",
        "dwm",
        "explorer",
        "fontdrvhost",
        "MsMpEng",
        "SecurityHealthService",
        "audiodg"
    };

    /// <summary>
    /// True for our own process, Windows critical processes, and Microsoft PC Manager processes.
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
             processName.StartsWith("MSPCManager", StringComparison.OrdinalIgnoreCase)) ||
            (name.StartsWith("FPM", StringComparison.OrdinalIgnoreCase) &&
             processName.StartsWith("FPM", StringComparison.OrdinalIgnoreCase)));
    }
}
