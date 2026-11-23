using PCManager.Services.Cleanup;
using Xunit;

namespace PCManager.Tests;

public sealed class CleanupPlanFreshnessTests
{
    [Fact]
    public void Unchanged_folder_is_fresh()
    {
        Assert.False(CleanupPlanFreshness.IsStale(
            expectedBytes: 100 * 1024 * 1024,
            expectedFiles: 1_000,
            actualBytes: 100 * 1024 * 1024,
            actualFiles: 1_000));
    }

    [Fact]
    public void Ordinary_churn_within_tolerance_is_fresh()
    {
        Assert.False(CleanupPlanFreshness.IsStale(
            expectedBytes: 100 * 1024 * 1024,
            expectedFiles: 1_000,
            actualBytes: 103 * 1024 * 1024,
            actualFiles: 990));
    }

    [Fact]
    public void Material_byte_growth_is_stale()
    {
        Assert.True(CleanupPlanFreshness.IsStale(
            expectedBytes: 100 * 1024 * 1024,
            expectedFiles: 1_000,
            actualBytes: 120 * 1024 * 1024,
            actualFiles: 1_000));
    }

    [Fact]
    public void Material_file_count_change_is_stale_even_when_size_is_similar()
    {
        Assert.True(CleanupPlanFreshness.IsStale(
            expectedBytes: 100 * 1024 * 1024,
            expectedFiles: 1_000,
            actualBytes: 100 * 1024 * 1024,
            actualFiles: 1_100));
    }

    [Fact]
    public void Small_folder_uses_the_minimum_byte_tolerance()
    {
        Assert.True(CleanupPlanFreshness.IsStale(
            expectedBytes: 100 * 1024,
            expectedFiles: 2,
            actualBytes: 200 * 1024,
            actualFiles: 2));

        Assert.False(CleanupPlanFreshness.IsStale(
            expectedBytes: 100 * 1024,
            expectedFiles: 2,
            actualBytes: 110 * 1024,
            actualFiles: 2));
    }

    [Fact]
    public void An_unmeasurable_zero_baseline_does_not_invalidate_elevated_rows()
    {
        Assert.False(CleanupPlanFreshness.IsStale(0, 0, 5 * 1024 * 1024, 12));
    }
}
