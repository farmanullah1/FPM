using System.IO.Compression;
using System.Security.Cryptography;
using PCManager.Core.Android;
using PCManager.Services.Android;
using Xunit;

namespace PCManager.Tests;

public sealed class AdbProvisionerTests
{
    [Fact]
    public async Task Consent_is_required_before_download_or_extraction()
    {
        using var temp = new TempFiles(createLocalAdb: false);
        var source = new FakePackageSource(CreateArchive("adb-bytes"));
        var provisioner = new AdbProvisioner(source, new FakeSignature(), temp.ToolsRoot);

        AdbProvisionResult result = await provisioner.InstallAsync(consented: false);

        Assert.Equal(AdbStatusKind.Declined, result.Kind);
        Assert.Equal(0, source.DownloadCount);
        Assert.False(Directory.Exists(Path.Combine(temp.ToolsRoot, "platform-tools")));
    }

    [Fact]
    public async Task Checksum_mismatch_is_rejected_before_extraction_or_signature_check()
    {
        using var temp = new TempFiles(createLocalAdb: false);
        var source = new FakePackageSource(CreateArchive("adb-bytes"));
        var signature = new FakeSignature();
        var provisioner = new AdbProvisioner(
            source, signature, temp.ToolsRoot, expectedSha256: new string('0', 64));

        AdbProvisionResult result = await provisioner.InstallAsync(consented: true);

        Assert.Equal(AdbStatusKind.ChecksumMismatch, result.Kind);
        Assert.Equal(0, source.ExtractCount);
        Assert.False(signature.Called);
        Assert.False(Directory.Exists(Path.Combine(temp.ToolsRoot, "platform-tools")));
    }

    [Fact]
    public async Task Valid_checksum_but_unsigned_adb_is_not_installed()
    {
        using var temp = new TempFiles(createLocalAdb: false);
        byte[] archive = CreateArchive("adb-bytes");
        var source = new FakePackageSource(archive);
        var provisioner = new AdbProvisioner(
            source, new FakeSignature(result: false), temp.ToolsRoot, Hash(archive));

        AdbProvisionResult result = await provisioner.InstallAsync(consented: true);

        Assert.Equal(AdbStatusKind.Unverified, result.Kind);
        Assert.False(Directory.Exists(Path.Combine(temp.ToolsRoot, "platform-tools")));
    }

    [Fact]
    public async Task Valid_checksum_and_google_signature_installs_the_staged_tools()
    {
        using var temp = new TempFiles(createLocalAdb: false);
        byte[] archive = CreateArchive("adb-bytes");
        var source = new FakePackageSource(archive);
        var provisioner = new AdbProvisioner(
            source, new FakeSignature(result: true), temp.ToolsRoot, Hash(archive));

        AdbProvisionResult result = await provisioner.InstallAsync(consented: true);

        Assert.Equal(AdbStatusKind.Ready, result.Kind);
        Assert.True(File.Exists(Path.Combine(temp.ToolsRoot, "platform-tools", "adb.exe")));
        Assert.Equal(1, source.ExtractCount);
    }

    private static byte[] CreateArchive(string adbText)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("adb.exe");
            using StreamWriter writer = new(entry.Open());
            writer.Write(adbText);
        }

        return stream.ToArray();
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
