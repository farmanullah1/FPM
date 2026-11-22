namespace PCManager.Services.Abstractions;

/// <summary>
/// User-owned "never touch" list stored at
/// %LOCALAPPDATA%\FarmanullahPCManager\whitelist.json. Whitelisted folders are excluded from
/// scans and refused by the safety guard even when a rule would otherwise match.
/// </summary>
public interface IWhitelistService
{
    string FilePath { get; }

    IReadOnlyList<string> GetEntries();

    bool IsWhitelisted(string path);

    /// <summary>Re-reads the file so a hand edit takes effect without restarting.</summary>
    void Reload();

    void Add(string path);

    bool Remove(string path);
}
