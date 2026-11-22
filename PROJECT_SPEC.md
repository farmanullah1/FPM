# Farmanullah PC Manager - Project Specification & Build Audit

Audit target: the code in `app/` of this repository (`app/PCManager.sln`), commit `9a56711`
plus uncommitted working-tree changes.
Document date: 2026-11-22.

Legend used throughout: **[DONE]** implemented and working · **[PARTIAL]** implemented but
incomplete or stubbed · **[MISSING]** not implemented · **[DEFERRED]** intentionally postponed.

> Reviewer note: this document describes the code as it exists, not the target brief. Where the
> brief asked for something the code does not do, the deviation is stated explicitly in §1.1 and
> again in the relevant section. Nothing here is aspirational.

## 1. Overview

- **Purpose:** a Windows 10/11 cleanup / boost / startup-control utility with the six-screen
  layout of Microsoft PC Manager 3.23, reimplemented as an independent product.
- **Target user:** a single desktop user who wants one-click memory relief, measured disk
  cleanup, startup control, and a small toolbox without installing Microsoft's app.
- **Version:** `3.23.3` in the UI (`AppInfo.Version`), assembly/file version `3.23.3.0`.
- **Framework:** .NET 8 (`net8.0` for `PCManager.Core`, `net8.0-windows` for the other three
  projects), C# 12, nullable reference types enabled, ImplicitUsings enabled.
- **UI:** WPF with hand-written Fluent-style XAML. **Wpf.Ui is not referenced anywhere** — see
  §1.1.
- **Packages (actual, from the `.csproj` files):**

| Project | Packages |
| --- | --- |
| `PCManager.Core` | `System.Text.Json` 8.0.5 |
| `PCManager.Services` | `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.2, `System.Management` 8.0.0, `System.ServiceProcess.ServiceController` 8.0.0, `System.Drawing.Common` 8.0.7 |
| `PCManager.ViewModels` | `CommunityToolkit.Mvvm` 8.3.2, `System.Text.Json` 8.0.5 |
| `PCManager` (shell, `FarmanullahPCManager.exe`) | `Microsoft.Extensions.DependencyInjection` 8.0.1, `UseWPF`, `UseWindowsForms` (tray icon only) |

- **Size:** 99 source files (`.cs` + `.xaml`), 6,871 lines, excluding `bin/` and `obj/`.

### 1.1 Compliance with the supplied brief

| Brief item | Actual implementation | Status |
| --- | --- | --- |
| UI framework "WPF, Wpf.Ui (Fluent)" | WPF with custom XAML control templates and brush dictionaries; no Wpf.Ui package. Functionally Fluent-like (cards, 1 px borders, toggle switches, pill buttons). | **[PARTIAL]** — deliberate deviation; see §3 |
| AppUserModelID `Farmanullah.PCManager.App` | `FarmanullahPCManager.Desktop.8A4F2C1E` | **[PARTIAL]** — different value, same purpose |
| Registry root `HKCU\Software\FarmanullahPCManager\` | Not used. The app stores settings in `%LOCALAPPDATA%\FarmanullahPCManager\settings.json` and writes only the `Run` value under `HKCU\...\CurrentVersion\Run`. | **[MISSING]** — root key is never created or read (it was removed during the cleanup pass) |
| Light palette `#FBF7EE / #FFFFFF / #EAE0CC / #C89B3C` | `#F7F3E8 / #FFFFFF / #E8E0CC / #C98A00` | **[PARTIAL]** — same warm structure, different hex values (§3) |
| Dark palette `#1E1B16 / #2A2620 / #3A342B / #E4B95C` | `#1F1C16 / #2B2721 / #443C2E / #F2C14E` | **[PARTIAL]** — different hex values |
| Deep cleanup categories A (AI IDEs), B (Windows), C (dev/browser), D (own cache) | Only B (partially) and the browser part of C are implemented: update cache, Windows logs + Windows Temp, Recycle Bin, user temp, Edge/Chrome/Brave caches | **[MISSING]** for A, npm/pip/NuGet/yarn/Docker, crash dumps, DirectX shader cache, thumbnail cache, own app cache |
| Two-phase cleanup: SCAN → numbered table → STOP → `proceed`/`only N,M`/`keep N,M` | Not implemented in the application. The Storage page measures categories and the user ticks checkboxes; there is no numbered table, no approval parser and no pause state. | **[MISSING]** — see §7.3 |
| Store (MSIX) apps in App management | Only Win32 registry entries are enumerated. The Store branch exists in `UninstallAsync` but is unreachable because no entry is ever created with `IsStoreApp = true`. | **[MISSING]** for listing; **[PARTIAL]** for uninstall |

## 2. Identity & Registration (Safety-Critical)

