using PCManager.Core.Models;
using PCManager.Services.Storage;
using Xunit;

namespace PCManager.Tests;

public sealed class StorageIntelligenceTests
{
    [Fact]
    public async Task GetDrivesHealthAsync_ReturnsPhysicalOrFixedDrives()
    {
        var logger = new TestLogger();
        var service = new StorageIntelligenceService(logger);

        IReadOnlyList<DriveHealthInfo> drives = await service.GetDrivesHealthAsync();

        Assert.NotNull(drives);
        Assert.NotEmpty(drives);
        foreach (var d in drives)
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Model));
            Assert.False(string.IsNullOrWhiteSpace(d.HealthStatus));
            Assert.True(d.SizeBytes >= 0);
        }
    }

    [Fact]
    public async Task AnalyzeStorageMapAsync_ReturnsCategorizedItems()
    {
        var logger = new TestLogger();
        var service = new StorageIntelligenceService(logger);

        string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        IReadOnlyList<StorageFolderItem> items = await service.AnalyzeStorageMapAsync(systemDrive, topCount: 5);

        Assert.NotNull(items);
        foreach (var item in items)
        {
            Assert.NotNull(item.Name);
            Assert.True(item.Bytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(item.Category));
            Assert.True(item.PercentOfDrive >= 0);
        }
    }

    [Fact]
    public void DismAnalysisResult_Failed_HasStructuredDefaults()
    {
        var failed = DismAnalysisResult.Failed("Elevation required");

        Assert.False(failed.Success);
        Assert.Equal("Elevation required", failed.ErrorMessage);
        Assert.Equal("Unknown", failed.ComponentStoreSize);
        Assert.False(failed.CleanupRecommended);
    }

    [Fact]
    public async Task AnalyzeStorageMapAsync_NonExistentFolder_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var service = new StorageIntelligenceService(logger);

        string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        IReadOnlyList<StorageFolderItem> items = await service.AnalyzeStorageMapAsync(fakePath);

        Assert.NotNull(items);
        Assert.Empty(items);
    }
}
