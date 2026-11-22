using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Built-in cleanup rules. Each rule owns a fixed root list and the junk sub-folders it may
/// measure; nothing here reads user patterns or commands from disk.
/// </summary>
public interface ICleanupRuleCatalog
{
    IReadOnlyList<CleanupRule> GetRules();

    /// <summary>Discovery outcomes worth showing, e.g. an AI app with no supported cache.</summary>
    IReadOnlyList<string> GetDiscoveryNotes();
}
