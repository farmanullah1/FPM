using PCManager.Core.Android;
using PCManager.ViewModels;
using Xunit;

namespace PCManager.Tests;

public sealed class AndroidPackageRowTests
{
    [Fact]
    public void Row_exposes_metadata_and_selection_change()
    {
        var package = new AndroidPackage(
            "com.example.app",
            PackageState.Enabled,
            true,
            new UadPackageEntry(
                "com.example.app",
                UadListKind.Misc,
                "Example package",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                UadRemovalTier.Advanced));
        var row = new AndroidPackageRow(package);
        bool changed = false;
        row.SelectionChanged += () => changed = true;

        row.IsSelected = true;

        Assert.True(changed);
        Assert.Equal("com.example.app", row.Id);
        Assert.Equal("Misc", row.ListText);
        Assert.Equal("Advanced", row.RemovalText);
        Assert.Equal("Enabled", row.StateText);
        Assert.Equal("Example package", row.Description);
    }

    [Theory]
    [InlineData("com.android.systemui")]
    [InlineData("com.android.settings")]
    public void Critical_rows_are_denied(string id)
    {
        var row = new AndroidPackageRow(
            new AndroidPackage(id, PackageState.Enabled, true, null));

        Assert.True(row.Package.IsDenylisted());
    }

    [Fact]
    public void Unsafe_metadata_is_denied()
    {
        var package = new AndroidPackage(
            "com.example.unsafe",
            PackageState.Enabled,
            false,
            new UadPackageEntry(
                "com.example.unsafe",
                UadListKind.Unlisted,
                "Unsafe package",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                UadRemovalTier.Unsafe));

        Assert.True(package.IsDenylisted());
    }
}