| Property | Value | Where defined |
| --- | --- | --- |
| Assembly name | `FarmanullahPCManager` | `app/src/PCManager/PCManager.csproj` (`<AssemblyName>`) |
| Output executable | `FarmanullahPCManager.exe` | build output |
| Product / title | `Farmanullah PC Manager` | `.csproj` `<Product>`, `<AssemblyTitle>`; `AppInfo.ProductName` |
| Version | `3.23.3.0` (file), `3.23.3` (UI string) | `.csproj`, `AppInfo.Version` |
| Single-instance mutex | `Global\FarmanullahPCManager_SingleInstance_8A4F2C1E` | `AppInfo.SingleInstanceMutexName`, acquired in `App.xaml.cs` (`TryAcquireSingleInstance`) |
| AppUserModelID | `FarmanullahPCManager.Desktop.8A4F2C1E` | `AppInfo.AppUserModelId`, applied by `AppIdentity.ApplyAppUserModelId` (`SetCurrentProcessExplicitAppUserModelID`) |
| Data dir | `%LOCALAPPDATA%\FarmanullahPCManager\` | `AppInfo.DataFolder` |
| Settings file | `%LOCALAPPDATA%\FarmanullahPCManager\settings.json` | `AppInfo.SettingsFile` |
| Log dir | `%LOCALAPPDATA%\FarmanullahPCManager\logs\app-YYYYMMDD.log` | `AppInfo.LogFolder`, `AppLogger` |
| Startup Run value | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → value name `FarmanullahPCManager`, data = quoted `Environment.ProcessPath` | `AppInfo.RunKeyPath` / `AppInfo.RunValueName`, `AppStartupService` |
| URI protocols | none registered (no `mspcmanager://`, no `pcmanager://`) | verified, §9.4 |

**Non-conflict with Microsoft PC Manager — [DONE] as designed:**
- Different executable name (`FarmanullahPCManager.exe` vs the packaged `MSPCManager`).
- Different mutex name and namespace from any other utility.
- No file is written under `%LOCALAPPDATA%\Microsoft\*`; no key is written under
  `HKCU\Software\Microsoft\PCManager\*`.
- The `Run` value name is `FarmanullahPCManager`; the code never reads, writes or deletes any
  other Run value (proof: `AppStartupService` only touches `AppInfo.RunValueName`).
- Protected process list: `MSPCManager`, `MSPCManagerService`, `MPCMDRUN`,
  `FarmanullahPCManager` (`AppInfo.ProtectedProcessNames`). `AppInfo.IsProtectedProcessName`
  additionally matches any name starting with `MSPCManager`.
- Boost skips `Environment.ProcessId` and every protected name (`MemoryOptimizer.Optimize`).
- End Task refuses PID ≤ 4, session 0, the critical-name list, its own PID, and every
  protected name (`ProcessManager.EndProcess`).

## 3. Visual Theme

Palette files: `app/src/PCManager/Styles/Themes/Light.xaml` and `Dark.xaml`.
Both declare identical keys; `ThemeService` replaces merged dictionary index 0 at runtime, and
every brush is consumed through `DynamicResource`, so switching re-themes live.

| Key | Light | Dark |
| --- | --- | --- |
| `App.Background` (canvas) | `#F7F3E8` | `#1F1C16` |
| `App.Rail` (nav rail) | `#F1EAD9` | `#191712` |
| `App.Card` | `#FFFFFF` | `#2B2721` |
| `App.CardHover` | `#FBF6EA` | `#35302A` |
| `App.Border` | `#E8E0CC` | `#443C2E` |
| `App.Text` | `#2A241A` | `#FFF8E7` |
| `App.Subtle` (captions) | `#7A6E55` | `#C9BFA6` |
| `App.Accent` | `#C98A00` | `#F2C14E` |
| `App.AccentSoft` | `#FBF0D2` | `#3A3020` |
| `App.AccentText` (on accent) | `#FFFFFF` | `#241C0C` |
| `App.Success` | `#4F7A28` | `#9BC46A` |
| `App.Warning` | `#B26A00` | `#F2C14E` |
| `App.Critical` | `#B3261E` | `#FF9A8F` |
| `App.Selection` | `#F5E8C6` | `#3E3524` |
| `App.Track` (toggle/slider track) | `#EFE7D3` | `#3A342A` |

Deviation from the brief: the brief's hexes (`#FBF7EE`, `#C89B3C`, `#1E1B16`, `#E4B95C`, …)
differ from the shipped values above. Structure, key names and role of every brush match; only
the numbers differ. **[PARTIAL]**

Card / control tokens (defined in `Styles/Controls.xaml`):

