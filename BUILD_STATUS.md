# Build Status Snapshot

Snapshot date: **2026-11-22**. Code state: commit `9a56711` plus uncommitted working-tree changes.
All statements below were verified on the machine that produced the screenshots; nothing is
assumed.

## Environment

| Item | Value |
| --- | --- |
| OS | Windows 11 Home, build **26300** |
| .NET SDK | **8.0.428** (`app/global.json` pins `8.0.0`, `rollForward: latestFeature`) |
| Runtime target | `net8.0` (Core), `net8.0-windows` (Services, ViewModels, shell) |
| UI framework | WPF — **no Wpf.Ui** (deviation from the brief, see PROJECT_SPEC §1.1) |
| MVVM / DI | `CommunityToolkit.Mvvm` 8.3.2, `Microsoft.Extensions.DependencyInjection` 8.0.1 |
| IDE | Antigravity (language server holds project DLLs, which can cause transient `MSB3026` build retries) |
| Build host path | `app/PCManager.sln` |

## Build

| Field | Value |
| --- | --- |
| Command | `cd app && dotnet build PCManager.sln -nodeReuse:false -m:1` |
| Result | **PASS** |
| Warnings | **0** |
| Errors | **0** |
| Timestamp | 2026-11-22 (last run of this session) |
| Output | `app/src/PCManager/bin/Debug/net8.0-windows/FarmanullahPCManager.exe` |

Note: building the solution in parallel can produce `MSB3026` copy-retry warnings when the IDE's
language server has a project DLL open. The retries succeed and the build still passes; `-m:1`
removes the noise. The earlier `WFAC010` (duplicate DPI configuration) warning was fixed by
removing the DPI block from `app.manifest` (DPI comes from `<ApplicationHighDpiMode>`).

## Run

| Check | Result |
| --- | --- |
| App launches (Debug build) | **Y** — starts on Home with live memory %, temp size, drive usage |
| Mutex blocks a second instance | **Y** — second process exited within 4 s while the first stayed alive |
| Window title reads "Farmanullah PC Manager" | **Y** — read back via UI Automation |
| Six rail entries + pinned Settings render | **Y** — captured in `app/screenshots/` |
| Floating toolbar opens from the Toolbox toggle | **Y** — captured |
| Tray icon | **Not verified visually** — `NotifyIcon` code exists (Open / Boost now / Exit, double-click restores the window) but its presence in the notification area was not confirmed in this session |
| `mspcmanager://` or `pcmanager://` registered | **N** (both `HKCU\Software\Classes` keys absent) |
| `Run` value written by the test session | cleaned up — value **absent** afterwards |

## Feature Completion Matrix

