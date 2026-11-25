# Changelog

All notable changes to Farmanullah PC Manager. Dates are ISO (`YYYY-MM-DD`). Status markers
follow the audit legend: **[DONE]**, **[PARTIAL]**, **[MISSING]**.

## [Unreleased]

### Added
- **Two-phase Cleanup Center** in Storage: a read-only scan builds a numbered table (exact paths,
  measured bytes, file/subfolder counts, age, tier, confidence, running/elevation state), and a
  typed approval gate (`proceed`, `only 1, 2`, `only 3-7`, `keep 1, 2`) produces a plan that is
  confirmed before anything is removed. Invalid or ambiguous commands return a clarification and
  delete nothing. Plans expire after 15 minutes and are discarded on rescan. A dry-run mode logs
  the plan without deleting.
- **Runtime safety guard** (`ISafetyGuard`): canonical paths, rule allow-list roots, hard deny-list
  trees and name fragments, junction/reparse refusal and whitelist precedence, called before every
  file deletion and every rule-root enumeration.
- **Whitelist service** backed by `%LOCALAPPDATA%\FarmanullahPCManager\whitelist.json`; entries
  are excluded from scans and refused by the guard.
- **Whitelist manager** in Settings: list protected paths, add an existing folder or file, remove
  the selected entry, reload from disk and open the app data folder. Trailing separators are
  normalised so the same folder cannot be added twice.
- **Cleanup audit log** at `logs\cleanup_log.txt`, one line per item with timestamp, number, rule,
  paths, bytes and `DELETED` / `SKIPPED_*` / `DRYRUN` / `FAILED`, rotated at 5 MB.
- **Changed-file approval invalidation**: before deleting a planned row, the executor measures
  it again. A byte change beyond 5% / 64 KB, or a file-count change beyond 5% / 3 files, logs
  `CHANGED_SINCE_SCAN` and leaves that row untouched; the user must scan and approve again.
- **Cleanup profiles**: six presets (Quick Clean, Developer Clean, AI Workspace Clean, Browser
  Clean, Windows Clean, Deep Review) tick rows by family. They never select Opt-in or
  running-application rows, and they do not approve or delete anything. Changing any tick after
  review discards the pending plan, so an approval always describes the current selection.
