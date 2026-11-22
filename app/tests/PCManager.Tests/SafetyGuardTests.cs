using PCManager.Services.Abstractions;
using PCManager.Services.Cleanup;
using Xunit;

namespace PCManager.Tests;

public sealed class SafetyGuardTests
{
    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string WindowsFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static readonly string NpmCache = Path.Combine(LocalAppData, "npm-cache");

    [Fact]
    public void Allows_a_path_inside_the_rule_root()
    {
        var guard = new SafetyGuard(new FakeWhitelist());
        string file = Path.Combine(NpmCache, "_cacache", "content-v2", "sha512", "abcd");

        SafetyVerdict verdict = guard.Evaluate(file, new[] { NpmCache });

        Assert.True(verdict.Allowed, verdict.Reason);
    }

    [Fact]
    public void Refuses_a_path_outside_the_rule_root()
    {
        var guard = new SafetyGuard(new FakeWhitelist());
        string file = Path.Combine(WindowsFolder, "System32", "kernel32.dll");

        SafetyVerdict verdict = guard.Evaluate(file, new[] { NpmCache });

        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void Refuses_traversal_that_resolves_outside_the_root()
    {
        var guard = new SafetyGuard(new FakeWhitelist());
        string sneaky = Path.Combine(NpmCache, "..", "..", "..", "Windows", "System32", "kernel32.dll");

        SafetyVerdict verdict = guard.Evaluate(sneaky, new[] { NpmCache });

        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void Refuses_a_drive_root()
    {
        var guard = new SafetyGuard(new FakeWhitelist());
        string drive = Path.GetPathRoot(WindowsFolder)!;

        SafetyVerdict verdict = guard.EvaluateRoot(drive, new[] { drive });

        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void Refuses_the_windows_folder_but_allows_named_temp_subtree()
    {
        var guard = new SafetyGuard(new FakeWhitelist());
        string windows = Path.TrimEndingDirectorySeparator(WindowsFolder);
        string temp = Path.Combine(windows, "Temp");

        Assert.False(guard.EvaluateRoot(windows, new[] { windows }).Allowed);
        Assert.True(guard.EvaluateRoot(temp, new[] { temp }).Allowed);
    }

    [Fact]
    public void Refuses_files_that_look_like_credentials_or_databases()
    {
        var guard = new SafetyGuard(new FakeWhitelist());
        string token = Path.Combine(NpmCache, "auth-token.json");
        string database = Path.Combine(NpmCache, "state.sqlite");

        Assert.False(guard.Evaluate(token, new[] { NpmCache }).Allowed);
        Assert.False(guard.Evaluate(database, new[] { NpmCache }).Allowed);
    }

    [Fact]
    public void Whitelist_wins_even_inside_an_allowed_root()
    {
        var whitelist = new FakeWhitelist { Entries = { NpmCache } };
        var guard = new SafetyGuard(whitelist);
        string file = Path.Combine(NpmCache, "item");

        SafetyVerdict verdict = guard.Evaluate(file, new[] { NpmCache });

        Assert.False(verdict.Allowed);
        Assert.Contains("whitelist", verdict.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refuses_microsoft_state_but_allows_the_named_edge_cache_tree()
    {
        var guard = new SafetyGuard(new FakeWhitelist());
        string inetCache = Path.Combine(LocalAppData, "Microsoft", "Windows", "INetCache", "x");
        string edgeCache = Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data", "Default", "Cache", "x");

        Assert.False(guard.Evaluate(inetCache, new[] { Path.GetDirectoryName(inetCache)! }).Allowed);
        Assert.True(guard.Evaluate(edgeCache, new[] { Path.GetDirectoryName(edgeCache)! }).Allowed);
    }

    [Fact]
    public void Refuses_a_rule_root_as_a_deletion_target_but_accepts_it_for_enumeration()
    {
        var guard = new SafetyGuard(new FakeWhitelist());

        Assert.False(guard.Evaluate(NpmCache, new[] { NpmCache }).Allowed);
        Assert.True(guard.EvaluateRoot(NpmCache, new[] { NpmCache }).Allowed);
    }

    private sealed class FakeWhitelist : IWhitelistService
    {
        public List<string> Entries { get; } = new();

        public string FilePath => "test-whitelist.json";

        public IReadOnlyList<string> GetEntries() => Entries;

        public bool IsWhitelisted(string path)
            => Entries.Any(entry => path.StartsWith(
                Path.TrimEndingDirectorySeparator(entry) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));

        public void Reload()
        {
        }

        public void Add(string path) => Entries.Add(path);

        public bool Remove(string path) => Entries.Remove(path);
    }
}
