using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

/// <summary>How risky a cleanup target is. Safe rows are ticked after a scan; the others are not.</summary>
public enum CleanupTier
{
    Safe,
    Caution,
    OptIn
}

/// <summary>How well the application behind a rule was identified.</summary>
public enum DetectionConfidence
{
    Verified,
    Probable,
    Unverified
}

/// <summary>What the cleaner is allowed to do for one rule.</summary>
public enum CleanupActionKind
{
    DeleteContents,
    EmptyRecycleBin
}

/// <summary>Result of one approved item.</summary>
public enum CleanupItemOutcome
{
    Deleted,
    PartiallyDeleted,
    Quarantined,
    PartiallyQuarantined,
    DryRun,
    SkippedProtected,
    SkippedLocked,
    SkippedElevation,
    SkippedRunning,
    ChangedSinceScan,
    Failed
}

/// <summary>
/// One declarative cleanup rule. Roots are an allow-list: the scanner never widens them, and
/// the safety guard re-validates every path against them before a single file is removed.
/// </summary>
public sealed record CleanupRule
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Application { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>What is deliberately left alone, shown next to the size.</summary>
    public required string SafetyNote { get; init; }

    /// <summary>What happens after cleaning — re-download, slower first launch, and so on.</summary>
    public required string RebuildImpact { get; init; }

    public CleanupTier Tier { get; init; } = CleanupTier.Safe;
    public DetectionConfidence Confidence { get; init; } = DetectionConfidence.Verified;
    public string Evidence { get; init; } = string.Empty;
    public CleanupActionKind Action { get; init; } = CleanupActionKind.DeleteContents;
    public IReadOnlyList<string> Roots { get; init; } = Array.Empty<string>();
    public TimeSpan MinimumAge { get; init; } = TimeSpan.Zero;
    public bool RequiresElevation { get; init; }
    public bool RequiresAppClosed { get; init; }
    public string? ProcessName { get; init; }
}

/// <summary>A measured row in the cleanup report. Numbers come from real enumeration.</summary>
public sealed class CleanupCandidate
{
    public int Number { get; set; }
    public required CleanupRule Rule { get; init; }

    /// <summary>Only roots that actually exist and are not whitelisted.</summary>
    public IReadOnlyList<string> Roots { get; init; } = Array.Empty<string>();

    public long Bytes { get; set; }
    public int FileCount { get; set; }
    public int FolderCount { get; set; }
    public DateTime? OldestWriteUtc { get; set; }
    public DateTime? NewestWriteUtc { get; set; }
    public bool IsSelected { get; set; }
    public bool IsRunning { get; set; }

    public string Title => Rule.Name;
    public string Category => Rule.Category;
    public string Application => Rule.Application;
    public CleanupTier Tier => Rule.Tier;
    public DetectionConfidence Confidence => Rule.Confidence;
    public string ConfidenceText => Rule.Confidence.ToString();
    public bool IsOptIn => Rule.Tier == CleanupTier.OptIn;
    public bool IsCaution => Rule.Tier == CleanupTier.Caution;
    public string SizeText => ByteFormatter.Format(Bytes);
    public string TierText => Rule.Tier switch
    {
        CleanupTier.Safe => "Safe",
        CleanupTier.Caution => "Caution",
        _ => "Opt-in"
    };

    public string PathText => Roots.Count == 0
        ? "Recycle Bin"
        : string.Join(Environment.NewLine, Roots);

    public string DetailText
    {
        get
        {
            var parts = new List<string>
            {
                $"{FileCount:N0} files"
            };

            if (FolderCount > 0)
            {
                parts.Add($"{FolderCount:N0} subfolders");
            }

            if (OldestWriteUtc is { } oldest)
            {
                parts.Add(NewestWriteUtc is { } newest && newest.Date != oldest.Date
                    ? $"modified {oldest.ToLocalTime():yyyy-MM-dd} to {newest.ToLocalTime():yyyy-MM-dd}"
                    : $"modified {oldest.ToLocalTime():yyyy-MM-dd}");
            }

            parts.Add($"{Rule.Confidence} identity");

            if (IsRunning)
            {
                parts.Add("application is running");
            }

            if (Rule.RequiresElevation)
            {
                parts.Add("needs administrator rights");
            }

            return string.Join(" · ", parts);
        }
    }

    public string SafetyText => Rule.SafetyNote;

    public string RebuildText => Rule.RebuildImpact;
}

/// <summary>Everything one read-only scan found.</summary>
public sealed class CleanupScanResult
{
    public IReadOnlyList<CleanupCandidate> Items { get; init; } = Array.Empty<CleanupCandidate>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public DateTime ScannedUtc { get; init; } = DateTime.UtcNow;

