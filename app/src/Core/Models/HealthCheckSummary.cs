using System.Text.Json.Serialization;

namespace PCManager.Core.Models;

/// <summary>
/// Aggregated result of one health check run.
/// </summary>
public sealed class HealthCheckSummary
{
    /// <summary>When the scan finished.</summary>
    public DateTime ScannedAt { get; set; } = DateTime.Now;

    /// <summary>How long the scan took.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Every row returned by the registered modules.</summary>
    public List<HealthCheckItem> Items { get; set; } = new();

    /// <summary>Bytes that were actually released by the last cleanup run.</summary>
    public long BytesFreed { get; set; }

    /// <summary>Rows that are not in the "Good" state.</summary>
    [JsonIgnore]
    public int TotalIssues => Items.Count(i => i.Status != HealthItemStatus.Good);

    /// <summary>Rows the user can still fix.</summary>
    [JsonIgnore]
    public int TotalFixable => Items.Count(i => i.CanFix && !i.FixApplied);

    /// <summary>Reclaimable bytes across all fixable rows.</summary>
    [JsonIgnore]
    public long ReclaimableBytes => Items.Where(i => i.CanFix && !i.FixApplied).Sum(i => i.Bytes);

    /// <summary>
    /// Coarse 0-100 health score. Each warning costs 6 points and each critical
    /// finding costs 12 points, with a floor of 10 so the gauge never reads zero
    /// for a merely messy machine.
    /// </summary>
    [JsonIgnore]
    public int HealthScore
    {
        get
        {
            int penalty = Items.Sum(i => i.Status switch
            {
                HealthItemStatus.Critical => 12,
                HealthItemStatus.Warning => 6,
                _ => 0
            });
            return Math.Clamp(100 - penalty, 10, 100);
        }
    }
}
