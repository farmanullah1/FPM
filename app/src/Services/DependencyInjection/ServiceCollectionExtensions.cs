using Microsoft.Extensions.DependencyInjection;
using PCManager.Services.Abstractions;
using PCManager.Services.Boost;
using PCManager.Services.Cleaners;
using PCManager.Services.HealthCheck;
using PCManager.Services.HealthCheck.Modules;
using PCManager.Services.Infrastructure;
using PCManager.Services.Memory;
using PCManager.Services.Processes;
using PCManager.Services.Protection;
using PCManager.Services.Settings;
using PCManager.Services.Startup;
using PCManager.Services.Storage;
using PCManager.Services.Toolbox;

namespace PCManager.Services.DependencyInjection;

/// <summary>
/// Single place where every service is registered.
///
/// To add a new tool or cleaner:
///   1. write the interface + implementation in this project,
///   2. add one line to the "isolated services" block (or to the health-check block
///      if it is a scan module),
///   3. inject it into a view model.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPCManagerServices(this IServiceCollection services)
    {
        // --- Isolated single-purpose services -----------------------------
        services.AddSingleton<IAppLogger, AppLogger>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAppStartupService, AppStartupService>();
        services.AddSingleton<ITempFileCleaner, TempFileCleaner>();
        services.AddSingleton<IBrowserCacheCleaner, BrowserCacheCleaner>();
        services.AddSingleton<IRecycleBinService, RecycleBinService>();
        services.AddSingleton<IMemoryOptimizer, MemoryOptimizer>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<IStartupManager, StartupManager>();
        services.AddSingleton<IStorageAnalyzer, StorageAnalyzer>();
        services.AddSingleton<IStorageSenseService, StorageSenseService>();
        services.AddSingleton<IDeepCleanupService, DeepCleanupService>();
        services.AddSingleton<IDuplicateFinderService, DuplicateFinderService>();
        services.AddSingleton<IInstalledAppService, InstalledAppService>();
        services.AddSingleton<ISystemRepairService, SystemRepairService>();
        services.AddSingleton<IDefenderStatusService, DefenderStatusService>();
        services.AddSingleton<IWindowsUpdateStatusService, WindowsUpdateStatusService>();
        services.AddSingleton<IToolLauncher, ToolLauncher>();
        services.AddSingleton<INetworkDiagnosticsService, NetworkDiagnosticsService>();
        services.AddSingleton<IDefaultBrowserService, DefaultBrowserService>();
        services.AddSingleton<ITaskbarPinService, TaskbarPinService>();

        // --- Page facades -------------------------------------------------
        services.AddSingleton<IPcBoostService, PcBoostService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IToolboxService, ToolboxService>();
        services.AddSingleton<IProtectionService, ProtectionService>();
        services.AddSingleton<IHealthCheckService, HealthCheckService>();

        // --- Health check modules (add new scans here) --------------------
        services.AddSingleton<IHealthCheckModule, TempFileModule>();
        services.AddSingleton<IHealthCheckModule, BrowserCacheModule>();
        services.AddSingleton<IHealthCheckModule, StartupImpactModule>();
        services.AddSingleton<IHealthCheckModule, SystemUpdateModule>();
        services.AddSingleton<IHealthCheckModule, DiskSpaceModule>();

        return services;
    }
}
