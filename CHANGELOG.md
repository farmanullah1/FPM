# Changelog

All notable changes to Farmanullah PC Manager. Dates are ISO (`YYYY-MM-DD`). Status markers
follow the audit legend: **[DONE]**, **[PARTIAL]**, **[MISSING]**.

## [Unreleased]

### Added
- Deep Cleanup now measures and removes five more caches, all behind the same measure-then-delete
  contract and fixed root lists: `$PatchCache$` (`%WINDIR%\Installer\$PatchCache$`, skipped
  without admin), Firefox `cache2` for every profile (measured while Firefox runs, deleted only
  when it is closed), npm (`%LOCALAPPDATA%\npm-cache` and the legacy `%APPDATA%\npm-cache`),
  NuGet (`v3-cache`, `plugins-cache`, `%TEMP%\NuGetScratch`) and the pip/Yarn/pnpm download
  stores. Cookies, history, logins and project files remain out of reach of every root.
- `PROJECT_SPEC.md`, `BUILD_STATUS.md`, `CHANGELOG.md` for the build audit.
- Shared `CardList` style so the Apps lists no longer repeat the same `ItemsControl` template, and
  a single `NavigationItemTemplate` shared by both rail lists.

### Changed
- View models now use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`) instead of
  hand-written backing fields; `CleanupCategoryViewModel` derives from `ObservableObject` (the
  bespoke `ViewModelBaseItem` base was deleted).
- Settings and Storage open folders through `IToolLauncher.OpenFolder`; `DefaultBrowserService`
  opens Default apps through `IToolLauncher.OpenUri`.
- `RunningAppViewModel` reuses `ProcessInfo.CpuText/MemoryText` rather than re-formatting bytes.
- Root `README.md` rewritten to describe the shipped stack (custom Fluent XAML, no UI framework
  package) and the six screens; `app/README.md` updated for the new project layout.

### Removed
- Dead health-check engine: `Services/HealthCheck/` (service + five scan modules),
  `IHealthCheckService`, `IHealthCheckModule`, `HealthCheckItem`, `HealthCheckSummary`,
  `StatusToBrushConverter`.
- Dead network diagnostics (`INetworkDiagnosticsService`, `NetworkDiagnosticsService`,
  `NetworkAdapterInfo`, `DnsLookupResult`) and taskbar pinning (`ITaskbarPinService`,
  `TaskbarPinService`, `TaskbarPinEntry`).
- Unused storage surface: `ScanFolderAsync`, `RunCleanupAsync`, `EmptyRecycleBinAsync`,
  `GetRecycleBinSize`, `IStorageAnalyzer.ScanFolder`, `FolderSize`.
- Unused model members (`ProcessInfo.Path/MemoryPercent/StartTime/ThreadCount/StartedText/
  ThreadsText/SystemText`, `StartupImpact`, `StartupApp.Path`, `MemorySnapshot.CommitUsedBytes`,
  `DefenderStatus.Available/LastScan`, `UpdateStatus.LastInstalled`, `DriveSpace.Label/DriveType/
  IsReady/UsedPercentText`, `AppInfo.ExecutableName/RegistryRoot/CacheFolder`) and helpers
  (`ByteFormatter.FormatRate`, `ToolboxTool.IsAvailable`, `CleanupCategory.CanClean`,
  `Converters.StatusToBrushConverter`, `Converters.InverseBooleanConverter`).
- Unused styles (`ListView`, `ListViewItem`, `GridViewColumnHeader`, `MetricCard`, `MetricValue`,
  `StatusPill`, `GhostButton`), unused icon geometries, unused converters resources.
- `app/MECHANICS.md` (described the original Microsoft 3.23 build, not this code base).
- Duplicate DPI configuration in `app.manifest` (DPI is set by `ApplicationHighDpiMode`), which
  removes the `WFAC010` build warning.

### Fixed
- Boost no longer claims to have "reclaimed" memory. A working-set trim only asks Windows to page
  out least-recently-used pages, so the result now reports the working sets trimmed, the measured
  before → after available physical memory with its signed delta (negative when the machine is
  busy), and the temp bytes actually deleted. The tray balloon and the log line use the same
  figures, and `MemorySnapshot.ReclaimedBytes` was removed so the old claim cannot come back.
- The Home result caption wraps, so the longer honest sentence is not clipped.
- `ProtectionViewModel` opened Default apps when asked to check Windows Update; it now opens
  `ms-settings:windowsupdate` through the new `ISystemRepairService.OpenWindowsUpdateSettings`.
- Restored `Icon.Storage` and `Icon.Toolbox`, which were pruned during the styles cleanup even
  though they are referenced by string keys from `MainViewModel`.
- Storage settings no longer initialise the large-file threshold twice; the filter pill label is
  derived from the threshold instead of duplicated in a field.
- Home and the floating toolbar share one `ApplySnapshot` helper, so the memory text cannot drift
  between the two surfaces.

### Known gaps carried forward
- **[MISSING]** Wpf.Ui package (custom Fluent XAML instead), brief's `HKCU\Software\FarmanullahPCManager\`
  root, AppUserModelID `Farmanullah.PCManager.App`, deep-cleanup categories A/C/D (AI IDE caches,
  npm/pip/NuGet/yarn/Docker, crash dumps, shader/thumbnail caches, own app cache), the in-app
  two-phase cleanup approval protocol, Store/MSIX app listing, automated tests, elevated helper,
  Dark-theme screenshots.
- **[PARTIAL]** Palette hex values, card radius (10 vs 8), floating-toolbar position persistence,
  duplicate handling (detection only), simultaneous-run collision test with Microsoft PC Manager.

## [0.1.0] - 2026-11-22

First working build of the rebranded application. Commit `8702e7e` ("first commit") was published
to `github.com/farmanullah1/Farmanullah-PC-Manager`; documentation commits `f0bae92` and `9a56711`
followed.

### Added
- **Identity layer** — `app/src/Core/AppInfo.cs` as the single source of `Farmanullah PC Manager`,
  `FarmanullahPCManager` assembly/exe, `3.23.3`, data folder, Run value, protected process names.
  `FarmanullahPCManager.exe` build output; `Global\FarmanullahPCManager_SingleInstance_8A4F2C1E`
  mutex; AppUserModelID `FarmanullahPCManager.Desktop.8A4F2C1E` applied via
  `SetCurrentProcessExplicitAppUserModelID`; tray icon (`NotifyIcon`, tooltip "Farmanullah PC
  Manager", Open / Boost now / Exit).
- **Six-screen shell** — `MainWindow.xaml`: custom title bar, 236 px rail with Home, Protection,
  Storage, Apps, Toolbox and pinned Settings, `DataTemplate`-based view location, shared
  `NavigationService` broker for in-page navigation.
- **Home** — live memory gauge (`GlobalMemoryStatusEx`), temp-size probe, Boost (working-set trim +
  temp purge), Smart Boost with threshold and once-per-minute cap, four quick-action cards.
- **Protection** — Defender status (`MSFT_MpComputerStatus`, fallback `AntiVirusProduct`), Windows
  Update service + reboot-pending detection, four repair cards (notifications, default browser,
  Explorer restart, default apps).
- **Storage** — system-drive bar, deep cleanup over five measured categories, large-file scan with
  10/50/100 MB and 1 GB filters, duplicate detection (size + edge SHA-256, read-only), Storage
  Sense toggle.
- **Apps** — process list with CPU/RAM/End (self and `MSPCManager*` excluded and refused), startup
  entries from Run/RunOnce/Startup folders/Task Scheduler with real toggles, Win32 installed-app
  list with search, version, size and uninstall.
- **Toolbox** — floating always-on-top pill (live RAM, Boost, Screenshot, Notepad, Calculator) and
  six tools: screenshot (all monitors → PNG in Pictures), screen recorder (Game Bar → `psr.exe`
  fallback), calculator, notepad, DNS flush (`ipconfig /flushdns`), quick links.
- **Settings** — Start with Windows through the app's own Run value, Smart Boost enable + 50–95 %
  slider + reset, Light/Dark switch, About block with data folder and log path.
- **Theme** — `Styles/Themes/Light.xaml` / `Dark.xaml` warm palettes, `Styles/Controls.xaml`
  templated cards, buttons, toggle switch, slider, pills; `Styles/Icons.xaml` original vector icons.
- **Services** — `PcBoostService`, `MemoryOptimizer`, `ProcessManager`, `ProcessService`,
  `StartupManager`, `AppStartupService`, `DeepCleanupService`, `StorageAnalyzer`, `StorageService`,
  `StorageSenseService`, `DuplicateFinderService`, `InstalledAppService`, `SystemRepairService`,
  `ToolboxService`, `ToolLauncher`, `DefaultBrowserService`, `DefenderStatusService`,
  `WindowsUpdateStatusService`, `ProtectionService`, `TempFileCleaner`, `BrowserCacheCleaner`,
  `RecycleBinService`, `FolderCleaner`, `SafeFileSystem`, `ProcessRunner`, `AppLogger`,
  `SettingsService`; all registered in `ServiceCollectionExtensions`.
- **Safety** — no writes under `%LOCALAPPDATA%\Microsoft\*` or
  `HKCU\Software\Microsoft\PCManager\*`; no URI protocol registration; temp cleaning limited to
  files older than 48 h; browser cleaning limited to cache folders; failed deletes skipped and
  reported rather than retried blindly.
- **Docs & evidence** — `app/README.md` (architecture + per-page behaviour), root `README.md`
  (stack, architecture, six screens), `app/screenshots/` with one Light-theme capture per screen
  and the floating toolbar.

### Stubbed / placeholder behaviour (intentional)
- Programme-level default-browser changes and pop-up blocking are Windows-blocked from third-party
  apps; the cards open the supported Settings pages instead.
- "Check for updates" reads the local service/reboot state and then opens Windows Update; it does
  not install updates.
- Screen recording is delegated to Game Bar (or Steps Recorder); the app does not capture video
  itself.
- Store/MSIX apps are not enumerated; the uninstall method contains a Store branch that is
  unreachable in the current build.
- Duplicate detection reports groups only; there is no delete action.