| Token | Value |
| --- | --- |
| `Card` | `CornerRadius=10`, `BorderThickness=1` (`App.Border`), `Padding=18`, `Margin=0,0,0,12`, background `App.Card` |
| `RowCard` | based on `Card`: `Padding=14`, `Margin=0,0,0,8` |
| `ActionCard` (button) | `CornerRadius=8`, 1 px border, `Padding=16`, margin `0,0,12,12`, hover tints background and border to accent |
| `PrimaryButton` / `SecondaryButton` | `CornerRadius=6`, padding `16,8`, disabled opacity 0.4 |
| `PillButton` | `CornerRadius=14`, padding `14,6`, 12 px font |
| `ToggleSwitch` | 40×20 track, 12 px knob, accent fill when checked, 0.45 opacity when disabled |
| `FluentSlider` | 4 px track, 18 px accent thumb |
| `CaptionButton` (title bar) | 46×32, transparent, hover `App.CardHover` |
| `ThinProgress` | 6 px, accent indicator on border-coloured track |
| `CardList` | shared `ItemsControl` template: `MaxHeight=320`, vertical `ScrollViewer` |
| Page spacing | page padding `28,22,28,28`; card gaps `12`; quick-action grid `UniformGrid Columns=2` |

Note: the brief asked for `CornerRadius=8` on cards; `Card` uses **10** (the action cards are 8).
**[PARTIAL]** cosmetic.

Icons: `Styles/Icons.xaml`, original 24×24 `Geometry` drawings (shield, gear, home, process,
storage, toolbox, screenshot, calculator, notepad, browser, taskbar, info, network, links,
cleanup, startup, pill, record, close/minimise/maximise). No Microsoft artwork is used.

## 4. Navigation Shell

- File: `app/src/PCManager/Views/MainWindow.xaml`, VM `MainViewModel`.
- Window: 1280×820, min 1020×660, `WindowStyle=None`, custom 44 px title bar with
  `WindowChrome` (`CaptionHeight=0`, `ResizeBorderThickness=6`), logo + "Farmanullah PC Manager"
  + `v3.23` badge, minimise/maximise/close buttons wired to code-behind handlers.
- Layout: fixed 236 px rail (`App.Rail`) + `ContentControl` hosted page.
- Rail is **two `ListBox`es** sharing `SelectedItem="{Binding SelectedPage}"`:
  - main list bound to `Pages`: Home, Protection, Storage, Apps, Toolbox (in that order);
  - bottom list bound to `SettingsItems`: Settings, pinned under a separator, with the version
    label below it.
- Both lists use one shared `DataTemplate` (`NavigationItemTemplate`) with icon + title +
  subtitle. Selection highlight comes from the `ListBoxItem` `IsSelected` trigger; no per-item
  selection flag exists.
- Page view resolution: `App.xaml` `DataTemplate`s map each page VM to its view, so the shell has
  no view-switching code.
- Navigation from inside pages goes through `INavigationService`; `MainViewModel` assigns the
  handler (`navigation.Handler = SelectPage`), avoiding a shell↔page dependency.
- Selecting a page sets `CurrentPage` and calls that page's `LoadAsync()` once per selection.
- Status: **[DONE]**

## 5. Screens

Each heading lists the file and every visible element with its binding or command.

### 5.1 Home — `Views/HomeView.xaml` + `ViewModels/HomeViewModel.cs`
- Hero card: `LoadText` (big %), `ThinProgress` bar bound to `LoadPercent`, `AvailableText`
  ("x free of y"), `TempFilesText`, `BoostCommand`, `SmartBoostEnabled` toggle,
  `StatusMessage`, `ResultText`. **[DONE]**
- Live values poll every 4 s (`DispatcherTimer` → `GlobalMemoryStatusEx`).
- Boost = working-set trim (`SetProcessWorkingSetSizeEx(-1,-1)`) then temp purge older than 48 h;
  result line reports working sets trimmed, available memory before → after with the signed
  delta, and the temp bytes actually removed. No "reclaimed" figure is shown, because a
  working-set trim does not free memory. **[DONE]**
- Smart Boost: when enabled and `LoadPercent >= SmartBoostThresholdPercent`, trims at most once
  per minute. **[DONE]**
- 2×2 quick cards → `OpenProtectionCommand`, `OpenProcessesCommand`, `OpenCleanupCommand`,
  `OpenStartupCommand`. **[DONE]** (the "Health check" card navigates to Protection, which holds
  the Defender/Update detail; there is no separate health-check engine any more.)

### 5.2 Protection — `Views/ProtectionView.xaml` + `ViewModels/ProtectionViewModel.cs`
- Status banner: `BannerTitle`, `BannerDetail`, shield icon tinted by
  `BannerHealthy → HealthyToBrush` (success vs warning). **[DONE]**
- "Check for updates" (`CheckUpdatesCommand`): reads service/reboot state, then opens
  `ms-settings:windowsupdate` (Windows exposes no supported programmatic check that this app
  uses). **[DONE]** with that limitation.
- Security detail cards: `DefenderDetail` (status text + signature version),
  `UpdateDetail` (service state / restart pending). **[DONE]**
- Tool cards: `PopupCommand` (`ms-settings:notifications`), `BrowserCommand` (reads current
  browser, message + `ms-settings:defaultapps`), `TaskbarCommand` (kills and restarts
  `explorer.exe`), `RestoreAppsCommand` (`ms-settings:defaultapps`). **[DONE]**
