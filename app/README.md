# Farmanullah PC Manager 3.23.3.0 — C# / WPF

A working reimplementation of the Microsoft PC Manager 3.23 feature set in .NET 8 + WPF (MVVM),
shipped under its own identity so it can never collide with Microsoft PC Manager:
`FarmanullahPCManager.exe`, mutex `Global\FarmanullahPCManager_SingleInstance_8A4F2C1E`,
AppUserModelID `FarmanullahPCManager.Desktop.8A4F2C1E`, data in
`%LOCALAPPDATA%\FarmanullahPCManager` and the Run value `FarmanullahPCManager`. No
`mspcmanager://` or `pcmanager://` protocol is registered, and the boost/End Task paths skip
this process and every `MSPCManager*` process.
Everything is real code: Win32 P/Invoke, WMI (`System.Management`), registry reads/writes,
filesystem enumeration and `Process`/`ServiceController` calls. There is no mock data,
no placeholder view model and no `TODO`: each page reads live machine state and each
cleaner/repair performs an actual operation.

## Build and run

Requirements: Windows 10/11 and the .NET 8 SDK (`global.json` pins the SDK feature band,
`rollForward: latestFeature`, so a newer 8.0.x band also works).

```powershell
cd app
dotnet build PCManager.sln
dotnet test tests/PCManager.Tests/PCManager.Tests.csproj
dotnet run --project src/PCManager/PCManager.csproj
```

or open `app/PCManager.sln` in Visual Studio and press F5. The executable lands in
`src/PCManager/bin/Debug/net8.0-windows/FarmanullahPCManager.exe`.

NuGet packages are declared per project and restore automatically:

| Project | Packages |
| --- | --- |
| `PCManager` (app) | `Microsoft.Extensions.DependencyInjection` |
| `PCManager.Services` | `CommunityToolkit.Mvvm`, `System.Management`, `System.ServiceProcess.ServiceController`, `System.Drawing.Common`, `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `PCManager.ViewModels` | `CommunityToolkit.Mvvm` |

## Project layout

```
app/
├── PCManager.sln
├── global.json                 SDK pin
├── screenshots/                One capture per page plus the floating toolbar
├── tests/PCManager.Tests/      xunit tests for the approval parser, safety guard and helper contract
└── src/
    ├── Core/                   Models + formatting, no Windows dependencies
    │   ├── Elevation/          Closed helper protocol + fixed machine-wide cleanup targets
    │   ├── Models/             Process, startup, storage, cleanup, settings and toolbox models
    │   └── Formatting/         ByteFormatter
    ├── Services/               All system access; one interface per tool/cleaner
    │   ├── Abstractions/       ITempFileCleaner, IProcessService, ICleanupScanner, ...
    │   ├── Cleanup/            Rule catalog, read-only scanner, command parser, safety guard,
    │   │                        approved-plan executor, whitelist and cleanup log
    │   ├── Interop/            NativeMethods (P/Invoke signatures + documented constants)
    │   ├── Infrastructure/     SafeFileSystem (guarded enumeration), ProcessRunner, AppLogger
    │   ├── Cleaners/           TempFileCleaner, BrowserCacheCleaner, RecycleBinService
    │   ├── Boost/              PcBoostService (memory trim + temp purge)
    │   ├── Process/            ProcessService, ProcessManager, StartupManager
    │   ├── Storage/            StorageService, StorageAnalyzer, DuplicateFinderService,
    │   │                        StorageSenseService
    │   ├── Protection/         Defender + Windows Update status
    │   ├── Toolbox/            ToolboxService, ToolLauncher, InstalledAppService,
    │   │                        AppxPackageService, SystemRepairService, DefaultBrowserService
    │   ├── Settings/           SettingsService (per-user JSON under %LOCALAPPDATA%)
    │   ├── Startup/            AppStartupService (HKCU Run value)
    │   └── DependencyInjection/ServiceCollectionExtensions.cs   ← single registration point
    ├── ViewModels/             One view model per page + shell, theme abstraction, item wrappers
    ├── Helper/                 Separate requireAdministrator WinExe: one typed request per launch
    └── PCManager/              WPF shell
        ├── App.xaml(.cs)       DI composition root, DataTemplates mapping view model → view
        ├── Converters/         Icon/status/bool converters
        ├── Services/ThemeService.cs
        ├── Styles/             Themes/Light.xaml, Themes/Dark.xaml, Icons.xaml, Controls.xaml
        └── Views/              MainWindow, HomeView, ProtectionView, StorageView, AppsView,
                                ToolboxView, SettingsView, FloatingToolbarWindow
