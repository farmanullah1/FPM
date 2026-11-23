# Farmanullah PC Manager

A Windows 10/11 system utility built with **C# / .NET 8 / WPF (MVVM)**: live memory boost,
deep disk cleanup, process and startup control, protection checks, a toolbox and an
always-on-top quick bar.

The application lives in [`app/`](app/) — see [`app/README.md`](app/README.md) for build
details and the internals of every service.

## Tech stack

| Area | What the code actually uses |
| --- | --- |
| Runtime | .NET 8 (`net8.0` for the model project, `net8.0-windows` for the rest), C# 12, nullable reference types enabled |
| UI framework | WPF only. The Fluent look is hand-written XAML — there is **no third-party UI framework package** in the solution |
| Look and feel | `Styles/Themes/Light.xaml` and `Dark.xaml` declare the same brush keys and are swapped at index 0 of the merged dictionaries by `ThemeService`; `Styles/Controls.xaml` holds templated cards, buttons, toggle switch, slider and pills; `Styles/Icons.xaml` holds original 24×24 `Geometry` icons |
| Windowing | Borderless shell with a custom title bar via `WindowChrome`; the floating toolbar is a transparent, topmost pill window |
| MVVM | `CommunityToolkit.Mvvm` 8.3.2 — `ObservableObject`, `[ObservableProperty]`, `RelayCommand`, `AsyncRelayCommand` |
| Composition | `Microsoft.Extensions.DependencyInjection` 8.0.1 with one registration point (`Services/DependencyInjection/ServiceCollectionExtensions.cs`); WPF-only services are registered in `App.xaml.cs` |
| System access | Win32 P/Invoke (`Services/Interop/NativeMethods.cs`), WMI via `System.Management`, `Microsoft.Win32.Registry`, `ServiceController`, `System.Drawing.Common` for screen capture |
| State | JSON settings and rolling logs under `%LOCALAPPDATA%\FarmanullahPCManager`; `Core/AppInfo.cs` is the single source of identity, paths and protected process names |

`UseWindowsForms` is enabled in the shell project for one reason only — the tray
`NotifyIcon`. Everything the user sees is WPF.

## What shipped: six screens

### Home
Hero card with live physical-memory usage (percent, used and total) and the current
temporary-file size, a **Boost** button that trims process working sets and removes temp files
older than 48 hours, and a **Smart Boost** switch that trims automatically once memory load
crosses the saved threshold (at most once a minute). Four quick-action cards jump to
Protection, Apps (processes), Storage (deep cleanup) and Apps (startup).

### Protection
Status banner driven by Defender real-time/antivirus state and Windows Update service state
(restart-pending aware), with Defender signature version and the update status spelled out.
Four tools: pop-up management (notifications settings), default browser detection (opens
Default apps, since Windows blocks programmatic changes), taskbar repair (restarts Explorer)
and restore default apps.

### Storage
System-drive usage bar, then the **Cleanup Center**: a read-only scan builds a numbered table of
sixteen measured targets — Windows Update cache, user/Windows temp, Windows logs, error reports,
shader caches, Prefetch, the installer patch cache (`$PatchCache$`), Recycle Bin, every detected
Edge/Chrome/Brave profile and Firefox `cache2`, npm, NuGet, pip, Yarn, pnpm, and the detected
caches of Antigravity, Freebuff, Cursor, VS Code and Windsurf. Every row shows its exact path,
size, file count, age, tier and identity confidence. Nothing is deleted until an approval command
(`proceed`, `only 1, 2`, `keep 1, 2`, ranges allowed) is reviewed and confirmed; a dry-run mode,
a 15-minute plan expiry, a runtime safety guard, `cleanup_log.txt` and a measured before/after
report sit behind that gate. Machine-wide rows (Windows temp/update/logs/Prefetch/`$PatchCache$`)
run through a separate `requireAdministrator` helper on demand, with UAC only when the approved
plan reaches them; a restore-point checkbox asks for a checkpoint before Caution/Opt-In plans.
Every approved row is re-measured before deletion, so a folder that changed after the scan is
skipped and must be reviewed again. Six profiles tick rows by family without approving anything.
A **quarantine** checkbox moves eligible non-elevated files into the app's own data folder
instead of deleting them; the manifest keeps the original path, size and SHA-256, the Storage
page restores any entry once its original location is free (never overwriting), and Settings
sets the retention and size cap. Completed runs are kept in `cleanup_history.json` and export
as Markdown or CSV. **Manage large
files** scans any folder with
`> 10 MB / 50 MB / 100 MB / 500 MB / 1 GB` filters, **Duplicate files** groups identical files by
size then sampled SHA-256 and reports the wasted bytes, and a **Storage Sense** switch reads and
writes the Windows setting.

