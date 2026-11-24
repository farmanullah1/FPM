using Microsoft.Win32;
using PCManager.Core.Models;
using PCManager.Services.Apps;
using PCManager.Services.Cleanup;
using PCManager.Services.Infrastructure;
using Xunit;

namespace PCManager.Tests;

public sealed class AppLeftoverScannerTests
{
    [Theory]
    [InlineData("Windows")]
    [InlineData("Microsoft")]
    [InlineData("System32")]
    [InlineData("FPM")]
    [InlineData("FPM.Helper")]
    public async Task ScanLeftoversAsync_ProtectedNames_ReturnsEmptyAndRefuses(string protectedName)
    {
        var safetyGuard = new SafetyGuard(new WhitelistService());
        var scanner = new AppLeftoverScanner(safetyGuard, new TestLogger());

        AppLeftoverScanResult result = await scanner.ScanLeftoversAsync(protectedName);

        Assert.NotNull(result);
        Assert.False(result.FoundAny);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("ab")]
    public async Task ScanLeftoversAsync_InvalidOrShortName_ReturnsEmpty(string? invalidName)
    {
        var safetyGuard = new SafetyGuard(new WhitelistService());
        var scanner = new AppLeftoverScanner(safetyGuard, new TestLogger());

        AppLeftoverScanResult result = await scanner.ScanLeftoversAsync(invalidName!);

        Assert.NotNull(result);
        Assert.False(result.FoundAny);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task CleanLeftoversAsync_EmptyItems_ReturnsOk()
    {
        var safetyGuard = new SafetyGuard(new WhitelistService());
        var scanner = new AppLeftoverScanner(safetyGuard, new TestLogger());

        OperationResult result = await scanner.CleanLeftoversAsync(Array.Empty<AppLeftoverItem>());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CleanLeftoversAsync_DirectoryItem_RemovesDirectory()
    {
        var safetyGuard = new SafetyGuard(new WhitelistService());
        var scanner = new AppLeftoverScanner(safetyGuard, new TestLogger());

        string testDir = Path.Combine(Path.GetTempPath(), "FPM_Leftover_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        string testFile = Path.Combine(testDir, "cache.dat");
        File.WriteAllBytes(testFile, new byte[1024]);

        var item = new AppLeftoverItem(
            name: "TestFolder",
            targetPath: testDir,
            kind: AppLeftoverKind.Directory,
            sizeBytes: 1024,
            formattedSize: "1 KB",
            isSafeToRemove: true,
            safetyReason: "Test folder");

        OperationResult result = await scanner.CleanLeftoversAsync(new[] { item }, createRegBackup: false);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(testDir));
    }

    [Fact]
    public async Task ExportRegBackupAsync_WritesOneFilePerKeyInsteadOfOverwriting()
    {
        var safetyGuard = new SafetyGuard(new WhitelistService());
        var scanner = new AppLeftoverScanner(safetyGuard, new TestLogger());

        string suffix = Guid.NewGuid().ToString("N");
        string subKey = $"Software\\FPM\\__LeftoverBackupTest_{suffix}";
        string keyA = $"HKEY_CURRENT_USER\\{subKey}\\A";
        string keyB = $"HKEY_CURRENT_USER\\{subKey}\\B";
        string backupDir = Path.Combine(Path.GetTempPath(), "FPM_RegBackup_Test_" + suffix);

        try
        {
            using (Registry.CurrentUser.CreateSubKey(subKey + "\\A"))
            using (Registry.CurrentUser.CreateSubKey(subKey + "\\B"))
            {
            }

            OperationResult result = await scanner.ExportRegBackupAsync(
                new[] { keyA, keyB },
                Path.Combine(backupDir, "reg_leftovers.reg"));

            Assert.True(result.Success, result.Message);
            string[] files = Directory.GetFiles(backupDir, "*.reg");
            Assert.Equal(2, files.Length);
            Assert.All(files, file => Assert.Contains("Windows Registry Editor", File.ReadAllText(file)));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }
        }
    }
}