- **Cleanup history and export**: the last 50 completed/dry-run summaries are kept in
  `cleanup_history.json`, the last five are shown in Storage, and the measured report exports as
  Markdown or CSV under `reports\`. History read/write failures are logged and never block cleanup.
- **Cleanup executor** with per-drive free-space before → after, actual bytes versus estimate,
  re-measurement of each folder, and `SKIPPED_ELEVATION` / `SKIPPED_LOCKED` / `SKIPPED_PROTECTED`
  / `CHANGED_SINCE_SCAN` reporting instead of aborting.
- **Cleanup CLI**: `--scan` (deletes nothing), `--clean --ids 1,2`, `--dry-run`,
  `--restore-point` and `--auto-clean`, using the same catalog, parser and executor as the UI.
- **Elevated helper** `FarmanullahPCManager.Helper.exe`: a separate `requireAdministrator` WinExe
  copied next to the UI. It accepts one typed request per launch over a `CurrentUserOnly` named
  pipe, and only `CleanRule` for five fixed Windows targets or `CreateRestorePoint`. No path,
  executable or shell string crosses the pipe. The update-cache command stops and restarts
  `wuauserv`/`bits`; every target is re-checked for path, age and reparse points. UAC decline is
  reported, not retried.
- **Restore-point offer**: a Storage checkbox (default on for Caution/Opt-In plans) and CLI
  `--restore-point` call the helper's fixed `Checkpoint-Computer` command. Failure is stated in
  the report and cleanup may continue; dry runs never create a point.
- **Helper audit log** at `logs\helper-YYYYMMDD.log`, with no paths beyond the fixed rule ids and
  no credentials.
- **Rule catalog** covering Windows temp/logs/update/WER/shader/Prefetch/`$PatchCache$`/Recycle Bin,
  every detected Edge/Chrome/Brave profile, Firefox `cache2`, npm, NuGet, pip, Yarn, pnpm, and the
  detected caches of Antigravity, Freebuff, Cursor, VS Code and Windsurf. Omnirush is reported as
  having no supported cleanup target.
- **Duplicate recycling**: duplicate groups are now confirmed with **full SHA-256** instead of
  edge samples, and each group names the oldest copy as its keeper. **Recycle extras** asks first,
  re-hashes the keeper and every extra immediately before acting, and refuses whitelisted paths,
  reparse points, Windows/Program Files/ProgramData/app-data/package folders, vanished files and
  anything whose size or hash changed after the scan. The only delete path is the shell's
  `SHFileOperation` + `FOF_ALLOWUNDO` (Recycle Bin), and **Open Recycle Bin** sits next to the
  scan button for restoration. Service behavior is unit-tested; the shell move was verified live
  on one temporary file (no real duplicate group recycled through the UI).
- **Restoreable quarantine**: an approval-gate checkbox moves eligible non-elevated cleanup files
  to `%LOCALAPPDATA%\FarmanullahPCManager\quarantine` instead of deleting them. `manifest.json`
  records the original path, stored path, UTC time, size, rule, reason and a SHA-256 for files up
  to 64 MB. Windows/Program Files, databases, keys, cookies and login/session files are refused,
  and a failed manifest write rolls the move back. Storage lists the entries with a per-file
  Restore that refuses an occupied destination or a hash mismatch, plus Open folder and Delete
  quarantined files; Settings sets retention (3/7/30 days) and the size cap (1/2/5/10 GB),
  applied before each quarantined run. CLI: `--quarantine`. Unit tests cover the batch move,
  manifest,   restore conflict, path traversal, database/credential refusal, rollback when the
  manifest cannot be written, and purge by age and size; no real cache cleanup was run in
  quarantine mode. Duplicate recycling tests cover the re-hash, whitelist refusal, a keeper
  outside the group and a shell refusal.
- **Win32 uninstall completion**: the uninstaller is still started from the registry command
  (`MsiExec /I…` rewritten to `/X`), but the result is now based on evidence: the service waits
  up to 10 minutes for the process (or for the Uninstall entry when the shell returned no wait
  handle), then up to 1 more minute for that app's registry entry to disappear. Only then is the
  app reported removed; the Apps list refreshes and any remaining `InstallLocation` is measured
  and reported read-only, never deleted. Compiled and build-verified; no live uninstall was run.
- **Store/MSIX app list** on Apps via a read-only `Get-AppxPackage` query. Framework, resource,
  system and Microsoft PC Manager packages are hidden and refused. The per-row uninstall asks
  Yes/No first, re-queries the exact package, and runs `Remove-AppxPackage` for the current user
  only; the package list is re-read before success is reported, and failures are stated.
  Parser/filtering tested; the compiled listing was exercised on
  this machine (57 eligible packages); actual removal not exercised.
- **Test project** `app/tests/PCManager.Tests` with 73 passing tests for the approval parser,
  safety guard, elevated command contract, changed-file tolerances, history, report export,
  profile matching, whitelist persistence, Store/MSIX JSON filtering and quarantine.
- Documentation: `PROJECT_SPEC.md`, `BUILD_STATUS.md`, `CHANGELOG.md` for the build audit.
- Shared `CardList` style so the Apps lists no longer repeat the same `ItemsControl` template, and
  a single `NavigationItemTemplate` shared by both rail lists.

### Changed
- The cleanup executor's optional quarantine mode reports quarantined bytes/files separately from
  deleted ones, so a run never claims free space for files that are still on disk. Markdown/CSV
  exports gained the quarantined totals and the history keeps the same honest summary.
- Storage's five-category checkbox list was replaced by the Cleanup Center; the same safe Windows
  and browser categories are now rules in one catalog, plus AI/IDE and developer caches. Large
  files gained a 500 MB filter.
- Cleanup scans use bounded parallelism (2–4 workers) instead of one serial pass; a 16-row,
  4.64 GB scan completed in 18.5–22.8 s across runs on the test machine.
- Machine-wide cleanup rules now route through the helper instead of attempting an unelevated
  delete and reporting `SKIPPED_ELEVATION`. Their roots are shared between the rule catalog and
  the helper via `Core/Elevation/ElevatedCleanTargets.cs`, so the reviewed path and the deleted
  path cannot drift.
- View models now use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`) instead of
  hand-written backing fields.
