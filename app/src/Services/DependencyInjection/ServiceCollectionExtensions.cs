using Microsoft.Extensions.DependencyInjection;
using PCManager.Services.Abstractions;
using PCManager.Services.Boost;
using PCManager.Services.Cleaners;
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
/// To add a tool or cleaner: write the interface + implementation in this project, add
/// one line below, then inject it into the view model that exposes it.
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
        services.AddSingleton<IDefaultBrowserService, DefaultBrowserService>();

        // --- Page facades -------------------------------------------------
        services.AddSingleton<IPcBoostService, PcBoostService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IToolboxService, ToolboxService>();
        services.AddSingleton<IProtectionService, ProtectionService>();

        return services;
    }
}