| Area | Status | Notes |
| --- | --- | --- |
| Identity / exe | **[DONE]** | `FarmanullahPCManager.exe`, product "Farmanullah PC Manager", v3.23.3.0 |
| Mutex | **[DONE]** | `Global\FarmanullahPCManager_SingleInstance_8A4F2C1E`; second launch exits |
| AppUserModelID | **[PARTIAL]** | `FarmanullahPCManager.Desktop.8A4F2C1E`, not the brief's `Farmanullah.PCManager.App` |
| Data paths | **[DONE]** | `%LOCALAPPDATA%\FarmanullahPCManager\settings.json` + `logs\`; no Microsoft folder touched |
| Registry paths | **[PARTIAL]** | Only the `FarmanullahPCManager` Run value (plus read-only startup/update/StorageSense/app keys). Brief's `HKCU\Software\FarmanullahPCManager\` is unused |
| Theme Light | **[DONE]** | Custom palette in `Styles/Themes/Light.xaml`; hex values differ from the brief |
| Theme Dark | **[PARTIAL]** | Palette + runtime swap implemented; **not visually verified, no screenshot** |
| Home tab | **[DONE]** | Hero (RAM %, temp, Boost, Smart Boost) + 2×2 quick cards |
| Protection tab | **[DONE]** | Banner, Defender/Update detail, four repair cards; update "check" opens Settings |
| Storage tab | **[PARTIAL]** | Drive bar, 5 measured categories, large-file filters, duplicates (read-only), Storage Sense. Brief's AI-IDE/dev/own-cache categories missing |
| Apps tab | **[PARTIAL]** | Processes + startup **DONE**; installed apps Win32 **DONE**, Store/MSIX listing **MISSING** |
| Toolbox tab | **[DONE]** | Floating toggle + six tools |
| Settings tab | **[DONE]** | Start with Windows, Smart Boost, theme, About paths (registry path shown only as the Run-value line) |
| Wpf.Ui requirement | **[MISSING]** | Deliberate deviation: hand-written Fluent XAML instead |
| PCBoostService | **[DONE]** | Snapshot, working-set trim, 48 h temp purge; skips self + MSPCManager |
| ProcessManager | **[DONE]** | CPU deltas, RAM, End Task with system/protected refusals |
| StartupService | **[DONE]** | Run/RunOnce (HKCU+HKLM+WOW64), Startup folders, Task Scheduler toggles |
| DeepCleanup Phase 1 | **[PARTIAL]** | In-app scan + sized checkboxes; no numbered table, no STOP gate |
| DeepCleanup Phase 2 | **[PARTIAL]** | Deletes only ticked categories and reports bytes, but no `proceed` / `only N,M` / `keep N,M` parser exists |
| Floating toolbar | **[PARTIAL]** | Works, draggable, always-on-top; position not persisted |
| Self / MSPC exclude | **[DONE]** | Filtered from the list, refused in `EndProcess`, skipped in `Optimize` |
| Automated tests | **[MISSING]** | No test project in the solution |
| Elevation | **[MISSING]** | No elevated helper; HKLM/system operations report access denied / skipped |

## Screenshots

All captured **Light theme only** (`app/screenshots/`):

| Tab | File |
| --- | --- |
| Home | `app/screenshots/Home.png` |
| Protection | `app/screenshots/Protection.png` |
| Storage | `app/screenshots/Storage.png` |
| Apps | `app/screenshots/Apps.png`, `app/screenshots/Apps-bottom.png`, `app/screenshots/Apps-late.png` |
| Toolbox | `app/screenshots/Toolbox.png` |
| Settings | `app/screenshots/Settings.png` |
| Floating toolbar | `app/screenshots/FloatingToolbar.png` |

**Dark theme: none [MISSING].**

## Manual verification observed

- Home: 42–45 % memory, 1.39 GB temp, 17.15–18.01 GB free of 31.33 GB.
- Storage: 171.8 GB used / 26.06 GB free of 197.86 GB; 3.42 GB selected across the five
  categories (update cache 1.53 GB, logs+Windows Temp 238.43 MB, Recycle Bin 0 B, temp 1.39 GB,
  browser caches 273.15 MB).
- Protection: "Windows Update needs attention — Defender real-time protection on · Restart
  pending", Defender definitions 1.463.414.0, browser detected as Brave.
- Apps: 60 applications · 5.93 GB in use; startup list populated (slow — Task Scheduler scan).
- Smart Boost ran once during a capture (167 working sets trimmed, 743 796 042 bytes of temp
  removed) — expected behaviour, not a fault. Since the Boost-result change, the result line
  reports the measured available-memory delta instead of a reclaimed figure.
- Log file `%LOCALAPPDATA%\FarmanullahPCManager\logs\app-20261122.log` records start/exit,
  Boost, and the Start-with-Windows toggle.

## Open Questions for Reviewer

- Is the **Wpf.Ui** dependency a hard requirement, or is the custom Fluent XAML acceptable? It is
  the only framework-level deviation.
- Should the AppUserModelID be changed to `Farmanullah.PCManager.App`, and is
  `HKCU\Software\FarmanullahPCManager\` required for any state (nothing currently needs it)?
- Are the brief's exact palette hexes mandatory, or is "warm yellow, same token structure"
  sufficient?
- Is the two-phase cleanup protocol meant to live **inside the application** (numbered table +
  typed approval + stop) or is it the agent workflow used during this session? The app currently
  implements checkbox selection only.
- Should App management list Store/MSIX packages (`Get-AppxPackage`) and route removal through
  Settings?
- Is deleting duplicates in scope? Detection exists, deletion does not.
- Should the app ship an elevated helper for Windows Temp / update cache / HKLM startup toggles,
  or keep the current "report access denied" behaviour?
- Is Dark-theme visual verification required before sign-off?
