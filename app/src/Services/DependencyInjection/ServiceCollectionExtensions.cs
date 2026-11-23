using Microsoft.Extensions.DependencyInjection;
using PCManager.Services.Abstractions;
using PCManager.Services.Boost;
using PCManager.Services.Cleaners;
using PCManager.Services.Cleanup;
using PCManager.Services.Elevation;
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
        services.AddSingleton<IDuplicateFinderService, DuplicateFinderService>();
        services.AddSingleton<IFileRecycler, ShellFileRecycler>();
        services.AddSingleton<IDuplicateRemovalService, DuplicateRemovalService>();
        services.AddSingleton<IInstalledAppService, InstalledAppService>();
        services.AddSingleton<IAppxPackageService, AppxPackageService>();
        services.AddSingleton<ISystemRepairService, SystemRepairService>();
        services.AddSingleton<IDefenderStatusService, DefenderStatusService>();
        services.AddSingleton<IWindowsUpdateStatusService, WindowsUpdateStatusService>();
        services.AddSingleton<IToolLauncher, ToolLauncher>();
        services.AddSingleton<IDefaultBrowserService, DefaultBrowserService>();

        // --- Two-phase cleanup engine (Phase 1 read-only, Phase 2 approved) ---
        services.AddSingleton<IWhitelistService, WhitelistService>();
        services.AddSingleton<ISafetyGuard, SafetyGuard>();
        services.AddSingleton<ICleanupRuleCatalog, CleanupRuleCatalog>();
        services.AddSingleton<ICleanupScanner, CleanupScanner>();
        services.AddSingleton<ICleanupCommandParser, CleanupCommandParser>();
        services.AddSingleton<IQuarantineService, QuarantineService>();
        services.AddSingleton<ICleanupExecutor, CleanupExecutor>();
        services.AddSingleton<ICleanupLog, CleanupLog>();
        services.AddSingleton<ICleanupHistoryService, CleanupHistoryService>();
        services.AddSingleton<ICleanupReportExporter, CleanupReportExporter>();

        // --- Elevated helper (one fixed command per request, UAC on demand) ---
        services.AddSingleton<IElevationService>(sp =>
            new ElevationService(sp.GetRequiredService<IAppLogger>()));
        services.AddSingleton<IRestorePointService, RestorePointService>();

        // --- FPM Services (Migration, Memory Diagnostics, Unified Cleanup, Network) ---
        services.AddSingleton<ILegacyIdentityMigrationService, PCManager.Services.Infrastructure.LegacyIdentityMigrationService>();
        services.AddSingleton<MemoryDiagnosticsService>();
        services.AddSingleton<IMemoryDiagnosticsService>(sp => sp.GetRequiredService<MemoryDiagnosticsService>());
        services.AddSingleton<IMemoryReliefService>(sp => sp.GetRequiredService<MemoryDiagnosticsService>());
        services.AddSingleton<IUnifiedCleanupCoordinator, UnifiedCleanupCoordinator>();
        services.AddSingleton<INetworkThroughputService, PCManager.Services.Network.NetworkThroughputService>();
        services.AddSingleton<IStorageIntelligenceService, PCManager.Services.Storage.StorageIntelligenceService>();
        services.AddSingleton<IAppLeftoverScanner, PCManager.Services.Apps.AppLeftoverScanner>();

        // --- Page facades -------------------------------------------------
        services.AddSingleton<IPcBoostService, PcBoostService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IToolboxService, ToolboxService>();
        services.AddSingleton<IProtectionService, ProtectionService>();

        return services;
    }
}
