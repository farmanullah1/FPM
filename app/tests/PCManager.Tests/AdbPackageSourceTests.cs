using System.IO.Compression;
using PCManager.Services.Android;
using Xunit;

namespace PCManager.Tests;

public sealed class AdbPackageSourceTests
{
    [Fact]
    public async Task Extractor_keeps_platform_tools_and_ignores_other_archive_entries()
    {
        using var temp = new TempFiles(createLocalAdb: false);
        string archive = Path.Combine(temp.ToolsRoot, "source.zip");
        Directory.CreateDirectory(temp.ToolsRoot);

        using (var stream = File.Create(archive))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            Write(zip, "platform-tools/adb.exe", "fake-adb");
            Write(zip, "platform-tools/lib/notes.txt", "tool");
            Write(zip, "other/ignored.txt", "nope");
        }

        string destination = Path.Combine(temp.ToolsRoot, "staging");
        await new HttpAdbPackageSource().ExtractPlatformToolsAsync(archive, destination);

        Assert.True(File.Exists(Path.Combine(destination, "adb.exe")));
        Assert.True(File.Exists(Path.Combine(destination, "lib", "notes.txt")));
        Assert.False(File.Exists(Path.Combine(destination, "other", "ignored.txt")));
    }

    [Fact]
    public async Task Extractor_rejects_an_entry_that_escapes_platform_tools()
    {
        using var temp = new TempFiles(createLocalAdb: false);
        string archive = Path.Combine(temp.ToolsRoot, "source.zip");
        Directory.CreateDirectory(temp.ToolsRoot);

        using (var stream = File.Create(archive))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            Write(zip, "platform-tools/../escape.txt", "bad");
        }

        string destination = Path.Combine(temp.ToolsRoot, "staging");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new HttpAdbPackageSource().ExtractPlatformToolsAsync(archive, destination));
        Assert.False(File.Exists(Path.Combine(temp.ToolsRoot, "escape.txt")));
    }

    private static void Write(ZipArchive zip, string path, string text)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path);
        using StreamWriter writer = new(entry.Open());
        writer.Write(text);
    }
}
