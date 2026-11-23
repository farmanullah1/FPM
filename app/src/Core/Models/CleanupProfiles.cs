namespace PCManager.Core.Models;

public enum CleanupProfileKind
{
    QuickClean,
    DeveloperClean,
    AiWorkspaceClean,
    BrowserClean,
    WindowsClean,
    DeepReview
}

/// <summary>A review preset. Applying one only changes which rows are ticked.</summary>
public sealed record CleanupProfile(
    string Id,
    string Name,
    string Hint,
    CleanupProfileKind Kind);

public static class CleanupProfileCatalog
{
    private static readonly CleanupProfile[] Profiles =
    {
        new(
            "quick",
            "Quick Clean",
            "Safe-tier temporary files and disposable caches only. Caution and Opt-in rows stay unticked.",
            CleanupProfileKind.QuickClean),
        new(
            "developer",
            "Developer Clean",
            "Developer, IDE and package-manager caches: dev.* and ai.* Safe rows. Caution rows stay unticked.",
            CleanupProfileKind.DeveloperClean),
        new(
            "ai",
            "AI Workspace Clean",
            "Supported AI IDE/agent caches and logs only. Prompts, chat history, workspace state, models and credentials are never roots.",
            CleanupProfileKind.AiWorkspaceClean),
        new(
            "browsers",
            "Browser Clean",
            "Edge, Chrome, Brave and Firefox caches only. Cookies, history and sessions are never touched. Close the browser first.",
            CleanupProfileKind.BrowserClean),
        new(
            "windows",
            "Windows Clean",
            "Safe-tier Windows temp, Update, reports, shader and Recycle Bin rows. Logs and Prefetch stay unticked.",
            CleanupProfileKind.WindowsClean),
        new(
            "deep",
            "Deep Review",
            "Ticks every Safe and Caution row so every category can be reviewed. Opt-in and running-app rows stay unticked; approval is still required.",
            CleanupProfileKind.DeepReview)
    };

    public static IReadOnlyList<CleanupProfile> All => Profiles;

    public static bool Includes(CleanupProfileKind profile, CleanupCandidate candidate)
    {
        if (candidate.Tier == CleanupTier.OptIn || candidate.IsRunning)
        {
            return false;
        }

        string id = candidate.Rule.Id;

        return profile switch
        {
            CleanupProfileKind.QuickClean => candidate.Tier == CleanupTier.Safe,
            CleanupProfileKind.DeveloperClean =>
                candidate.Tier == CleanupTier.Safe &&
                (id.StartsWith("dev.", StringComparison.OrdinalIgnoreCase) ||
                 id.StartsWith("ai.", StringComparison.OrdinalIgnoreCase)),
            CleanupProfileKind.AiWorkspaceClean =>
                candidate.Tier == CleanupTier.Safe &&
                id.StartsWith("ai.", StringComparison.OrdinalIgnoreCase),
            CleanupProfileKind.BrowserClean =>
                candidate.Tier == CleanupTier.Safe &&
                id.StartsWith("browser.", StringComparison.OrdinalIgnoreCase),
            CleanupProfileKind.WindowsClean =>
                candidate.Tier == CleanupTier.Safe &&
                id.StartsWith("windows.", StringComparison.OrdinalIgnoreCase),
            CleanupProfileKind.DeepReview => true,
            _ => false
        };
    }
}