### Apps
**Process management** lists running user applications with CPU, RAM and a per-row End button
(60 rows, largest first; this app and every `MSPCManager*` process are refused, as are
session-0 processes). **Startup apps** reads Run/RunOnce keys, Startup folders and Task
Scheduler, with working toggles through `StartupApproved`, `.lnk` renaming and
`Enable/Disable-ScheduledTask`; built-in `\Microsoft\` tasks are shown read-only. **App
management** lists installed Win32 apps from the HKLM/HKCU Uninstall keys with search,
version, size and uninstall, then Store/MSIX packages from `Get-AppxPackage` with framework,
system and Microsoft PC Manager packages hidden. Both uninstall buttons ask before acting;
Store removal is current-user only and re-checks the exact package first. A desktop uninstall
waits for the uninstaller, confirms the app left the registry, refreshes the list and reports
any leftover install folder without deleting it.

### Toolbox
Floating-toolbar switch (always-on-top pill with live RAM, Boost, screenshot, notepad,
calculator) plus a six-tool grid: screenshot of all monitors to a PNG in Pictures, screen
recorder (Game Bar, falling back to Steps Recorder), calculator, notepad, DNS flush
(`ipconfig /flushdns`) and quick web links.

### Settings
Start with Windows through this app's own `HKCU\...\Run` value, Smart Boost on/off with a
50–95 % threshold slider and reset, a protected-paths manager that adds/removes whitelist
entries, Light/Dark theme applied instantly, and an About block showing the data folder and
today's log path with buttons to open them.

## Architecture

Five projects with a one-way dependency chain:

```
PCManager.Core            models, formatting, identity, helper protocol (no Windows UI)
      ▲                         ▲
PCManager.Services        PCManager.Helper
system access, UI-free    elevated, fixed-command worker
      ▲
PCManager.ViewModels      page and shell view models
      ▲
PCManager                 WPF shell, styles, views  →  FarmanullahPCManager.exe
```

| Layer | Responsibility | Key files |
| --- | --- | --- |
| `Core` | Plain models, byte formatting and app identity (`ProductName`, data folder, Run value, protected process names) | `AppInfo.cs`, `Models/*.cs`, `Formatting/ByteFormatter.cs` |
| `Services` | One interface per capability in `Abstractions/`, implementations grouped by feature (`Boost`, `Cleaners`, `Cleanup`, `Process`, `Storage`, `Protection`, `Toolbox`, `Settings`, `Startup`), shared helpers in `Infrastructure/` | `DependencyInjection/ServiceCollectionExtensions.cs`, `Cleanup/CleanupScanner.cs`, `Cleanup/SafetyGuard.cs`, `Process/StartupManager.cs` |
| `Helper` | Separate `requireAdministrator` WinExe; one typed request over a current-user-only pipe, fixed targets only, no path from the client | `app/src/Helper/Program.cs`, `app/src/Helper/app.manifest` |
| `Tests` | Parser, safety-guard and helper-contract tests | `app/tests/PCManager.Tests` |
| `ViewModels` | Six page view models plus the shell; no Win32 calls, only `Services.Abstractions` | `MainViewModel.cs`, `HomeViewModel.cs`, `StorageViewModel.cs` |
| `PCManager` | XAML views, themed styles, converters, DI composition root, tray icon, floating toolbar window | `App.xaml(.cs)`, `Views/*.xaml`, `Styles/*` |

How a page is composed:

1. `MainViewModel` owns the six page view models and exposes the rail (`Pages` plus the pinned
   `SettingsItems`); `App.xaml` maps each view model to its view with a `DataTemplate`, so the
   shell's `ContentControl` needs no code-behind switching.
2. Pages never reference the shell: quick-action cards call `INavigationService`, whose
   `NavigationService` broker forwards the page key to `MainViewModel`.
3. A page talks only to interfaces (`IStorageService`, `ICleanupScanner`, `ICleanupExecutor`, …).
   The implementations move enumeration, WMI and registry work onto the thread pool, so the
   dispatcher stays responsive.
4. Anything destructive goes through the approved-plan executor, `ISafetyGuard` and
   `Infrastructure/SafeFileSystem`; each action returns the bytes actually released instead of
   assuming success.

## Quick start

```powershell
cd app
dotnet build PCManager.sln
dotnet test tests/PCManager.Tests/PCManager.Tests.csproj
dotnet run --project src/PCManager/PCManager.csproj
```

The same executable exposes the cleanup engine from a terminal:

```powershell
FarmanullahPCManager.exe --scan                    # read-only, prints the numbered table
FarmanullahPCManager.exe --clean --ids 1,2 --dry-run
FarmanullahPCManager.exe --clean --ids 1,2 --restore-point
FarmanullahPCManager.exe --clean --ids 1,2
```

Windows 10/11 with the .NET 8 SDK (`app/global.json` pins the feature band). The executable
is `app/src/PCManager/bin/Debug/net8.0-windows/FarmanullahPCManager.exe`.

## Side-by-side with Microsoft PC Manager

Everything about the identity is distinct so both utilities can be installed at once:
`FarmanullahPCManager.exe`, its own single-instance mutex, AppUserModelID, data folder
(`%LOCALAPPDATA%\FarmanullahPCManager`) and Run value. No `mspcmanager://` or `pcmanager://`
protocol is registered, and Boost / End Task never touch this process or any `MSPCManager*`
process.

## Screenshots

`app/screenshots/` contains a capture of each screen and of the floating toolbar. The Storage
capture predates the Cleanup Center; the new table and approval gate have not been captured yet.

## License & attribution

Personal and educational Windows system management. All branding, titles and icons belong to
Farmanullah PC Manager; no Microsoft visual assets are redistributed.
