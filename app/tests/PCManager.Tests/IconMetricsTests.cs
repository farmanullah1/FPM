using PCManager.Core.Design;
using Xunit;

namespace PCManager.Tests;

public sealed class IconMetricsTests
{
    [Theory]
    [InlineData(16, IconSizeClass.Small)]
    [InlineData(20, IconSizeClass.Small)]
    [InlineData(24, IconSizeClass.Small)]
    [InlineData(27.9, IconSizeClass.Small)]
    [InlineData(28, IconSizeClass.Medium)]
    [InlineData(32, IconSizeClass.Medium)]
    [InlineData(48, IconSizeClass.Medium)]
    [InlineData(55.9, IconSizeClass.Medium)]
    [InlineData(56, IconSizeClass.Large)]
    [InlineData(64, IconSizeClass.Large)]
    [InlineData(96, IconSizeClass.Large)]
    public void ForSize_picks_the_documented_detail_level(double size, IconSizeClass expected) =>
        Assert.Equal(expected, IconMetrics.ForSize(size));

    [Fact]
    public void GeometryKey_appends_the_role_and_omits_an_empty_one()
    {
        Assert.Equal("Icon3D.Memory.Face", IconMetrics.GeometryKey("Memory", "Face"));
        Assert.Equal("Icon3D.Memory.Neutral", IconMetrics.GeometryKey("Memory", "Neutral"));
        Assert.Equal("Icon3D.Memory", IconMetrics.GeometryKey("Memory", ""));
    }

    [Theory]
    [InlineData("Memory", "Memory")]
    [InlineData("Icon3D.Memory", "Memory")]
    [InlineData("  Icon3D.Boost  ", "Boost")]
    [InlineData(null, "")]
    public void NormalizeKey_accepts_prefixed_and_plain_names(string? input, string expected) =>
        Assert.Equal(expected, IconMetrics.NormalizeKey(input));

    [Theory]
    [InlineData("Memory", "Memory")]
    [InlineData("MemorySnapshot", "Memory Snapshot")]
    [InlineData("Icon3D.AiTools", "Ai Tools")]
    [InlineData("", "Icon")]
    public void AutomationName_splits_pascal_case_and_never_returns_empty(string key, string expected) =>
        Assert.Equal(expected, IconMetrics.AutomationName(key));
}
