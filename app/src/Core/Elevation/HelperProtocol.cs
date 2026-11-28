using System.Text.Json.Serialization;
using PCManager.Core.Debloat;

namespace PCManager.Core.Elevation;

/// <summary>
/// Closed command set accepted by the elevated helper. The client can send a command and a
/// rule id; it can never send a path, executable or shell string.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HelperCommand
{
    CleanRule,
    CreateRestorePoint,

    /// <summary>Replace or remove the FPM section of the hosts file (backup written first).</summary>
    ManageHostsBlock,

    /// <summary>Apply one catalog tweak (id validated against the embedded catalog).</summary>
    ApplyTweak,

    /// <summary>Revert one catalog tweak, restoring the journaled pre-state when supplied.</summary>
    RevertTweak,

    /// <summary>Detect one catalog tweak and return its current registry states.</summary>
    DetectTweak
}

/// <summary>One request line on the helper pipe. Maximum 8 KB, enforced by both ends.</summary>
public sealed class HelperRequest
{
    public HelperCommand Command { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Validated DNS names for ManageHostsBlock; empty means remove the block.</summary>
    public List<string> Domains { get; set; } = new();

    /// <summary>Catalog id for ApplyTweak/RevertTweak/DetectTweak.</summary>
    public string TweakId { get; set; } = string.Empty;

    /// <summary>Exact pre-state for RevertTweak; validated against the tweak's own targets.</summary>
    public List<RegistryValueState> Restore { get; set; } = new();

    /// <summary>
    /// Typed user target: empty/"current", "default", a profile name, or "sid:S-1-5-...".
    /// The helper resolves it against ProfileList; no path is accepted here.
    /// </summary>
    public string TargetUser { get; set; } = string.Empty;
}

/// <summary>One response line from the helper.</summary>
public sealed class HelperResponse
{
    public bool Success { get; set; }
    public bool Cancelled { get; set; }
    public long BytesFreed { get; set; }
    public int FilesDeleted { get; set; }
    public int FilesSkipped { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;

    /// <summary>True when a failed apply restored everything it had already changed.</summary>
    public bool RolledBack { get; set; }

    /// <summary>DetectTweak result (null for other commands).</summary>
    public bool? Detected { get; set; }

    /// <summary>Registry states captured by DetectTweak/ApplyTweak.</summary>
    public List<RegistryValueState> Values { get; set; } = new();

    /// <summary>The resolved target the helper used (display text, never a path).</summary>
    public string TargetUser { get; set; } = string.Empty;
}

/// <summary>Client-facing result, independent of the pipe transport.</summary>
public sealed record ElevationOutcome(
    bool Success,
    bool Cancelled,
    long BytesFreed,
    int FilesDeleted,
    int FilesSkipped,
    string Message)
{
    public static ElevationOutcome Failed(string message)
        => new(false, false, 0, 0, 0, message);

    public static ElevationOutcome Declined()
        => new(false, true, 0, 0, 0, "Administrator permission was declined.");
}
