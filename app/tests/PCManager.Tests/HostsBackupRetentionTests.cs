using PCManager.Core;
using Xunit;

namespace PCManager.Tests;

public sealed class HostsBackupRetentionTests
{
    private static string[] Backups(int count)
        => Enumerable.Range(0, count)
            .Select(index => $@"C:\Users\test\AppData\Local\FPM\backups\hosts-202601{index + 1:00}-120000.hosts")
            .ToArray();

    [Fact]
    public void SelectObsolete_KeepsOnlyTheNewestBackups()
    {
        string[] backups = Backups(25);

        IReadOnlyList<string> obsolete = HostsBackupRetention.SelectObsolete(backups, keep: 20);

        Assert.Equal(5, obsolete.Count);
        Assert.Contains(backups[0], obsolete);
        Assert.Contains(backups[4], obsolete);
        Assert.DoesNotContain(backups[5], obsolete);
        Assert.DoesNotContain(backups[^1], obsolete);
    }

    [Fact]
    public void SelectObsolete_KeepsEverythingWhenTheLimitIsNotReached()
    {
        Assert.Empty(HostsBackupRetention.SelectObsolete(Backups(3), keep: 20));
    }

    [Fact]
    public void SelectObsolete_IgnoresFilesTheHelperDidNotWrite()
    {
        string[] paths =
        {
            @"C:\backups\hosts-20260101-120000.hosts",
            @"C:\backups\notes.txt",
            @"C:\backups\hosts.bak",
            @"C:\backups\hosts-2026.hosts",
            @"C:\backups\hosts-20260101-120000.hosts.bak"
        };

        IReadOnlyList<string> obsolete = HostsBackupRetention.SelectObsolete(paths, keep: 1);

        Assert.Empty(obsolete);
    }

    [Fact]
    public void SelectObsolete_NeverKeepsNothing()
    {
        string[] backups = Backups(2);

        IReadOnlyList<string> obsolete = HostsBackupRetention.SelectObsolete(backups, keep: 0);

        Assert.Single(obsolete);
        Assert.Equal(backups[0], obsolete[0]);
    }

    [Theory]
    [InlineData("hosts-20260101-120000.hosts", true)]
    [InlineData("HOSTS-20260101-120000.HOSTS", true)]
    [InlineData("hosts-20260101-120000.hosts.bak", false)]
    [InlineData("hosts-2026010-120000.hosts", false)]
    [InlineData("hosts-backup.hosts", false)]
    public void IsBackupName_MatchesOnlyTimestampedHostsBackups(string fileName, bool expected)
        => Assert.Equal(expected, HostsBackupRetention.IsBackupName(fileName));
}