```

Dependency direction is one-way: `Core` ← `Services`/`ViewModels` ← `PCManager`.
View models never touch Win32 APIs; they only talk to the `Abstractions` interfaces,
which is what makes each feature testable and replaceable.

## Pages

| Page | View model | What is real |
| --- | --- | --- |
| Home | `HomeViewModel` | Live memory usage and temp-file size, one-click Boost (working-set trim + temp purge, reporting the measured available-memory delta and the bytes actually removed), Smart Boost auto-trim above the saved threshold, and four quick-action cards. |
| Protection | `ProtectionViewModel` | Defender real-time/antivirus state and signature version, Windows Update service and pending restart, plus pop-up management, default browser, Explorer restart and default-apps cards. |
| Storage | `StorageViewModel` | Drive usage from `DriveInfo`; Cleanup Center with a read-only numbered scan, tiered rows, typed approval gate (`proceed`/`only`/`keep`, ranges), dry run, optional restoreable quarantine, safety-guarded execution, `cleanup_log.txt` and a measured before/after report; quarantine list with per-file Restore that refuses an occupied destination; large-file filter (10/50/100/500 MB, 1 GB); duplicate finder (size + sampled SHA-256); Storage Sense. |
| Apps | `AppsViewModel` | Live application list with RAM and CPU plus per-row End Task (this process and `MSPCManager*` are refused), startup entries from `Run`/`StartupApproved`/Startup folder/scheduled tasks with working toggles, installed Win32 apps (HKLM/HKCU Uninstall keys) with search and uninstall that waits for the uninstaller, re-checks the registry and reports measured leftovers, and Store/MSIX packages from `Get-AppxPackage` with framework/system packages hidden and a confirmed current-user uninstall. |
| Toolbox | `ToolboxViewModel` | Floating always-on-top toolbar toggle, multi-monitor screenshot via GDI, screen recorder (Game Bar with `psr.exe` fallback), calculator, notepad, `ipconfig /flushdns` and quick web links. |
| Settings | `SettingsViewModel` | Start with Windows (own `HKCU\...\Run` value), Smart Boost on/off and threshold, protected paths (add/remove/reload whitelist), quarantine retention and size policy, Light/Dark theme, and the About block with data/log paths. |

## Cleanup engine

The Storage page and the CLI use one engine, split so that measuring cannot delete:

| Piece | File | Job |
| --- | --- | --- |
| Rule catalog | `Services/Cleanup/CleanupRuleCatalog.cs` | Fixed roots, tiers, minimum age, rebuild impact, app detection evidence |
| Scanner | `Services/Cleanup/CleanupScanner.cs` | Read-only measurement with bounded parallelism; no delete/move API |
| Parser | `Services/Cleanup/CleanupCommandParser.cs` | `proceed`, `only 1, 2`, `keep 1, 2`, ranges; rejects everything else |
| Safety guard | `Services/Cleanup/SafetyGuard.cs` | Allow-list roots, deny-list, reparse refusal, whitelist precedence |
| Executor | `Services/Cleanup/CleanupExecutor.cs` | Deletes only an approved, unexpired, unchanged plan, or moves eligible non-elevated files to quarantine; logs and re-measures |
| Quarantine | `Services/Cleanup/QuarantineService.cs`, Storage and Settings cards | `quarantine\manifest.json` (original path, stored path, time, size, SHA-256, rule, reason); restore refuses occupied destinations, traversal and hash mismatches; retention/size policy from Settings |
| Freshness | `Services/Cleanup/CleanupPlanFreshness.cs` | Re-measures before deletion; material changes return `CHANGED_SINCE_SCAN` |
| Profiles | `Core/Models/CleanupProfiles.cs` | Quick, Developer, AI, Browser, Windows and Deep Review presets tick rows only; Opt-in and running apps excluded |
| History | `Services/Cleanup/CleanupHistoryService.cs` | Last 50 runs in `cleanup_history.json`; last five shown in Storage |
| Export | `Services/Cleanup/CleanupReportExporter.cs` | Measured report as Markdown or CSV under `reports\` |
| Log | `Services/Cleanup/CleanupLog.cs` | `logs\cleanup_log.txt`, append-only, 5 MB rotation |
| Whitelist | `Services/Cleanup/WhitelistService.cs`, Settings card | `whitelist.json`, always wins over a rule; add/remove/reload from Settings |
| Elevation client | `Services/Elevation/ElevationService.cs` | Starts the helper with `runas` only for an approved elevated rule; one request over a current-user-only pipe |
| Helper | `Helper/Program.cs` | Fixed targets only, one shot, stops/restarts update services, logs to `helper-YYYYMMDD.log` |
| Restore point | `Services/Elevation/ElevationService.cs` | Fixed `Checkpoint-Computer` command; UI checkbox and `--restore-point` |

Flow: **scan → numbered table → optional profile → approval command → confirm → execute → verify**.
A profile only ticks rows; the approval gate is unchanged. A plan is valid for 15 minutes and is
discarded when a new scan runs or any tick changes. Before touching a row, the executor
measures it again; a change beyond 5% / 64 KB (or 5% / 3 files) is reported as
`CHANGED_SINCE_SCAN` and nothing in that row is deleted. Opt-in rows (currently `$PatchCache$`)
are never included implicitly. Browsers must be closed; the row is unselected and the executor
re-checks the process before deleting. Machine-wide rows show a UAC prompt only when the plan
actually reaches them; declining is reported as a skip, never retried in a loop. Caution/Opt-In
plans tick "Create a System Restore point first" by default; the result is stated in the report
whether or not Windows allowed it. Completed reports are appended to `cleanup_history.json` and
can be exported as Markdown or CSV.

CLI (same executable):

```powershell
FarmanullahPCManager.exe --scan
FarmanullahPCManager.exe --clean --ids 1,2 --dry-run
FarmanullahPCManager.exe --clean --ids 1,2 --restore-point
FarmanullahPCManager.exe --clean --ids 1,2
```

`--scan` always exits without deleting. `--clean` refuses to run without an explicit `--ids`
list, so a typo cannot approve everything.

## Adding a feature

### New page

1. Write a view model in `src/ViewModels` deriving from `ViewModelBase`
   (`LoadAsync()` is called when the page is first shown; use `SetProperty` for bindings
   and `AsyncRelayCommand` for buttons).
2. Write `src/PCManager/Views/MyView.xaml` with a `partial class` code-behind.
3. Register the view model in `App.xaml.cs` (`collection.AddSingleton<MyViewModel>();`).
4. Add a `DataTemplate` mapping `vm:MyViewModel` → `views:MyView` in `App.xaml`.
5. Add a `NavigationItem` to the `Pages` collection in `MainViewModel`
   (`new("Title", "Subtitle", "Icon.Key", myViewModel)`), where the icon key exists in
   `Styles/Icons.xaml`.

### New cleaner or tool

1. Interface in `Services/Abstractions/IMyCleaner.cs`, implementation in a feature folder.
2. Register the pair in `ServiceCollectionExtensions`.
3. Inject it into the view model that should expose it and add a command.

Keep destructive code inside `SafeFileSystem` (it rejects reparse points, protects system
paths and swallows per-file failures, returning the bytes actually freed) so a cleaner
cannot delete something outside its intended roots.

## Administrator rights

The UI's `app.manifest` requests `asInvoker`, so launching it never prompts for UAC. The separate
`FarmanullahPCManager.Helper.exe` requests `requireAdministrator` and is started on demand only
when an approved plan includes a machine-wide rule or the restore-point box is ticked.

- Cleanup of `%WINDIR%\Temp`, Update downloads, Windows logs, Prefetch and `$PatchCache$` goes
  through the helper; declining UAC leaves the item as `SKIPPED_ELEVATION`.
- The helper's command set is closed: `CleanRule` with one of five fixed ids, or
  `CreateRestorePoint`. No path or command line is accepted from the client.
- Still unelevated and graceful-failure only: `HKLM\...\Run`/`StartupApproved` toggles,
  machine-wide scheduled tasks, and some Windows Update/Defender WMI counters.

Run the app from an elevated terminal if you want those remaining operations to succeed.

## Deliberately not automated

Windows blocks these for third-party applications, so the app reads the current state and
opens the matching Settings page instead (documented at the call site):

- Changing the default browser → `ms-settings:defaultapps`.
- Pinning items to the taskbar (Windows 11) → `ms-settings:taskbar`.
- Resetting Windows Update components, enabling Storage Sense, and similar policies where
  the supported path is the OS UI → the relevant `ms-settings:` or control-panel URI.

## Behaviour notes worth knowing

- Boost's temp purge only considers items older than **48 hours** (matches the original
  product); the Cleanup Center's temp rule uses **24 hours** and still skips open files.
- Cleanup scans cap each root at 250 000 files, skip reparse points/system files and use 2–4
  workers. A 16-row scan measured 4.64 GB in 18.5–22.8 s across runs on the test machine.
- The helper is a second executable copied next to the UI by the build. It exits after one reply;
  only one request per UAC consent, and no long-lived elevated service is installed.
- The helper was smoke-tested with `RunAsInvoker` (unknown rule rejected). No UAC-approved
  elevated deletion or restore-point creation has been run, so those paths are implemented but
  unverified end-to-end.
- History is capped at 50 entries. Export files are written only under the app's own data folder,
  never to a user-selected path. The history and export controls were not visually inspected.
- The scanner and executor live in one assembly; the rule roots and the runtime safety guard,
  not assembly boundaries, are what prevent deletion outside a rule.
- Startup toggles use the `StartupApproved` 12-byte binary blob (first byte even = enabled,
  odd = disabled), `.lnk` → `.lnk.disabled` renaming for the Startup folder, and
  `Disable-ScheduledTask` for scheduled tasks — never by localized display name.
- Memory "boost" calls `SetProcessWorkingSetSizeEx` with the quota flags; the reported number
  is the difference in available physical memory measured before and after.
- Settings are stored per user in `%LOCALAPPDATA%\FarmanullahPCManager\settings.json` (theme,
  thresholds, cleanup preferences, Smart Boost, floating toolbar) so the only registry write is
  the opt-in `FarmanullahPCManager` Run value used by "Start with Windows".
- Diagnostics: set `PCMANAGER_TRACE=<path>` before launching to record WPF binding and
  resource warnings to that file; unhandled exceptions always go to
  `%LOCALAPPDATA%\FarmanullahPCManager\logs\app-YYYYMMDD.log` (2 MB cap). Without the
  variable no trace listener is attached.
- Theme switching swaps the merged dictionary at index 0 of `Application.Resources`; every
  brush is referenced with `DynamicResource`, so pages restyle instantly.