    public long SelectedBytes => Items.Where(i => i.IsSelected).Sum(i => i.Bytes);
    public int SelectableCount => Items.Count(i => i.Bytes > 0);

    public string TotalText => $"{ByteFormatter.Format(Items.Sum(i => i.Bytes))} found · {ByteFormatter.Format(SelectedBytes)} selected";
}

/// <summary>An immutable item inside an approved plan.</summary>
public sealed record CleanupPlanItem(
    int Number,
    string RuleId,
    string Name,
    IReadOnlyList<string> Roots,
    long Bytes,
    int FileCount,
    CleanupTier Tier,
    CleanupActionKind Action,
    TimeSpan MinimumAge,
    bool RequiresElevation,
    bool RequiresAppClosed,
    string? ProcessName);

/// <summary>
/// The only object the executor accepts. Created by the command parser (or by the checkbox
/// path through the same parser) and never by free text.
/// </summary>
public sealed class CleanupPlan
{
    public string PlanId { get; init; } = Guid.NewGuid().ToString("N");
    public IReadOnlyList<CleanupPlanItem> Items { get; init; } = Array.Empty<CleanupPlanItem>();
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; init; } = DateTime.UtcNow.AddMinutes(15);

    public long TotalBytes => Items.Sum(i => i.Bytes);
    public string TotalText => ByteFormatter.Format(TotalBytes);
}

public enum CleanupCommandKind
{
    Approved,
    Clarification
}

/// <summary>Parser output: either a plan or a human-readable explanation of the three valid forms.</summary>
public sealed class CleanupCommandResult
{
    public required CleanupCommandKind Kind { get; init; }
    public CleanupPlan? Plan { get; init; }
    public required string Message { get; init; }
}

/// <summary>Per-item execution result.</summary>
public sealed class CleanupItemResult
{
    public int Number { get; init; }
    public required string Name { get; init; }
    public required string PathText { get; init; }
    public long BytesDeleted { get; init; }
    public long BytesRemaining { get; init; }
    public int FilesDeleted { get; init; }
    public int FilesQuarantined { get; init; }
    public long BytesQuarantined { get; init; }
    public int FilesSkipped { get; init; }
    public CleanupItemOutcome Outcome { get; init; }
    public required string Message { get; init; }

    public string OutcomeText => Outcome switch
    {
        CleanupItemOutcome.Deleted => "DELETED",
        CleanupItemOutcome.PartiallyDeleted => "PARTIAL",
        CleanupItemOutcome.Quarantined => "QUARANTINED",
        CleanupItemOutcome.PartiallyQuarantined => "PARTIAL_QUARANTINE",
        CleanupItemOutcome.DryRun => "DRYRUN",
        CleanupItemOutcome.SkippedProtected => "SKIPPED_PROTECTED",
        CleanupItemOutcome.SkippedLocked => "SKIPPED_LOCKED",
        CleanupItemOutcome.SkippedElevation => "SKIPPED_ELEVATION",
        CleanupItemOutcome.SkippedRunning => "SKIPPED_RUNNING",
        CleanupItemOutcome.ChangedSinceScan => "CHANGED_SINCE_SCAN",
        _ => "FAILED"
    };
}

/// <summary>Measured before/after report.</summary>
public sealed class CleanupReport
{
    public IReadOnlyList<CleanupItemResult> Items { get; init; } = Array.Empty<CleanupItemResult>();
    public long EstimatedBytes { get; init; }
    public long BytesDeleted { get; init; }
    public long BytesQuarantined { get; init; }
    public int FilesQuarantined { get; init; }
    public int FilesDeleted { get; init; }
    public int FilesSkipped { get; init; }
    public TimeSpan Duration { get; init; }
    public bool DryRun { get; init; }
    public bool Expired { get; init; }
    public string PlanId { get; init; } = string.Empty;
    public DateTime CompletedUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<string> DriveChanges { get; init; } = Array.Empty<string>();
    public required string Summary { get; init; }

    public int DeletedItems => Items.Count(i => i.Outcome is CleanupItemOutcome.Deleted
        or CleanupItemOutcome.PartiallyDeleted
        or CleanupItemOutcome.Quarantined
        or CleanupItemOutcome.PartiallyQuarantined);
    public int SkippedItems => Items.Count(i => i.Outcome is CleanupItemOutcome.SkippedProtected
        or CleanupItemOutcome.SkippedLocked
        or CleanupItemOutcome.SkippedElevation
        or CleanupItemOutcome.SkippedRunning
        or CleanupItemOutcome.ChangedSinceScan
        or CleanupItemOutcome.Failed);
}
