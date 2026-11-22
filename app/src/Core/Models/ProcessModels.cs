using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

/// <summary>Snapshot of a running process.</summary>
public sealed class ProcessInfo
{
    public int ProcessId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>CPU usage of this process since the previous sample, in percent of all cores.</summary>
    public double CpuPercent { get; set; }

    /// <summary>Working set in bytes.</summary>
    public long MemoryBytes { get; set; }

    /// <summary>True for session-0 / critical processes the UI will not kill.</summary>
    public bool IsSystem { get; set; }

    // Display helpers so the XAML does not need value converters.
    public string CpuText => $"{CpuPercent:0.0}%";
    public string MemoryText => ByteFormatter.Format(MemoryBytes);
}

/// <summary>Where a startup entry is registered.</summary>
public enum StartupSource
{
    RegistryCurrentUser,
    RegistryLocalMachine,
    TaskScheduler,
    StartupFolder
}

/// <summary>
/// A program that starts with Windows, from the Run keys, the Startup folders or
/// Task Scheduler.
/// </summary>
public sealed class StartupApp
{
    /// <summary>Opaque identity used by the UI, e.g. <c>run:HKCU:OneDrive:Software\...</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Launch command line as stored by Windows.</summary>
    public string Command { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public StartupSource Source { get; set; }

    /// <summary>True for tasks under \Microsoft\ - these must not be toggled.</summary>
    public bool IsSystem { get; set; }

    // --- Fields used by the registry / task scheduler implementations ---

    /// <summary>"HKCU", "HKLM" or "TASK".</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Registry sub-key path or task folder path.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Registry value name, shortcut file name or task name.</summary>
    public string EntryName { get; set; } = string.Empty;
}

/// <summary>Result of an operation the UI has to report back to the user.</summary>
public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