- Settings and Storage open folders through `IToolLauncher.OpenFolder`; `DefaultBrowserService`
  opens Default apps through `IToolLauncher.OpenUri`.
- `RunningAppViewModel` reuses `ProcessInfo.CpuText/MemoryText` rather than re-formatting bytes.
- Root `README.md` rewritten to describe the shipped stack (custom Fluent XAML, no UI framework
  package) and the six screens; `app/README.md` updated for the new project layout.

### Removed
- `DeepCleanupService` and `IDeepCleanupService` (superseded by the rule catalog, scanner and
  executor), plus the unused `CleanupCategory` / `CleanupOutcome` models.
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
- **[PARTIAL]** Duplicate recycling is implemented and unit-tested; the shell move to the
  Recycle Bin was verified on one temporary file, but no real duplicate group was recycled from
  the UI and the card was not visually inspected.
- **[MISSING]** Treemap/downloads manager, the new
  AI Tools / Restore / Agents / Feedback / Health Check screens, scheduler, portable mode,
  Urdu RTL, Dark-theme screenshots, installer.
- **[PARTIAL]** Quarantine is implemented and unit-tested with temporary files; the executor's
  quarantine branch has not been run on real cache files, and its cards were not visually
  inspected.
- **[PARTIAL]** Store/MSIX listing and its confirmation-gated current-user removal are implemented;
  no package was actually removed, and the Apps Store list was not visually inspected.
- **[PARTIAL]** Win32 uninstall now waits for the uninstaller, re-checks the Uninstall registry
  entry and reports measured leftovers, but no live uninstall was exercised.
- **[MISSING]** Helper commands for HKLM startup, hosts-file blocking and network reset.
- **[PARTIAL]** The elevated helper was only smoke-tested with `RunAsInvoker` (unknown-rule
  rejection); no UAC-approved run deleted system files, stopped services or created a restore
  point. The scanner and executor share one assembly (no reflection test proves the scanner has
  no delete API); the delete path was only exercised as a dry run; the Cleanup Center, restore-
  point checkbox, profile, history, export and whitelist controls were not visually inspected;
  floating-toolbar position still does not persist; the simultaneous-run test with Microsoft PC
  Manager is still a side-by-side-install check only.

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
- Store/MSIX apps are enumerated and can be removed for the current user after confirmation;
  this removal path was not exercised during verification.
- Duplicate detection reports groups only; there is no delete action.

### Fixed
- Deep-uninstall leftover cleanup exported every registry key to the same `.reg` path, so only the last key was recoverable. Each key now gets its own backup file, and leftover cleanup stops before deleting registry keys when the backup fails.

### Added
- Shell command palette (`Ctrl+K`) with page navigation.
- Maintenance readiness and network diagnostics services with tests.
- v7 theme token aliases in both theme dictionaries.

### Added
- User-started download speed test with configurable endpoint and measured Mbps/bytes/time.
- Pop-up management monitor listing visible windows with a persistent per-app allow list.
- Reversible hosts block through the elevated helper (`# BEGIN FPM` / `# END FPM`) with a timestamped backup before every write.

### Changed
- Home content is capped at 900 DIP and centred on maximized windows, so cards no longer stretch across the whole monitor.

### Added
- Local 30-day sighting counter for the pop-up monitor ("Seen Nx in 30 days"), stored only on this PC.
- Speed-test endpoint is remembered across restarts.

### Changed
- Hosts block apply/remove transforms moved into a shared, tested `HostsFileContent` helper used by both the app and the elevated worker.

### Changed
- Home uses a two-column Bento layout above 760 DIP of content width and caps its content at 1280 DIP, instead of stretching the compact stack across maximized windows.

### Fixed
- Protection no longer clips the update banner heading or the Pop-up management heading at the 560 px window width; the banner text wraps and the section actions moved below the description.

### Added
- Home tips carousel with three built-in tips, dots and previous/next controls.
- One-Click Cleanup entry with live memory and disk status chips linking to Memory and Storage.
