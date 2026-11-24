using PCManager.Core;
using Xunit;

namespace PCManager.Tests;

public sealed class HostsFileContentTests
{
    private const string Original =
        "# Copyright (c) 1993-2009 Microsoft Corp.\r\n" +
        "127.0.0.1 localhost\r\n" +
        "::1 localhost\r\n";

    [Fact]
    public void ApplyBlock_AddsMarkersAndDomains_AndPreservesExistingLines()
    {
        string updated = HostsFileContent.ApplyBlock(Original, new[] { "ads.example.com", "tracker.example.net" });

        Assert.Contains("# BEGIN FPM", updated);
        Assert.Contains("# END FPM", updated);
        Assert.Contains("0.0.0.0 ads.example.com", updated);
        Assert.Contains("0.0.0.0 tracker.example.net", updated);
        Assert.Contains("127.0.0.1 localhost", updated);
        Assert.Contains("::1 localhost", updated);
        Assert.Equal(2, HostsFileContent.CountBlockedDomains(updated));
        Assert.True(HostsFileContent.HasBlock(updated));
        Assert.EndsWith("\r\n", updated);
    }

    [Fact]
    public void ApplyBlock_ReplacesAnExistingBlockInsteadOfAppendingASecondOne()
    {
        string first = HostsFileContent.ApplyBlock(Original, new[] { "old.example.com" });
        string second = HostsFileContent.ApplyBlock(first, new[] { "new.example.com" });

        Assert.Equal(1, CountOccurrences(second, "# BEGIN FPM"));
        Assert.Equal(1, CountOccurrences(second, "# END FPM"));
        Assert.DoesNotContain("old.example.com", second);
        Assert.Contains("0.0.0.0 new.example.com", second);
        Assert.Equal(1, HostsFileContent.CountBlockedDomains(second));
    }

    [Fact]
    public void RemoveBlock_LeavesEveryOtherLineIntact()
    {
        string withBlock = HostsFileContent.ApplyBlock(Original, new[] { "ads.example.com" });
        string removed = HostsFileContent.RemoveBlock(withBlock);

        Assert.False(HostsFileContent.HasBlock(removed));
        Assert.DoesNotContain("ads.example.com", removed);
        Assert.Contains("# Copyright (c) 1993-2009 Microsoft Corp.", removed);
        Assert.Contains("127.0.0.1 localhost", removed);
        Assert.Equal(0, HostsFileContent.CountBlockedDomains(removed));
    }

    [Fact]
    public void RemoveBlock_IsIdempotent()
    {
        string once = HostsFileContent.RemoveBlock(Original);
        string twice = HostsFileContent.RemoveBlock(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void CountBlockedDomains_IgnoresCommentsAndBlankLinesInsideTheBlock()
    {
        const string content =
            "# BEGIN FPM\n" +
            "# a note\n" +
            "\n" +
            "0.0.0.0 one.example.com\n" +
            "0.0.0.0 two.example.com\n" +
            "# END FPM\n" +
            "0.0.0.0 outside.example.com\n";

        Assert.Equal(2, HostsFileContent.CountBlockedDomains(content));
    }

    [Fact]
    public void HasBlock_IsCaseInsensitive()
    {
        Assert.True(HostsFileContent.HasBlock("  # begin fpm  \n0.0.0.0 a.example.com\n# end fpm"));
        Assert.False(HostsFileContent.HasBlock("127.0.0.1 localhost"));
    }

    private static int CountOccurrences(string value, string needle)
    {
        int count = 0;
        int index = 0;

        while ((index = value.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
