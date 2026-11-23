using System.Text.Json.Serialization;
using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

/// <summary>
/// One file moved out of a cleanup target into the app's quarantine. The manifest keeps enough
/// information to restore it: where it came from, where it is stored, when, its size and a
/// SHA-256 for files small enough to hash cheaply.
/// </summary>
public sealed record QuarantineEntry(
    string Id,
    string OriginalPath,
    string StoredPath,
    DateTime QuarantinedUtc,
    string Sha256,
    long Bytes,
    string RuleId,
    string Reason)
{
    [JsonIgnore]
    public string Name => Path.GetFileName(OriginalPath);

    [JsonIgnore]
    public string SizeText => ByteFormatter.Format(Bytes);

    [JsonIgnore]
    public string QuarantinedText => QuarantinedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    [JsonIgnore]
    public string HashText => string.IsNullOrEmpty(Sha256) ? "not hashed" : Sha256;

    [JsonIgnore]
    public string DetailText => $"{SizeText} · {QuarantinedText} · {Reason}";
}

/// <summary>What one retention pass actually removed from the quarantine.</summary>
public sealed record QuarantinePurgeResult(int EntriesRemoved, long BytesRemoved)
{
    public string Message => EntriesRemoved == 0
        ? "Nothing in the quarantine passed the retention or size limit."
        : $"Removed {EntriesRemoved} quarantined file(s), {ByteFormatter.Format(BytesRemoved)}.";
}
