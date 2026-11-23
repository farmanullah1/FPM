using PCManager.Services.Cleanup;
using Xunit;

namespace PCManager.Tests;

public sealed class WhitelistServiceTests
{
    [Fact]
    public void Added_path_persists_and_reloads()
    {
        string folder = TemporaryFolder();
        string file = Path.Combine(folder, "whitelist.json");
        string protectedFolder = Path.Combine(folder, "projects");
        Directory.CreateDirectory(protectedFolder);

        try
        {
            var service = new WhitelistService(file);
            service.Add(protectedFolder);

            var reloaded = new WhitelistService(file);

            Assert.Contains(
                Path.GetFullPath(protectedFolder),
                reloaded.GetEntries(),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Remove_updates_the_file()
    {
        string folder = TemporaryFolder();
        string file = Path.Combine(folder, "whitelist.json");
        string protectedFolder = Path.Combine(folder, "projects");
        Directory.CreateDirectory(protectedFolder);

        try
        {
            var service = new WhitelistService(file);
            service.Add(protectedFolder);
            Assert.True(service.Remove(protectedFolder));

            var reloaded = new WhitelistService(file);
            Assert.Empty(reloaded.GetEntries());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Duplicate_entries_are_not_written_twice()
    {
        string folder = TemporaryFolder();
        string file = Path.Combine(folder, "whitelist.json");
        string protectedFolder = Path.Combine(folder, "projects");
        Directory.CreateDirectory(protectedFolder);

        try
        {
            var service = new WhitelistService(file);
            service.Add(protectedFolder);
            service.Add(protectedFolder + Path.DirectorySeparatorChar);

            Assert.Single(service.GetEntries());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Matching_requires_a_directory_boundary()
    {
        string folder = TemporaryFolder();
        string file = Path.Combine(folder, "whitelist.json");
        string protectedFolder = Path.Combine(folder, "projects");
        Directory.CreateDirectory(protectedFolder);

        try
        {
            var service = new WhitelistService(file);
            service.Add(protectedFolder);

            Assert.True(service.IsWhitelisted(Path.Combine(protectedFolder, "child")));
            Assert.False(service.IsWhitelisted(protectedFolder + "-other"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string TemporaryFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "PCManagerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
