using System.Text.Json;
using PCManager.Core;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Cleanup;

/// <summary>
/// whitelist.json in the app's own data folder. Format:
/// <code>{ "paths": [ "C:\\Projects", "D:\\Source" ] }</code>
/// A missing or unreadable file means an empty list; the hard deny-list still applies.
/// </summary>
public sealed class WhitelistService : IWhitelistService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private List<string> _entries = new();

    public WhitelistService(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(AppInfo.DataFolder, "whitelist.json");
        Reload();
    }

    public string FilePath { get; }

    public IReadOnlyList<string> GetEntries()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    public bool IsWhitelisted(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string full;

        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            return false;
        }

        List<string> entries;

        lock (_gate)
        {
            entries = _entries.ToList();
        }

        return entries.Any(entry => IsSameOrUnder(full, entry));
    }

    public void Reload()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                lock (_gate)
                {
                    _entries = new List<string>();
                }

                return;
            }

            string json = File.ReadAllText(FilePath);
            WhitelistDocument? document = JsonSerializer.Deserialize<WhitelistDocument>(json, JsonOptions);

            var loaded = new List<string>();

            foreach (string? entry in document?.Paths ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                try
                {
                    string normalised = Path.TrimEndingDirectorySeparator(Path.GetFullPath(entry.Trim()));

                    if (!loaded.Contains(normalised, StringComparer.OrdinalIgnoreCase))
                    {
                        loaded.Add(normalised);
                    }
                }
                catch
                {
                    // Ignore one malformed entry rather than failing the whole list.
                }
            }

            lock (_gate)
            {
                _entries = loaded;
            }
        }
        catch
        {
            // Unreadable file: keep the previous list. Never invent entries.
        }
    }

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string full;

        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            return;
        }

        List<string> snapshot;

        lock (_gate)
        {
            if (_entries.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            _entries.Add(full);
            snapshot = _entries.ToList();
        }

        Save(snapshot);
    }

    public bool Remove(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string full;

        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            return false;
        }

        List<string> snapshot;
        bool removed;

        lock (_gate)
        {
            removed = _entries.RemoveAll(entry => entry.Equals(full, StringComparison.OrdinalIgnoreCase)) > 0;
            snapshot = _entries.ToList();
        }

        if (removed)
        {
            Save(snapshot);
        }

        return removed;
    }

    private void Save(List<string> entries)
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new WhitelistDocument { Paths = entries }, JsonOptions));
        }
        catch
        {
            // A read-only profile still gets a working in-memory whitelist.
        }
    }

    private static bool IsSameOrUnder(string path, string root)
    {
        string candidate = Path.TrimEndingDirectorySeparator(path);
        string parent = Path.TrimEndingDirectorySeparator(root);

        if (candidate.Equals(parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefix = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class WhitelistDocument
    {
        public List<string> Paths { get; set; } = new();
    }
}
