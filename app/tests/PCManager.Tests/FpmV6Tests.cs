using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Cleanup;
using PCManager.Services.Infrastructure;
using PCManager.Services.Memory;
using Xunit;

namespace PCManager.Tests;

public sealed class FpmV6Tests
{
    private sealed class TestLogger : IAppLogger
    {
        public string LogFilePath => "test.log";
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    [Fact]
    public void AppInfo_HasCanonicalFpmIdentity()
    {
        Assert.Equal("FPM", AppInfo.ProductName);
        Assert.Equal("FPM", AppInfo.DataFolderName);
        Assert.Equal("FPM", AppInfo.RunValueName);
        Assert.Equal(@"Global\FPM_SingleInstance_8A4F2C1E", AppInfo.SingleInstanceMutexName);
        Assert.Equal("FPM.Desktop.8A4F2C1E", AppInfo.AppUserModelId);
        Assert.Equal(@"Software\FPM", AppInfo.RegistryRootKey);
    }

    [Fact]
    public void AppInfo_ProtectedProcess_IncludesFpmAndMspcManager()
    {
        Assert.True(AppInfo.IsProtectedProcessName("FPM"));
        Assert.True(AppInfo.IsProtectedProcessName("FPM.Helper"));
        Assert.True(AppInfo.IsProtectedProcessName("MSPCManager"));
        Assert.True(AppInfo.IsProtectedProcessName("MSPCManagerService"));
        Assert.True(AppInfo.IsProtectedProcessName("FarmanullahPCManager"));
        Assert.True(AppInfo.IsProtectedProcessName("explorer"));
        Assert.True(AppInfo.IsProtectedProcessName("services"));
    }

    [Fact]
    public void LegacyIdentityMigrationService_HandlesMissingGracefully()
    {
        var migrationService = new LegacyIdentityMigrationService(new TestLogger());
        var result = migrationService.MigrateIfNecessary();

        Assert.NotNull(result);
        Assert.NotNull(result.NewPath);
    }

    [Fact]
    public void MemoryDiagnosticsService_ProducesValidSnapshot()
    {
        var memoryService = new MemoryDiagnosticsService(new TestLogger());
        var snapshot = memoryService.GetExtendedSnapshot();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.TotalPhysicalBytes > 0);
        Assert.NotNull(snapshot.TopConsumers);
    }

    [Fact]
    public async Task MemoryReliefService_RefusesProtectedProcess()
    {
        var memoryService = new MemoryDiagnosticsService(new TestLogger());
        // Attempting to relieve PID 0 or non-existent or current process
        var plan = new MemoryReliefPlan(new[] { Environment.ProcessId });
        var result = await memoryService.ExecuteReliefPlanAsync(plan);

        Assert.Equal(1, result.Attempted);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.Failed);
    }
}
