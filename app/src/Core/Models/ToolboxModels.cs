namespace PCManager.Core.Models;

/// <summary>An entry in the Toolbox grid.</summary>
public sealed class ToolboxTool
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
}

/// <summary>The browser Windows currently uses for http/https links.</summary>
public sealed class DefaultBrowserSetting
{
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string ProgId { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

/// <summary>A shortcut pinned to the taskbar.</summary>
public sealed class TaskbarPinEntry
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsRunning { get; set; }
}

/// <summary>
/// Persistent, user-editable application settings.
/// </summary>
public sealed class AppSettings
{
    public ThemeOption Theme { get; set; } = ThemeOption.System;
    public bool AutoRefreshProcessList { get; set; } = true;
    public long LargeFileThresholdBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>Mirrors the HKCU Run value so the Settings toggle survives restarts.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>"Smart Boost" trims memory automatically when load passes the threshold.</summary>
    public bool SmartBoostEnabled { get; set; } = true;

    /// <summary>Memory load percentage that triggers a Smart Boost.</summary>
    public int SmartBoostThresholdPercent { get; set; } = 75;

    /// <summary>Shows the always-on-top floating toolbar pill.</summary>
    public bool ShowFloatingToolbar { get; set; }
}

/// <summary>Mirrors the ThemeOption enum found in the original shell assembly.</summary>
public enum ThemeOption
{
    Default = 0,
    Light = 1,
    Dark = 2,
    System = 3
}
