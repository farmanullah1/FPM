using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Cleanup;
using PCManager.Services.Storage;
using Xunit;

namespace PCManager.Tests;

public sealed class DuplicateRemovalServiceTests
{
    [Fact]
    public async Task Extra_copies_are_recycled_and_the_kept_copy_stays()
    {
        using var workspace = new Workspace();
        string keep = workspace.CreateFile("keep.txt", "same content");
        string extra = workspace.CreateFile("extra.txt", "same content");
        var recycler = new FakeRecycler();
        var service = workspace.CreateService(recycler);

        DuplicateRemovalResult result =
            await service.MoveExtrasToRecycleBinAsync(workspace.Group(keep, extra), keep);

        Assert.Equal(1, result.MovedFiles);
        Assert.Equal(0, result.SkippedFiles);
        Assert.Equal(new[] { extra }, recycler.Recycled);
        Assert.True(File.Exists(keep));
        Assert.False(File.Exists(extra));
    }

    [Fact]
    public async Task A_file_changed_after_the_scan_is_not_recycled()
    {
        using var workspace = new Workspace();
        string keep = workspace.CreateFile("keep.txt", "original content");
        string extra = workspace.CreateFile("extra.txt", "modified content");
        var recycler = new FakeRecycler();
        var service = workspace.CreateService(recycler);

        DuplicateRemovalResult result =
            await service.MoveExtrasToRecycleBinAsync(workspace.Group(keep, extra), keep);

        Assert.Equal(0, result.MovedFiles);
        Assert.Equal(1, result.SkippedFiles);
        Assert.Empty(recycler.Recycled);
        Assert.True(File.Exists(extra));
        Assert.Contains(result.Messages, message => message.Contains("no longer identical"));
    }

    [Fact]
    public async Task A_whitelisted_extra_is_refused()
    {
        using var workspace = new Workspace();
        string keep = workspace.CreateFile("keep.txt", "same content");
        string extra = workspace.CreateFile("extra.txt", "same content");
        workspace.Whitelist.Add(extra);
        var recycler = new FakeRecycler();
        var service = workspace.CreateService(recycler);

        DuplicateRemovalResult result =
            await service.MoveExtrasToRecycleBinAsync(workspace.Group(keep, extra), keep);

        Assert.Equal(0, result.MovedFiles);
        Assert.Equal(1, result.SkippedFiles);
        Assert.Empty(recycler.Recycled);
        Assert.True(File.Exists(extra));
        Assert.Contains(result.Messages, message => message.Contains("whitelist"));
    }

    [Fact]
    public async Task The_kept_copy_must_belong_to_the_group()
    {
        using var workspace = new Workspace();
        string keep = workspace.CreateFile("keep.txt", "same content");
        string extra = workspace.CreateFile("extra.txt", "same content");
        string stranger = workspace.CreateFile("stranger.txt", "other");
        var recycler = new FakeRecycler();
        var service = workspace.CreateService(recycler);

        DuplicateRemovalResult result =
            await service.MoveExtrasToRecycleBinAsync(workspace.Group(keep, extra), stranger);

        Assert.Equal(0, result.MovedFiles);
        Assert.Equal(0, result.SkippedFiles);
        Assert.Empty(recycler.Recycled);
        Assert.Contains(result.Messages, message => message.Contains("not part of"));
    }

    [Fact]
    public async Task A_shell_refusal_is_reported_and_the_file_stays()
    {
        using var workspace = new Workspace();
        string keep = workspace.CreateFile("keep.txt", "same content");
        string extra = workspace.CreateFile("extra.txt", "same content");
        var recycler = new FakeRecycler { FailAll = true };
        var service = workspace.CreateService(recycler);

        DuplicateRemovalResult result =
            await service.MoveExtrasToRecycleBinAsync(workspace.Group(keep, extra), keep);

        Assert.Equal(0, result.MovedFiles);
        Assert.Equal(1, result.SkippedFiles);
        Assert.True(File.Exists(extra));
        Assert.Contains(result.Messages, message => message.Contains("locked"));
    }

    private sealed class FakeRecycler : IFileRecycler
    {
        public List<string> Recycled { get; } = new();
        public bool FailAll { get; init; }

        public OperationResult Recycle(string path)
        {
            if (FailAll)
            {
                return OperationResult.Fail("The file is locked by another process.");
            }

            File.Delete(path);
            Recycled.Add(path);
            return OperationResult.Ok("Moved to the Recycle Bin.");
        }
    }

    private sealed class Workspace : IDisposable
    {
        private readonly string _root;

        public Workspace()
        {
            _root = Path.Combine(Path.GetTempPath(), "PCManagerDuplicateTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Whitelist = new WhitelistService(Path.Combine(_root, "whitelist.json"));
        }

        public WhitelistService Whitelist { get; }

        public string CreateFile(string name, string content)
        {
            string path = Path.Combine(_root, "files", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public DuplicateGroup Group(string keep, string extra)
            => new()
            {
                FileSize = new FileInfo(keep).Length,
                Paths = new List<string> { keep, extra },
                KeepPath = keep
            };

        public DuplicateRemovalService CreateService(IFileRecycler recycler)
            => new(Whitelist, recycler, new TestLogger());

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // Best effort for a test folder.
            }
        }
    }
}
