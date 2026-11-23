using PCManager.Core.Models;
using Xunit;

namespace PCManager.Tests;

public sealed class CleanupProfileTests
{
    [Fact]
    public void Quick_clean_selects_only_safe_rows()
    {
        Assert.True(Includes(CleanupProfileKind.QuickClean, "browser.chrome", CleanupTier.Safe));
        Assert.False(Includes(CleanupProfileKind.QuickClean, "windows.logs", CleanupTier.Caution));
        Assert.False(Includes(CleanupProfileKind.QuickClean, "windows.patchcache", CleanupTier.OptIn));
    }

    [Fact]
    public void Developer_clean_selects_safe_developer_and_ide_rows_only()
    {
        Assert.True(Includes(CleanupProfileKind.DeveloperClean, "dev.npm", CleanupTier.Safe));
        Assert.True(Includes(CleanupProfileKind.DeveloperClean, "ai.cursor", CleanupTier.Safe));
        Assert.False(Includes(CleanupProfileKind.DeveloperClean, "dev.pnpm", CleanupTier.Caution));
        Assert.False(Includes(CleanupProfileKind.DeveloperClean, "browser.chrome", CleanupTier.Safe));
    }

    [Fact]
    public void Ai_and_browser_profiles_select_their_own_safe_families()
    {
        Assert.True(Includes(CleanupProfileKind.AiWorkspaceClean, "ai.cursor", CleanupTier.Safe));
        Assert.False(Includes(CleanupProfileKind.AiWorkspaceClean, "browser.chrome", CleanupTier.Safe));
        Assert.False(Includes(CleanupProfileKind.AiWorkspaceClean, "ai.cursor", CleanupTier.Caution));

        Assert.True(Includes(CleanupProfileKind.BrowserClean, "browser.firefox", CleanupTier.Safe));
        Assert.False(Includes(CleanupProfileKind.BrowserClean, "dev.npm", CleanupTier.Safe));
    }

    [Fact]
    public void Windows_clean_excludes_caution_and_opt_in()
    {
        Assert.True(Includes(CleanupProfileKind.WindowsClean, "windows.update-cache", CleanupTier.Safe));
        Assert.False(Includes(CleanupProfileKind.WindowsClean, "windows.logs", CleanupTier.Caution));
        Assert.False(Includes(CleanupProfileKind.WindowsClean, "windows.patchcache", CleanupTier.OptIn));
        Assert.False(Includes(CleanupProfileKind.WindowsClean, "dev.npm", CleanupTier.Safe));
    }

    [Fact]
    public void Deep_review_ticks_caution_but_not_opt_in_or_running_apps()
    {
        Assert.True(Includes(CleanupProfileKind.DeepReview, "windows.logs", CleanupTier.Caution));
        Assert.False(Includes(CleanupProfileKind.DeepReview, "windows.patchcache", CleanupTier.OptIn));

        CleanupCandidate running = Candidate("browser.chrome", CleanupTier.Safe, isRunning: true);
        Assert.False(CleanupProfileCatalog.Includes(CleanupProfileKind.DeepReview, running));
    }

    [Fact]
    public void Every_profile_has_a_unique_id_and_a_hint()
    {
        Assert.Equal(
            CleanupProfileCatalog.All.Count,
            CleanupProfileCatalog.All.Select(profile => profile.Id).Distinct().Count());

        Assert.All(CleanupProfileCatalog.All, profile =>
        {
            Assert.False(string.IsNullOrWhiteSpace(profile.Name));
            Assert.False(string.IsNullOrWhiteSpace(profile.Hint));
        });
    }

    private static bool Includes(CleanupProfileKind profile, string ruleId, CleanupTier tier)
        => CleanupProfileCatalog.Includes(profile, Candidate(ruleId, tier, isRunning: false));

    private static CleanupCandidate Candidate(string ruleId, CleanupTier tier, bool isRunning)
        => new()
        {
            Rule = new CleanupRule
            {
                Id = ruleId,
                Category = "Test",
                Application = "Test",
                Name = ruleId,
                Description = "Test",
                SafetyNote = "Test",
                RebuildImpact = "Test",
                Tier = tier
            },
            IsRunning = isRunning
        };
}