- `RefreshCommand` re-runs the whole check; `StatusMessage` shows the check time.
- Not implemented here: programmatic default-browser change (blocked by Windows), pop-up ad
  blocking (only opens the OS notification page), Windows Update installation.

### 5.3 Storage — `Views/StorageView.xaml` + `ViewModels/StorageViewModel.cs`
- Drive card: `SystemDriveName`, `DriveUsedPercent` bar, `DriveUsedText`, `DriveFreeText`,
  `DriveSummary` (format + free-of-total). **[DONE]** for the system/fixed drive only.
- Deep cleanup: `CleanupCategories` rows (`CheckBox IsSelected`, `Title`, `Description`,
  `SizeText`), `CleanupTotalText` ("x selected"), `CleanSelectedCommand`, `ScanCleanupCommand`
  ("Rescan"). Ten categories, sizes measured before any deletion. **[PARTIAL]** against the
  brief: Windows, browser, npm, NuGet, `$PatchCache$` and package-manager caches are covered;
  AI-IDE caches, crash dumps and the app's own cache are not (§7.4).
- Manage large files: `ScanPath` text box, `OpenScanFolderCommand`, four filter pills calling
  `FilterLargeFilesCommand` with parameters `10`, `50`, `100`, `1024` (MB), `LargeFileFilterText`,
  `LargeFileSummary`, `LargeFiles` (`Path`, `SizeText`), capped at 200 results. **[DONE]**
- Duplicate files: `FindDuplicatesCommand`, `DuplicateSummary`, `DuplicateGroups`
  (`DisplayName`, first path, `CountText`, `WasteText`). Read-only report — there is no
  "delete duplicates" action. **[PARTIAL]** by design (detection only).
- Storage Sense: `StorageSenseEnabled` toggle reading/writing
  `HKCU\...\StorageSense\Parameters\StorageSense` value `01`. **[DONE]**
- `StatusMessage` at the bottom; drive and cleanup re-measured after a clean.

### 5.4 Apps — `Views/AppsView.xaml` + `ViewModels/AppsViewModel.cs`
- Process management: `ProcessSummary` ("n applications · x GB"), `RunningApps` rows with
  `Name`, `PidText`, `CpuText`, `MemoryText`, `EndCommand`; 60 rows max, sorted by RAM; a 5 s
  timer refreshes while `AutoRefreshProcessList` is on. Self and `MSPCManager*` are filtered out
  before the list is built and refused again in `ProcessManager.EndProcess`. **[DONE]**
