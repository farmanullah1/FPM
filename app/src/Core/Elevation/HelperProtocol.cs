using System.Text.Json.Serialization;

namespace PCManager.Core.Elevation;

/// <summary>
/// Closed command set accepted by the elevated helper. The client can send a command and a
/// rule id; it can never send a path, executable or shell string.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HelperCommand
{
    CleanRule,
    CreateRestorePoint
}

/// <summary>One request line on the helper pipe. Maximum 8 KB, enforced by both ends.</summary>
public sealed class HelperRequest
{
    public HelperCommand Command { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
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
