namespace PCManager.Core.Models;

/// <summary>One background app that currently owns a visible top-level window.</summary>
public sealed record PopupSourceInfo(
    string ProcessName,
    string WindowTitle,
    int ProcessId,
    int WindowCount,
    bool IsAllowed,
    int SeenCount)
{
    /// <summary>Local 30-day sighting count; never transmitted.</summary>
    public string SeenLabel => SeenCount <= 1
        ? "First sighting"
        : $"Seen {SeenCount}× in 30 days";
}

/// <summary>Read-only view of the FPM section in the Windows hosts file.</summary>
public sealed record HostsBlockState(int BlockedDomains, string Detail)
{
    public static HostsBlockState None(string detail) => new(0, detail);
}
