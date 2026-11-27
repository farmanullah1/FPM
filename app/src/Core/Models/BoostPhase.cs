namespace PCManager.Core.Models;

/// <summary>Shared Boost state machine (U7/W5): every Boost surface renders the same phases.</summary>
public enum BoostPhase
{
    Idle,
    Measuring,
    Working,
    Completed,
    Failed,
}

/// <summary>What started a Boost run.</summary>
public enum BoostTrigger
{
    /// <summary>The user pressed Boost: trim, then release temporary files.</summary>
    Manual,

    /// <summary>Smart Boost fired automatically: trim only, never delete files unattended.</summary>
    Automatic,
}

/// <summary>
/// The one confirmation rule for temporary files: they are only deleted by an explicit Boost press.
/// An automatic Smart Boost trims working sets, which is reversible by the OS, and touches no files.
/// </summary>
public static class BoostPolicy
{
    public static bool AllowsTempCleanup(BoostTrigger trigger) => trigger == BoostTrigger.Manual;

    public static bool IsBusy(BoostPhase phase) => phase is BoostPhase.Measuring or BoostPhase.Working;
}
