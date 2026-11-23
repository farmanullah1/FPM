using System.Text.Json.Serialization;
using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

/// <summary>One completed cleanup run, kept so the user can audit what happened.</summary>
public sealed record CleanupHistoryEntry(
    DateTime CompletedUtc,
    string PlanId,
    bool DryRun,
    long EstimatedBytes,
    long BytesDeleted,
    int FilesDeleted,
    int FilesSkipped,
    string Summary)
{
    [JsonIgnore]
    public string CompletedText => CompletedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    [JsonIgnore]
    public string SizeText => ByteFormatter.Format(BytesDeleted);

    [JsonIgnore]
    public string ModeText => DryRun ? "dry run" : "cleaned";

    [JsonIgnore]
    public string DisplayText =>
        $"{CompletedText} · {ModeText} · {SizeText} · {FilesDeleted:N0} file(s)" +
        (FilesSkipped > 0 ? $" · {FilesSkipped:N0} skipped" : string.Empty);
}
