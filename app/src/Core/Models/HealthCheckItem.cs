namespace PCManager.Core.Models;

/// <summary>
/// Severity of a single health check result.
/// </summary>
public enum HealthItemStatus
{
    Good = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>
/// One result row produced by an <c>IHealthCheckModule</c>.
/// A module normally returns a single aggregate row (for example "Temporary files:
/// 412 MB") rather than one row per file, so the UI stays readable and the scan
/// stays fast.
/// </summary>
public sealed class HealthCheckItem
{
    /// <summary>Stable identifier of the row, e.g. <c>temp-files</c>.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Identifier of the module that produced the row.</summary>
    public string ModuleId { get; init; } = string.Empty;

    /// <summary>Short title shown on the card.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>One-line explanation of the finding.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Longer diagnostic detail (paths, counts, registry keys...).</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Current severity.</summary>
    public HealthItemStatus Status { get; set; } = HealthItemStatus.Good;

    /// <summary>True when the module knows how to repair the finding.</summary>
    public bool CanFix { get; set; }

    /// <summary>True once the repair has been applied successfully.</summary>
    public bool FixApplied { get; set; }

    /// <summary>Reclaimable (or affected) bytes, used for the "free up space" total.</summary>
    public long Bytes { get; set; }

    /// <summary>Whether the user ticked this row for the next cleanup run.</summary>
    public bool IsSelected { get; set; } = true;
}