- Startup apps: `RefreshStartupCommand`, `StartupApps` rows with `Name`, `Command`, `SourceText`,
  `IsEnabled` two-way toggle, `CanToggle` (false for `\Microsoft\` tasks). Sources: HKCU/HKLM
  `Run` and `RunOnce` (including WOW6432Node), per-user and all-users Startup folders, and Task
  Scheduler via PowerShell. **[DONE]**
- App management: `AppSearch` (filters on name/publisher), `InstalledSummary`,
  `InstalledApps` rows with `Name`, `Subtitle` (publisher or "Desktop app"), `VersionText`,
  `SizeText`, `UninstallCommand`; from HKLM 64-bit, HKLM 32-bit and HKCU `Uninstall` keys.
  **[DONE]** for Win32; **Store/MSIX apps are not listed [MISSING]**.
- No admin elevation: HKLM startup toggles and elevated process ends return "access denied".

### 5.5 Toolbox — `Views/ToolboxView.xaml` + `ViewModels/ToolboxViewModel.cs`
- `ShowFloatingToolbar` toggle: persists to settings and shows/hides the pill through
  `IFloatingToolbarHost`. **[DONE]**
- `Tools` grid (`UniformGrid Columns=3`) bound to `ToolboxTool` items (icon via
  `IconKeyToGeometryConverter`, `Title`, `Description`), each invoking
  `LaunchToolCommand` with its `Id`. **[DONE]** for all six tools:
  - `screenshot` — GDI capture of the whole virtual desktop (all monitors) to
    `%USERPROFILE%\Pictures\Farmanullah PC Manager Screenshots\Screenshot_yyyyMMdd_HHmmss.png`,
    then opens it;
  - `screen-recorder` — opens Game Bar (`ms-gamebar:`), falls back to `psr.exe`;
  - `calculator` / `notepad` — shell-launch `calc.exe` / `notepad.exe`;
  - `dns-flush` — `ipconfig /flushdns` (20 s timeout), reports exit code;
  - `quick-links` — opens Bing, GitHub and Microsoft Learn.
- Note: `calc.exe` is only a stub on machines without the Calculator package (observed on this
  test machine); the launch itself succeeds.

### 5.6 Settings — `Views/SettingsView.xaml` + `ViewModels/SettingsViewModel.cs`
- Start with Windows: `StartWithWindows` toggle writing only the `FarmanullahPCManager` Run
  value, with `RunValueText` showing `HKCU\Software\Microsoft\Windows\CurrentVersion\Run →
  FarmanullahPCManager`. **[DONE]**
- Smart Boost: `SmartBoostEnabled` toggle, `SmartBoostThreshold` slider (50–95, snaps to 1 %),
  `SmartBoostThresholdText`, `ResetThresholdCommand` (75 %). **[DONE]**
- Appearance: `IsDark` toggle, `ThemeLabel`; writes `settings.json` and swaps the dictionary.
  **[DONE]** (Dark palette exists; not visually verified — §9.3).
- About: `ProductName`, `VersionText`, `DataFolderPath`, `LogFilePath`,
  `OpenDataFolderCommand`, `OpenLogFolderCommand` (both via `IToolLauncher.OpenFolder`).
  **[DONE]**
- The brief asked the About panel to show data **and registry** paths; only the Run-value text
  above shows a registry path, because no other registry key is used. **[PARTIAL]**

### 5.7 Floating toolbar (not a tab) — `Views/FloatingToolbarWindow.xaml(.cs)` + `FloatingToolbarViewModel`
- Borderless, transparent, topmost, `ShowInTaskbar=False`, `SizeToContent`, drop shadow, 20 px
  radius; draggable via `MouseLeftButtonDown → DragMove`; ✕ calls `Hide()`.
- Contents: shield icon, `LoadText` (%), `FreeText`, divider, Boost / Shot / Note / Calc pill
  buttons, close button. Live values refresh every 3 s.
- Position: placed near the top-right of the work area on first creation; **not persisted**
  across restarts. **[PARTIAL]**

## 6. Services

All interfaces live in `app/src/Services/Abstractions/`; implementations are grouped per feature.
Everything is registered once in
`app/src/Services/DependencyInjection/ServiceCollectionExtensions.cs`; WPF-only services
(`IThemeService`, `IFloatingToolbarHost`, `NavigationService`) are registered in `App.xaml.cs`.

| Service (brief name) | Files | Public members | Safety rules enforced | Status |
| --- | --- | --- | --- | --- |
| PC Boost | `Boost/PcBoostService.cs`, `Memory/MemoryOptimizer.cs`, `Abstractions/IPcBoostService.cs`, `IMemoryOptimizer.cs` | `GetMemorySnapshot()`, `OptimizeAsync()`, `CleanTempFilesAsync()` | Skips PID ≤ 4, session 0, own PID, all protected names; worst case Windows ignores the trim; temp purge only files older than 48 h and never locked files | **[DONE]** |
| ProcessManager | `Process/ProcessManager.cs`, `Abstractions/IProcessManager.cs` | `GetProcesses()`, `EndProcess(pid)` | Refuses PID ≤ 4, `system/idle/smss/csrss/wininit/winlogon/services/lsass/svchost`, session 0, own PID, protected names; CPU measured from `TotalProcessorTime` deltas | **[DONE]** |
| Startup | `Process/StartupManager.cs`, `Startup/AppStartupService.cs`, `Abstractions/IStartupManager.cs`, `IAppStartupService.cs` | `GetStartupApps()`, `SetEnabled(app, bool)`, `IsEnabled()`, `SetEnabled(bool)` | Toggles via `StartupApproved` 12-byte blob, `.lnk` rename, `Enable/Disable-ScheduledTask`; `\Microsoft\` tasks refused; own Run value only | **[DONE]** |
| DeepCleanup | `Storage/DeepCleanupService.cs`, `Infrastructure/FolderCleaner.cs`, `Abstractions/IDeepCleanupService.cs` | `ScanAsync()`, `CleanAsync(categories)` | Fixed folder roots only; per-file failures skipped; bytes measured before delete; category reported "skipped" when a delete was blocked; recycle-bin emptying goes through shell API | **[DONE]** for its five categories, **[MISSING]** for the brief's extra categories |
| Large-file scanner | `Storage/StorageAnalyzer.cs`, `Storage/StorageService.cs`, `Abstractions/IStorageAnalyzer.cs`, `IStorageService.cs` | `GetDrives()`, `FindLargeFiles(path, min, max, token)`, facades `GetDrivesAsync`, `FindLargeFilesAsync`, `GetStorageSenseEnabled`, `SetStorageSenseEnabled` | Read-only scan; reparse points and system files skipped; 400 000 file cap; 200 result cap | **[DONE]** |
| DuplicateFinder | `Storage/DuplicateFinderService.cs`, `Abstractions/IDuplicateFinderService.cs` | `FindDuplicatesAsync(root, minSize, token)` | Read-only; groups by exact size then SHA-256 over length + first/last 32 KB; skips inaccessible files; 80 000 file cap, 100 groups | **[DONE]** detection; no delete action |
| Floating toolbar | `PCManager/Views/FloatingToolbarWindow.*`, `PCManager/Services/FloatingToolbarHost.cs`, `ViewModels/FloatingToolbarViewModel.cs`, `IFloatingToolbarHost.cs` | `IFloatingToolbarHost.Show()/Hide()/IsVisible`; VM commands Boost/Screenshot/Notepad/Calculator | Cannot close the main window's process; hides instead of closing; shares boost service with Home | **[DONE]** |
| Theme | `PCManager/Services/ThemeService.cs`, `ViewModels/IThemeService.cs` | `Current`, `Apply(ThemeOption)` | Only swaps index 0 of merged dictionaries; `System` reads `AppsUseLightTheme` | **[DONE]** |
| Settings | `Settings/SettingsService.cs`, `Abstractions/ISettingsService.cs` | `Current`, `Save()` | JSON under `%LOCALAPPDATA%\FarmanullahPCManager`; corrupt file falls back to defaults; never writes under a Microsoft folder | **[DONE]** |
| Log | `Infrastructure/AppLogger.cs`, `Abstractions/IAppLogger.cs` | `LogFilePath`, `Info()`, `Error()` | Serialised writes, 2 MB/day cap, failures swallowed | **[DONE]** |

Supporting services not named in the brief (also live): `ITempFileCleaner` (48 h rule),
`IBrowserCacheCleaner` (cache folders only), `IRecycleBinService` (`SHQueryRecycleBin` /
`SHEmptyRecycleBin`), `IStorageSenseService`, `IDefenderStatusService` (WMI
`MSFT_MpComputerStatus`, fallback `AntiVirusProduct`), `IWindowsUpdateStatusService`
(`wuauserv` + reboot flags), `IProtectionService`, `IInstalledAppService`, `ISystemRepairService`,
`IDefaultBrowserService`, `IToolLauncher`, `IToolboxService`, `IProcessService`.

## 7. Deep Cleanup Workflow

### 7.1 What the application actually does (Phase 1 + 2 combined)
1. `StorageViewModel.LoadAsync()` → `RefreshDriveAsync()` then `ScanCleanupAsync()`.
2. `DeepCleanupService.ScanAsync()` measures five categories in one `Task.Run`
   (`FolderCleaner.Measure`, `IRecycleBinService.GetSize`, `ITempFileCleaner.GetReclaimableBytes`,
   `IBrowserCacheCleaner.GetReclaimableBytes`). Read-only; nothing is deleted.
3. The page rebuilds `CleanupCategories` and shows each `SizeText`; all categories are selected
   by default (`CleanupCategory.IsSelected = true`), and `CleanupTotalText` sums the ticked ones.
   `CleanSelectedCommand` is disabled when nothing is ticked.
4. `CleanSelectedAsync()` → `DeepCleanupService.CleanAsync(selected)`:
   for each `IsSelected && !IsCleaned` category it deletes inside the fixed root, records released
   bytes, decrements the displayed size, marks `IsCleaned`, and counts `cleaned`/`skipped`.
   A category that had bytes but released none is reported as skipped (elevation or locks).
5. Result message (`CleanupOutcome.Message`), categories re-rendered, drive usage refreshed.

### 7.2 Numbered table / approval gate
**Not implemented.** There is no numbered table in the UI and no wait-for-command state.
`grep -niE "proceed|only [0-9]|keep [0-9]|numbered table" app/src` returns nothing, and no
parser, regex or command loop exists. **[MISSING]**

The interactive table/`proceed`/`only N,M`/`keep N,M` protocol described in the brief was
performed **manually by the agent earlier in the conversation** (a PowerShell size scan plus a
Markdown table), not by the application. It is not reproducible from the exe.

### 7.3 Parser
No parser exists to document. If the reviewer requires in-app two-phase cleanup, this is new work
(§10).

### 7.4 Whitelist currently implemented

| Category id | Root(s) touched | Notes |
| --- | --- | --- |
| `updates` | `%WINDIR%\SoftwareDistribution\Download` | update payload cache; needs elevation to delete |
| `logs` | `%WINDIR%\Logs`, `%WINDIR%\Temp` | reported skipped without admin |
| `recycle` | Recycle Bin of all drives (shell API) | destructive and accepted as such by the user |
| `temp` | `Path.GetTempPath()`, `%WINDIR%\Temp`, `%LOCALAPPDATA%\Temp` | files older than 48 h, non-zero length |
| `browser` | Edge/Chrome/Brave `Default\Cache` and `Default\Code Cache`, `%LOCALAPPDATA%\Microsoft\Windows\INetCache`, `WebCache` | caches only |
| `patchcache` | `%WINDIR%\Installer\$PatchCache$` | MSP patches for installed updates; skipped without admin; repairing an older update may need the patch again |
| `firefox` | `%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2` (profiles discovered at scan time) | measured while Firefox runs but **deleted only when Firefox is closed**; cookies/history/logins untouched |
| `npm` | `%LOCALAPPDATA%\npm-cache`, `%APPDATA%\npm-cache` | content-addressed package cache; re-downloaded on next install |
| `nuget` | `%LOCALAPPDATA%\NuGet\v3-cache`, `%LOCALAPPDATA%\NuGet\plugins-cache`, `%TEMP%\NuGetScratch` | http/plugin caches + scratch; restored by the next build |
| `packagemanagers` | `%LOCALAPPDATA%\pip\Cache`, `%LOCALAPPDATA%\Yarn\Cache`, `%LOCALAPPDATA%\Yarn\Berry\cache`, `%LOCALAPPDATA%\pnpm\store`, `%LOCALAPPDATA%\pnpm-store` | download stores only; project files untouched |

Large-file scan and duplicate scan follow whatever folder the user picks in `ScanPath`
(default `%USERPROFILE%`); they are read-only.

### 7.5 Blocklist (enforced by omission, not by a runtime filter)
Nothing in the code ever targets: `settings.json`, browser cookies/passwords/history/bookmarks,
`Local State`, extensions, chat databases, API keys or auth tokens, `%LOCALAPPDATA%\Microsoft\*`,
`HKCU\Software\Microsoft\PCManager\*`. The browser cleaner only enumerates the cache folders
listed above, and the temp cleaner only its three roots — so these files are unreachable by
construction. There is **no** generic path-blocklist guard: `SafeFileSystem` is
enumeration-only (IgnoreInaccessible, skip reparse points and system files), and deletion safety
comes from the fixed root lists plus per-file exception handling.

## 8. Data & Registry Layout

```
%LOCALAPPDATA%\FarmanullahPCManager\
├─ settings.json                  Theme, AutoRefreshProcessList, LargeFileThresholdBytes,
│                                StartWithWindows, SmartBoostEnabled,
│                                SmartBoostThresholdPercent, ShowFloatingToolbar
└─ logs\
   └─ app-YYYYMMDD.log            timestamped INFO/ERROR, 2 MB/day cap
```

Observed on the test machine: `settings.json` with `Theme=3` (System),
`LargeFileThresholdBytes=104857600`, `SmartBoostThresholdPercent=75`,
`ShowFloatingToolbar=false`, `StartWithWindows=false`; one log file `app-20261122.log`.

Registry access (complete list):

| Key | Value | Access | Purpose |
| --- | --- | --- | --- |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | `FarmanullahPCManager` | read/write/delete | Start with Windows (only this value) |
| `HKCU\...\Explorer\StartupApproved\Run`, `...\StartupApproved\Run32`, `...\StartupApproved\StartupFolder` | per-entry binary blob | read/write | startup toggles |
| `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, `RunOnce`, `WOW6432Node\...\Run`, and the same under HKCU | all values | read | startup listing |
| `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending`, `...\WindowsUpdate\Auto Update\RebootRequired`, `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager` (`PendingFileRenameOperations`) | – | read | Windows Update reboot state |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StorageSense` | `01` (DWORD) | read/write | Storage Sense toggle |
| `HKLM\...\Uninstall` (64/32) and `HKCU\...\Uninstall` | DisplayName, DisplayVersion, Publisher, InstallLocation, UninstallString, QuietUninstallString, EstimatedSize, SystemComponent, ParentKeyName, ReleaseType | read | installed apps |
| `HKCU\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice` | `ProgId` | read | current browser |
| `HKCR\<ProgId>\shell\open\command` | default | read | friendly browser name |
| `HKCU\...\Themes\Personalize` | `AppsUseLightTheme` | read | `ThemeOption.System` |

`HKCU\Software\FarmanullahPCManager\` from the brief is **not created or read** **[MISSING]**.

## 9. Build & Verification

### 9.1 Build
```
cd app
dotnet build PCManager.sln -nodeReuse:false -m:1
```
Last result (2026-11-22): **Build succeeded, 0 Warning(s), 0 Error(s)**.
Notes: build from `app/`; `global.json` pins the 8.0 feature band, SDK 8.0.428 installed.
Parallel builds of the solution can transiently hit `MSB3026` when the IDE's language server holds
a DLL; `-m:1` and `-nodeReuse:false` avoid it. Passing `-m:1` is not required for correctness.

### 9.2 Run
- Launches to the Home page with live values (memory %, temp size, drive usage, process list,
  Defender/Update state). Verified by UI Automation capture of all six screens.
- Second launch exits immediately (mutex works) — §9.4.

### 9.3 Screenshots (Light theme only)
`app/screenshots/`:
`Home.png`, `Protection.png`, `Storage.png`, `Apps.png`, `Apps-bottom.png`, `Apps-late.png`,
`Toolbox.png`, `Settings.png`, `FloatingToolbar.png`.

**Dark theme: no screenshot captured [MISSING]** — the palette and `ThemeService` exist, but the
dark rendering has not been visually verified.

### 9.4 Identity / collision tests actually performed
- `dotnet`-built exe launched; window title read back as **"Farmanullah PC Manager"** ✓.
- Second instance started while the first was running: **exited within 4 s** ✓ (single-instance
  mutex effective).
- `HKCU\Software\Classes\mspcmanager` and `...\pcmanager`: **do not exist** ✓.
- `Run` value `FarmanullahPCManager`: **absent** after the test (toggled on and back off; the
  only write seen in the log) ✓.
- Microsoft PC Manager **is installed** on this machine as MSIX
  `Microsoft.MicrosoftPCManager_3.23.3.0_x64__8wekyb3d8bbwe` under
  `C:\Program Files\WindowsApps\…`. Both applications are installed side by side with distinct
  exe names, mutex, data and registry value names; **a simultaneous run of both was not
  executed [PARTIAL]**, and no Microsoft binary was launched or modified.
- No elevation path has been exercised; HKLM/system operations were only observed failing
  gracefully.

### 9.5 Phase-1 scan sample (manual, not an app feature)
A read-only PowerShell scan performed earlier in the session produced, on this machine:

| # | Category | Path | Size |
| --- | --- | --- | --- |
| 1 | User temp | `%LOCALAPPDATA%\Temp` | 1.79 GB |
| 2 | Windows Update cache | `C:\Windows\SoftwareDistribution\Download` | 1.51 GB |
| 3 | npm cache | `%LOCALAPPDATA%\npm-cache` | 1.94 GB |
| 4 | Installer patch cache | `C:\Windows\Installer\$PatchCache$` | 529 MB |
| 5 | Brave caches | `…\Brave-Browser\User Data\Default\Cache` + `Code Cache` | 225 MB |
| 6 | Chrome caches | `…\Chrome\User Data\Default\Cache` + `Code Cache` | 19.3 MB |
| 7 | Firefox cache2 | `…\Mozilla\Firefox\Profiles\<profile>\cache2` | 8.5 MB |
| 8 | NuGet http cache | `%LOCALAPPDATA%\NuGet\v3-cache` | 7.9 MB |
| 9 | Cursor CachedData | `%APPDATA%\Cursor\CachedData` | 578 KB |
| 10 | VS Code cache/logs | `%APPDATA%\Code\Code Cache`, `…\logs` | 112 KB |

Total ≈ 6.02 GB. Items 3, 4, 7–10 are **not** covered by the app's Deep Cleanup.

## 10. Known Gaps & Next Steps

Enumerated [PARTIAL]/[MISSING] items:

1. **[MISSING]** Wpf.Ui package — the Fluent look is custom XAML (functional equivalent).
2. **[PARTIAL]** AppUserModelID differs from the brief (`FarmanullahPCManager.Desktop.8A4F2C1E`
   vs `Farmanullah.PCManager.App`).
3. **[MISSING]** `HKCU\Software\FarmanullahPCManager\` is never created; no registry root is used.
4. **[PARTIAL]** Palette hex values differ from the brief (structure identical).
5. **[PARTIAL]** Card corner radius is 10, brief says 8.
6. **[MISSING]** Cleanup categories A (AI IDE/agent caches: Antigravity, Freebuff, Omnirush,
   VS Code, Cursor), C-dev (npm, pip, NuGet, yarn, Docker build cache), crash dumps,
   DirectX shader cache, thumbnail cache, and D (own app cache).
7. **[MISSING]** Two-phase SCAN → numbered table → STOP → `proceed`/`only N,M`/`keep N,M`
   protocol and its parser; Storage uses checkboxes instead.
8. **[MISSING]** Store/MSIX app enumeration in App management (uninstall branch exists but is
   unreachable); Win32 uninstallers launch without waiting or refreshing the list afterwards.
9. **[PARTIAL]** Duplicate finder detects and reports only; no delete/keep action, and it hashes
   file edges (length + first/last 32 KB) rather than the whole file.
10. **[PARTIAL]** Floating toolbar position is not persisted between runs.
11. **[MISSING]** Dark-theme screenshots / visual verification.
12. **[MISSING]** Automated tests of any kind (no test project).
13. **[PARTIAL]** Collision test with Microsoft PC Manager limited to side-by-side installation
    and identity checks; not a simultaneous runtime test.
14. **[MISSING]** Elevated helper for system temp, update cache and HKLM startup toggles; those
    report "access denied" or "skipped" instead.
15. **[MISSING]** In-app junk scan of package-manager/IDE caches despite it being the largest
    measured win on this machine (≈4.8 GB of the 6.02 GB found).

Prioritized backlog (highest value first):

1. Extend `IDeepCleanupService` with the brief's category A/C/D roots behind the same
   measure-then-delete contract, keeping cookies/credentials out of scope.
2. Add the two-phase approval gate (numbered table, typed command parsing, explicit stop) if the
   reviewer requires it as product behaviour rather than an agent workflow.
3. Store/MSIX app listing and uninstall through `Get-AppxPackage`/Settings.
4. Full-file hashing option for duplicates plus a "delete selected copies" action with recycle-bin
   safety.
5. Dark-theme visual pass and screenshots; verify contrast of accent on canvas.
6. Test project for `SafeFileSystem`-based cleaners, `ByteFormatter`, duplicate grouping and
   cleanup selection math.
7. Optional elevated helper for the four machine-wide operations.
8. Persist floating-toolbar position; add "start minimised to tray".
