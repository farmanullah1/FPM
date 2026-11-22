# MASTER PROMPT v2 — "Farmanullah PC Manager" (Windows 10/11)
### Continuation prompt: finish the half-built app, reach full Microsoft PC Manager parity, add the AI-IDE junk-cleaner

> **How to use:** Give this entire file to your coding agent (Antigravity / Gemini / any agent) together with the repository. Also give it `PROJECT_SPEC.md`, `BUILD_STATUS.md` and `CHANGELOG.md`. Those three files describe the code **as it exists today** (commit `9a56711`, 2026-11-22); this prompt describes where it must go.
>
> **Non-negotiable rules for the agent**
> 1. **Extend, do not restart.** Working code stays. Refactor only what blocks a requirement here.
> 2. Write every file in full. **No stubs, no `TODO`, no `// implement later`, no "...rest of code", no fake or sample data in shipped code.**
> 3. If something cannot be done safely or Windows blocks it, implement the safe subset, say so in the UI, and document it in the README. Never display a number the code did not measure.
> 4. After every phase: `dotnet build` with **0 warnings, 0 errors**, tests pass, and print a one-paragraph status plus the updated Feature Matrix (`[DONE] / [PARTIAL] / [MISSING]`).
> 5. Ask the owner only about decisions that are destructive or irreversible. Everything else is decided in Section 1.

---

## 0. ROLE & OBJECTIVE

You are a **Senior Windows Desktop Engineer, Windows-internals/security specialist and Fluent UI/UX designer**.

Finish **Farmanullah PC Manager**, a native Windows utility that:
1. matches **every feature of Microsoft PC Manager 3.23** (Home, Protection, Storage, Apps, AI Tools, Restore, Agents, Settings, Feedback, floating toolbar, tray, Smart Boost);
2. wears its **own warm yellow / honey identity** (same layout language as Microsoft PC Manager, golden instead of blue);
3. adds a **powerful, safe, two-phase cleanup engine** that hunts cache, temp, log and leftover junk from **Google Antigravity, Freebuff, Omnirush, other AI IDEs/agents, dev tools, browsers and Windows itself**.

The owner will **uninstall Microsoft PC Manager** when this app is complete, so this app must fully replace it. Until then both run side by side on the same laptop and **must never conflict**. Microsoft PC Manager is installed as MSIX `Microsoft.MicrosoftPCManager_3.23.3.0_x64__8wekyb3d8bbwe`.

---

## 1. STARTING STATE & BINDING DECISIONS

### 1.1 What already exists (do not rebuild)
- Solution `app/PCManager.sln`, four projects: `PCManager.Core` (`net8.0`), `PCManager.Services`, `PCManager.ViewModels`, `PCManager` (shell → `FarmanullahPCManager.exe`) (`net8.0-windows`). C# 12, nullable on. `CommunityToolkit.Mvvm` 8.3.2, `Microsoft.Extensions.DependencyInjection` 8.0.1, DI registered in `Services/DependencyInjection/ServiceCollectionExtensions.cs` plus WPF-only services in `App.xaml.cs`.
- Build passes with 0 warnings. Command: `cd app && dotnet build PCManager.sln -nodeReuse:false -m:1`.
- Done: identity layer (`Core/AppInfo.cs`), single-instance mutex, six-screen shell (Home, Protection, Storage, Apps, Toolbox, pinned Settings), PC Boost (honest measured results), Smart Boost, Defender/Update status, repair cards, 5-category deep cleanup (checkboxes), large-file scan, duplicate detection (read-only), Storage Sense toggle, process list + End Task with protected refusals, startup manager (Run/RunOnce/Startup folders/Task Scheduler with real toggles), Win32 installed-app list, floating toolbar, Toolbox tools, Settings, Light/Dark theme dictionaries, logging.
- Known gaps you must close (from the audit): Dark theme never visually verified; no Store/MSIX listing; no cleanup categories A/C/D; **no in-app two-phase approval protocol**; no runtime SafetyGuard/blocklist (safety is "by omission" today); no elevation helper; no automated tests; duplicate delete missing; floating-toolbar position not persisted; tray icon not visually verified; AI Tools / Restore / Agents / Feedback screens do not exist; no scheduler, whitelist, portable mode, localization, restore point, cleanup log file in the required format.

### 1.2 Decisions on the audit's open questions (final — do not re-ask)
| Open question | Decision |
|---|---|
| Wpf.Ui required? | **No.** Keep the hand-written Fluent XAML. Do **not** add the package. Add controls only as local styles in `Styles/Controls.xaml`. |
| AppUserModelID | **Keep** `FarmanullahPCManager.Desktop.8A4F2C1E` (already unique). Use it for toasts too. |
| Registry root | **Create and use** `HKCU\Software\FarmanullahPCManager\` for *state that must survive settings.json loss*: default-browser baseline, hosts/proxy baselines, startup-entry baseline, scheduler markers, restore-drift snapshots. Portable mode skips it. |
| Palette hexes | **The shipped palette is canonical** (Section 4.1). Do not churn it. Add any missing keys with the same naming. |
| Card radius | Keep `Card=10`, `ActionCard=8`. Document, do not change. |
| Two-phase cleanup | **Must live inside the application** (numbered table + typed command + stop gate) *and* via CLI. Checkbox UI remains as the GUI path to the same engine. |
| Store/MSIX apps | **List them** via `Get-AppxPackage` (read-only), uninstall via `Remove-AppxPackage` for the current user with confirmation; framework/system packages hidden and refused. |
| Duplicate deletion | **In scope**, Recycle-Bin only, never delete the last copy, full-file SHA-256 confirmation before offering delete. |
| Elevated helper | **Ship it** (Section 8). Main app stays `asInvoker`. |
| Dark theme verification | **Required**: screenshots of all screens in Dark before sign-off. |
| Namespaces/projects | **Keep existing** `PCManager.*` names. Do not rename to `Farmanullah.PCManager.*`. Identity strings (exe, product, mutex, data folder, Run value) already carry the Farmanullah name. |
| Version | Keep `3.23.3` UI string; bump the semantic file version per phase (`0.2.0` …). |

---

## 2. CRITICAL CONSTRAINT — ZERO CONFLICT WITH MICROSOFT PC MANAGER

| Area | Value (unique to this app) | Rule |
|---|---|---|
| Executable | `FarmanullahPCManager.exe`; helper `FarmanullahPCManager.Helper.exe` | Never `MSPCManager*`, `PCManager.exe`, `PCMgr*` |
| Single-instance mutex | `Global\FarmanullahPCManager_SingleInstance_8A4F2C1E` | exists |
| AUMID | `FarmanullahPCManager.Desktop.8A4F2C1E` | exists |
| Data folder | `%LOCALAPPDATA%\FarmanullahPCManager\` (`settings.json`, `logs\`, `cleanup\`, `feedback\`, `whitelist.json`, `backups\`) | never under any `Microsoft` folder |
| Registry | `HKCU\Software\FarmanullahPCManager\`; startup Run value **`FarmanullahPCManager`** only | never touch another app's Run value; never write `HKCU\Software\Microsoft\PCManager\*` |
| Named pipe (helper IPC) | `\\.\pipe\FarmanullahPCManager.Helper.<UserSID>` ACL-restricted to current user SID | |
| URI scheme | `farmanullah-pcmanager:` (register only if needed, in HKCU, removable) | **never** `mspcmanager:`, `pcmanager:`, `ms-pcmanager:` (audit confirmed none present) |
| Global hotkeys | Circle to Act `Ctrl+Alt+Shift+Y`; Boost `Ctrl+Alt+Shift+B`; Quick Scan `Ctrl+Alt+Shift+S` | **Never `Ctrl+Shift+A`** (Microsoft's default). `RegisterHotKey` failure → UI says "hotkey in use", never crashes |
| Scheduled tasks | Task Scheduler folder `\Farmanullah\` (e.g. `\Farmanullah\AutoClean`) | create/remove only inside it |
| Temp | `%TEMP%\FarmanullahPCManager\` | |
| Tray icon | fixed GUID generated once and hard-coded | |
| Logs | `app-YYYYMMDD.log` + `cleanup_log.txt` inside own `logs\` | |

**Code-enforced coexistence safeguards**
1. **Detection (read-only):** at startup detect package `Microsoft.MicrosoftPCManager` (`Get-AppxPackage`) and processes `MSPCManager*`, `MSPCManagerService`, `MPCMDRUN`. Show an info card on Home: *"Microsoft PC Manager is installed. Both apps can run together; remove it from Windows Settings when you are ready."*
2. **Protected list** (hard-coded, already partly in `AppInfo.ProtectedProcessNames`): extend with `Microsoft.MicrosoftPCManager*` paths. Process Manager can never end them, Startup Manager never toggles them, Cleanup never deletes inside them, Deep Uninstall refuses them (shows "Remove from Windows Settings" instead). Boost skips them.
3. **Migration helper** (Settings): button opens `ms-settings:appsfeatures`, explains the removal steps. Never uninstalls Microsoft PC Manager automatically.
4. **Uninstaller / "Reset app data"** removes only Farmanullah keys, folders, tasks.
5. **Simultaneous-run test** (currently `[PARTIAL]`): add a documented manual + scripted check that both apps run together for 10 minutes (Boost in each, tray in each, startup toggles in each) with no cross-effects; record result in `BUILD_STATUS.md`.

---

## 3. TECH STACK & ARCHITECTURE (as built — extend)

- .NET 8, C# 12, WPF, MVVM with `CommunityToolkit.Mvvm` source generators, DI as above. Win32 P/Invoke in `Services/Interop/NativeMethods.cs`; WMI via `System.Management`; `System.Drawing.Common` for capture; `UseWindowsForms` only for the tray `NotifyIcon`.
- **Add packages only as needed** (pin versions): `Serilog` + `Serilog.Sinks.File` *only if* it replaces `AppLogger` cleanly (otherwise keep `AppLogger`); `Microsoft.Toolkit.Uwp.Notifications` (toasts); `TaskScheduler` (Task Scheduler wrapper, or keep PowerShell calls with `ArgumentList`); test stack `xunit`, `Moq`, `FluentAssertions`; for OCR target `net8.0-windows10.0.19041.0` on the shell project (needed for `Windows.Media.Ocr`).
- **Dependency chain stays one-way:** `Core` ← `Services` ← `ViewModels` ← `PCManager`. New projects: `PCManager.Helper` (elevated helper, console/windowless exe, `requireAdministrator` manifest) and `PCManager.Tests`. The helper references `Core` only.
- **Privileges:** main exe `asInvoker`. Admin-only work (see Section 8) goes through the helper launched with `runas` on demand.
- **Performance targets:** < 50 MB RAM idle in tray; all system queries async with `CancellationToken`; UI 60 fps; `DispatcherTimer`s paused while the window is hidden; file walkers use channels/`Parallel` with caps.
- **Quality bar:** nullable warnings = errors in `Core`, `Services`, `ViewModels`; analyzers on; no `catch {}` without a logged reason.
- **Publish:** `dotnet publish app/src/PCManager/PCManager.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish`.

### Required service interfaces (add what is missing, keep names consistent with existing code)
Existing: `IPcBoostService`, `IMemoryOptimizer`, `IProcessManager`, `IProcessService`, `IStartupManager`, `IAppStartupService`, `IDeepCleanupService`, `IStorageAnalyzer`, `IStorageService`, `IDuplicateFinderService`, `IInstalledAppService`, `ISystemRepairService`, `IDefaultBrowserService`, `IDefenderStatusService`, `IWindowsUpdateStatusService`, `IProtectionService`, `ITempFileCleaner`, `IBrowserCacheCleaner`, `IRecycleBinService`, `IStorageSenseService`, `IToolboxService`, `IToolLauncher`, `ISettingsService`, `IAppLogger`, `IThemeService`, `IFloatingToolbarHost`, `INavigationService`.
**New:** `ISafetyGuard`, `IScanner`/`ICleaner` (split, see §6), `ICleanupCatalog` (rule registry), `ICleanupCommandParser`, `ICleanupReportService`, `IRestorePointService`, `ISchedulerService`, `IWhitelistService`, `IElevationService` (+ helper IPC client), `IAppxService`, `IBaselineService` (registry baselines), `IRestoreDriftService`, `IPopupGuardService`, `IHostsBlocklistService`, `INetworkService`, `ISpeedTestService`, `IOcrService`, `IScreenCaptureService`, `IHotkeyService`, `ILocalizationService`, `IUsageTrackerService`, `ISandboxService`, `IFeedbackService`, `IAiToolDiscoveryService`, `IPortableModeService`, `ICoexistenceService`.

---

## 4. VISUAL IDENTITY — YELLOW FLUENT

Same compact-utility layout as Microsoft PC Manager; **only the identity colour changes from blue to warm honey**. Brush keys are **already implemented** in `Styles/Themes/Light.xaml` / `Dark.xaml` and swapped at index 0 of the merged dictionaries by `ThemeService`. Every colour is consumed via `DynamicResource`; **no hard-coded hex in views**.

### 4.1 Canonical tokens (shipped values)
| Key | Light | Dark |
|---|---|---|
| `App.Background` | `#F7F3E8` | `#1F1C16` |
| `App.Rail` | `#F1EAD9` | `#191712` |
| `App.Card` | `#FFFFFF` | `#2B2721` |
| `App.CardHover` | `#FBF6EA` | `#35302A` |
| `App.Border` | `#E8E0CC` | `#443C2E` |
| `App.Text` | `#2A241A` | `#FFF8E7` |
| `App.Subtle` | `#7A6E55` | `#C9BFA6` |
| `App.Accent` (honey gold) | `#C98A00` | `#F2C14E` |
| `App.AccentSoft` | `#FBF0D2` | `#3A3020` |
| `App.AccentText` | `#FFFFFF` | `#241C0C` |
| `App.Success` / `Warning` / `Critical` | `#4F7A28` / `#B26A00` / `#B3261E` | `#9BC46A` / `#F2C14E` / `#FF9A8F` |
| `App.Selection` | `#F5E8C6` | `#3E3524` |
| `App.Track` | `#EFE7D3` | `#3A342A` |

**Additions required:** `App.AccentHover`, `App.AccentPressed`, `App.Link` (metric/link text), `App.PreviewBadgeFill/Text`, and the five storage-bar segment brushes: User Files `#E04F5F`, Application Files `#E67E22`, System Files `#C9A227`, Recycle Bin `#3E9B4F`, Other `#6B6350` (brighter dark variants). **Contrast gate:** body text ≥ 4.5:1, accent-on-canvas and white-on-`App.Accent` verified by a script that prints the ratios; fix any failing pair (Light `#FFFFFF` on `#C98A00` must be checked — switch `App.AccentText` to charcoal if it fails).

### 4.2 Shell (exists — verify and complete)
- Window 1280×820 (min 1020×660) as built. Custom 44 px title bar with `WindowChrome`: optional **back arrow `←` on sub-pages**, **Farmanullah 4-petal honey logo (vector `Path`, never a Microsoft-style blue mark)**, title "Farmanullah PC Manager", `v3.23` badge, Minimise / Maximise / Close. Close → minimise to tray (setting).
- Left rail 236 px (as built) with icon + title + subtitle, **3 px honey indicator pill** on the active item, filled gold icon. Items top-to-bottom: **Home, Protection, Storage, Apps, AI Tools, Restore, Agents**; pinned bottom: **Settings, Feedback**. (The shipped "Toolbox" page becomes **AI Tools**; keep its six tools and extend, see §5.5.)
- Cards, buttons, toggle, slider, pills, progress: keep tokens in `Styles/Controls.xaml` (`Card` r10/pad18, `RowCard`, `ActionCard` r8, `PrimaryButton` r6, `PillButton` r14, `ToggleSwitch` 40×20, `ThinProgress` 6 px).
- Typography `Segoe UI Variable` (fallback Segoe UI). Page title 22 Bold, card header 15 SemiBold, secondary 12, metrics/links 12 SemiBold in `App.Link`.
- Animations 150–220 ms slide+fade between pages, gold progress ring, Boost button pulse; **respect Windows "reduce animations"** (`SystemParameters.ClientAreaAnimation`).
- Theme setting: `System / Light Yellow / Dark Yellow`, live switch (already works), follows Windows when `System`.
- Assets to produce: multi-size `.ico` (16–256), tray icon (light/dark variants), installer wizard images — all in the yellow palette.

---

## 5. SCREENS — FULL PARITY + UPGRADES

All figures come from **real system data**. No marketing/ads, no Microsoft 365 promos, no remote content.

### 5.1 Home *(exists — extend)*
- **Tips banner card** (soft gold gradient): lightbulb badge, rotating **local** tips generated from real findings (e.g. "Clear 3.2 GB of AI-IDE cache", "5 startup apps slow your boot"), link "More tips >" → Health Check. Plus the **Coexistence info card** (§2.1).
- **PC Boost card** (done): memory %, temp size, **Boost**, **Smart Boost** switch, honest result line (working sets trimmed, available-memory before → after with signed delta, temp bytes actually deleted). Keep. Add a "Set Smart boost" link → Settings.
- **2×2 quick cards:** Health check (last check date), Process (running app count), Deep cleanup (`Local Disk (C:) used/total`), Startup (enabled count + measured/estimated boot impact).
- **Health Check page (new, from the "Health check" card):** async multi-category scan with checkboxes, sizes/counts and **Proceed**. Categories: temporary files, browser caches, Recycle Bin, AI-IDE/agent caches, high-impact startup apps, pending Windows updates, Defender status, low disk space, memory pressure. **Live dashboard:** CPU %, RAM used/total, per-disk usage and I/O, GPU % (performance counters/WMI where available), temperatures when exposed, battery health (`powercfg /batteryreport` parsing or WMI), uptime, top-5 resource-hungry apps, **app-usage trend** from the local usage tracker. Overall **Health Score 0–100** with listed deductions (each deduction states its cause and size).
- **Circle to Act card** (new): sparkle icon, "OCR, image translate, smart copy", outlined **Capture** button → §5.5.

### 5.2 Protection *(exists — extend)*
Done: status banner, Defender/Update detail, four repair cards. Add / fix:
1. **Virus & threat protection** — real **Scan** button: `Start-MpScan -ScanType QuickScan` (fallback `MpCmdRun.exe -Scan -ScanType 1`), progress + result, link `windowsdefender:`.
2. **Windows Update** — last-updated timestamp + Available/Up to date via WUA COM (`Microsoft.Update.Session`, `IsInstalled=0`), chevron opens `ms-settings:windowsupdate`. Keep the honest limitation (no installing).
3. **Default-browser guard** — baseline stored in `HKCU\Software\FarmanullahPCManager\Baselines`, compared at startup and every 15 min; a change raises a toast and a Restore-screen item. **Never forge `UserChoice` hashes**; guide the user to `ms-settings:defaultapps`.
4. **Taskbar repair** (done: Explorer restart) — add icon-cache (`iconcache_*.db`) and thumbnail-cache clearing with a confirmation dialog.
5. **Restore default apps** — `ms-settings:defaultapps` with explanation.
6. **Pop-up management** — honest, layered: (a) heuristic monitor flags unexpected top-level windows from non-whitelisted background processes launched without user interaction; per-app **Allow**; counter "Blocked N pop-up(s) in the last 30 days" (local); (b) optional **hosts-file ad-domain blocklist** toggle through the helper: back up hosts first, write only between `# BEGIN Farmanullah` … `# END Farmanullah`, fully reversible, user can view the list; (c) UI text states the limit ("a browser extension blocks in-page ads better").
7. **Network check** — adapter info, gateway/DNS ping, internet reachability, **speed test** (configurable endpoint, default a reputable public file, no telemetry), **Troubleshoot** (flush DNS, release/renew, Winsock reset — each confirmed, via helper where admin is needed).

### 5.3 Storage *(exists — extend heavily)*
- **Drive card** (done) + **multi-segment bar** (User / Application / System / Recycle Bin / Other, dot legend, drive dropdown for all fixed drives) + **Disk analysis >** → async cancellable **treemap/folder-size explorer** with drill-down and "Open in Explorer".
- **Deep cleanup** → opens the **Cleanup Engine page** (§6) — this replaces the current five-checkbox list but keeps its categories.
- **Downloaded files** — Downloads manager: sort by size/date, filter by type, **Old Downloads (> 90 days)** shortcut, Recycle-Bin action.
- **Large files** (exists): thresholds 10/50/100 MB and 1 GB → add **500 MB** chip and a category filter (Videos, Archives, Installers/ISO, Documents, Disk images, All); per-row **Open folder** (`explorer.exe /select,"path"`) and **Move to Recycle Bin**. Keep the 200-row cap.
- **Duplicate files** (detection exists): add scope dropdown (All disks / Downloads / Documents / Desktop / Custom), full-file SHA-256 confirmation before any delete, helpers "keep newest / keep oldest / keep one in chosen folder", Recycle-Bin delete, **never allow all copies of a group to be selected**.
- **Storage Sense** (done).

### 5.4 Apps *(exists — extend)*
- Hub cards: **Process management (N running)**, **Deep uninstall**, **Microsoft Store** (`ms-windows-store:`), **Startup apps**.
- **Process management** (done): protected refusals. Add collapsible "End these apps if needed" with extracted 28×28 icons, graceful `CloseMainWindow()` first then `Kill()` after a timeout, confirmation for apps with unsaved-work risk, search box, sort by RAM/CPU, "Show background processes" toggle. Protected set (cannot be ended): `System, Idle, Registry, smss, csrss, wininit, winlogon, services, lsass, svchost, dwm, explorer, fontdrvhost, MsMpEng, SecurityHealthService, audiodg`, all `Farmanullah*`, all `MSPCManager*`.
- **Startup manager** (done): add publisher (Authenticode signer), impact (Low/Med/High from measured data else heuristic, labelled which), HKLM toggles via helper, a baseline so new entries show up in Restore.
- **Deep uninstall:** Win32 (exists) **+ Store/MSIX** (§1.2). After uninstall launch **Leftover Scan** (read-only report) over `%APPDATA%`, `%LOCALAPPDATA%`, `%PROGRAMDATA%`, Program Files remnants, orphaned `HKCU/HKLM\Software\<Publisher>` keys; the user ticks leftovers; **every registry deletion is first exported to `.reg` in `backups\`**. Wait for the uninstaller to exit and refresh the list (currently missing).

### 5.5 AI Tools *(evolves the shipped Toolbox)*
1. **Settings card (collapsible):** Circle-to-Act hotkey input (default `Ctrl+Alt+Shift+Y`, clearable, conflict detection), Text Selection Toolbar toggle (default Off), **Show toolbar on the desktop** toggle (floating toolbar — exists).
2. **3×4 tools grid:** Circle to Act, Translate, OCR text & table, Screenshot folder, Captions, Notepad, Quick links, Translator, Currency converter, Weather, Image search, Calendar. Keep the existing working ones (screenshot, screen recorder via Game Bar/`psr.exe`, calculator, notepad, DNS flush, quick links).
   - **Circle to Act / OCR:** DPI-aware multi-monitor region-select overlay → `Windows.Media.Ocr` (offline) → panel with Copy, Translate, Search, **Copy table as CSV/TSV**.
   - **Translate / Translator / Currency / Weather / Image search:** user-configurable providers with open-in-browser fallbacks; **no hard-coded API keys**; keys stored with DPAPI; free public endpoints with caching; graceful offline state.
   - **Captions:** Live Captions (`ms-settings:easeofaccess-captions`/`LiveCaptions.exe` if present). **Screenshot folder:** `%USERPROFILE%\Pictures\Screenshots`. **Calendar:** `outlookcal:` fallback `ms-clock:`.
3. **Floating toolbar** (done): add position persistence per monitor, edge snapping, `WS_EX_TOOLWINDOW` (hidden from Alt-Tab), RAM % colour (green < 70, amber 70–85, red > 85), collapsible drawer (Screenshot, Notepad, Calculator, Resource Monitor, Lock).
4. **Quick launchers:** Snipping Tool (`ms-screenclip:`), Sound Recorder, Resource Monitor (`resmon.exe`), Task Manager, Device Manager, Disk Management, Lock (`LockWorkStation`).
   > Note: `calc.exe` is a stub on machines without the Calculator package (seen on the dev machine). Detect that and fall back to `ms-calculator:`; if absent say so.

### 5.6 Restore *(new)*
"These default settings were changed" — drift list from `IRestoreDriftService`, comparing stored baselines to current: **default browser**, default PDF/image handlers, taskbar/Start pins, **hosts file**, **proxy settings**, **new startup entries**. Each item expands with description and a **Restore** action: open the right Settings page, or restore **our own backup** where safe (proxy, hosts). Subtitle "Recommend restoring N items"; "Manual reset" section. A user-triggered "Accept current state as new baseline" per item.

### 5.7 Agents *(new, Preview badge)*
**Agent Management** → **Agent Security Sandbox** card with isometric two-cube light-gold graphic (vector). Real implementation via **Windows Sandbox**: detect the optional feature; generate `.wsb` in `%TEMP%\FarmanullahPCManager\` with user-chosen mapped folders (read-only default), networking on/off, clipboard on/off, vGPU off; launch `WindowsSandbox.exe <file>.wsb`. If missing, explain and offer `Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM` via the helper after confirmation (needs reboot — say so). Also list **detected AI agents/IDEs** (from `IAiToolDiscoveryService`) with "Run in sandbox" where applicable.

### 5.8 Settings *(exists — extend)*
- **Performance:** Smart Boost + threshold (done), temp-size trigger (≥ 1 GB default), cooldown, **Application usage duration** (local foreground-time tracking, stored locally, deletable, feeds Health Check).
- **General:** Start with Windows (own Run value — done), Appearance (done), **Language** (English, **اردو Urdu with full RTL**, Hindi, Arabic; `.resx` via `ILocalizationService`; Urdu font stack `Noto Nastaliq Urdu` → `Segoe UI`), minimise-to-tray on close, tray quick-boost, notifications.
- **Auto-Clean schedule:** Off/Daily/Weekly/Monthly, time, "scan only" vs "scan + clean Safe tier", only when idle / on AC; implemented as Task Scheduler tasks in `\Farmanullah\`; the task launches `FarmanullahPCManager.exe --auto-clean`.
- **Whitelist Manager:** add/remove folders and file patterns **never scanned or deleted**; import/export JSON; the engine consults it before every operation (wins over every rule).
- **Dry-run** toggle (also `--dry-run`).
- **Updates:** manual check / notify against GitHub Releases (`github.com/farmanullah1/Farmanullah-PC-Manager`), no telemetry, offline-safe.
- **Portable mode:** `portable.flag` next to the exe or `--portable` → all data under `.\Data\`, **no registry writes, no Run value, no scheduled tasks** (controls shown disabled with a tooltip).
- **Migration helper** (§2.3). **About** block (done) plus registry/state paths now that a root key exists.
- **Data & privacy** text: "No telemetry. Nothing leaves this PC except the optional update check and a speed test you start."

### 5.9 Feedback *(new)*
Local-first form: **Problem type\*** → dependent **Detail\*** → **Description\*** (multiline) → **Upload screenshot** (.png/.jpg) → **Submit** (disabled until valid). Submit writes a reviewable bundle (form + app log + anonymous system summary) to `%LOCALAPPDATA%\FarmanullahPCManager\feedback\` and offers **Open GitHub issue** and **Email report** (`mailto:`, prefilled). No Microsoft links, no automatic upload.

### 5.10 System tray *(code exists; verification missing)*
Tooltip `RAM: XX% · Free C: YY GB`; double-click restores; menu: **Open, Quick Boost, Quick Scan, Show/Hide Floating Toolbar, Pause auto-clean, Settings, Exit**. Fixed-GUID `NotifyIcon`. **Verify it appears in the notification area** (the audit could not) and record how.

---

## 6. ADVANCED CLEANUP ENGINE — TWO PHASES (in-app, CLI and scheduler)

Engine lives in `PCManager.Services` (rules/scanners/cleaners) with pure logic in `PCManager.Core`. It is used by Boost (Safe tier only), Health Check, Deep Cleanup, Scheduler and CLI (`FarmanullahPCManager.exe --scan`, `--clean --ids 1,2`, `--dry-run`, `--auto-clean`).

### 6.0 Architecture
- **Rule catalog** (`ICleanupCatalog`): each rule = `Id`, `Category` (A–I), `Name`, `Roots` (allow-list), `IncludePatterns`, `ExcludePatterns`, `MinAge`, `Tier` (Safe / Caution / OptIn), `RequiresElevation`, `RequiresProcessClosed` (e.g. a browser), `DeleteMethod` (Permanent / RecycleBin / OfficialTool), `Description`, `SafetyNote`. Rules are C# classes **and** loadable from `cleanup-rules.json` so the owner can add one without recompiling (document the format in README).
- **Hard split of read and write:** `IScanner` (read-only; has no reference to any delete API — enforce with separate assemblies/namespaces and a unit test that reflects over the scanner assembly to prove no `File.Delete`/`Directory.Delete`/`SHFileOperation`/`SHEmptyRecycleBin` use) and `ICleaner` (requires an approved plan object produced only by the parser).
- **Discovery rule for A (AI IDEs):** do not depend on a fixed path list. Enumerate children of `%APPDATA%`, `%LOCALAPPDATA%`, `%LOCALAPPDATA%\Programs`, `%USERPROFILE%` (and dot-folders) whose names match a known-app list case-insensitively with variants (`Antigravity`, `antigravity`, `Google\Antigravity`, `.antigravity`, `Freebuff`, `.freebuff`, `Omnirush`, `.omnirush`, `Cursor`, `.cursor`, `Code`, `Code - Insiders`, `VSCodium`, `Windsurf`, `Zed`, `JetBrains`, Claude/ChatGPT/Gemini desktop, `Ollama`, `LM Studio`) **plus** a generic Electron/Chromium detector (folder containing `Local State`/`GPUCache`/`Code Cache`). Inside each discovered app root, scan **only** the junk subfolders listed in §6.1-A.

### 6.1 PHASE 1 — SCAN & REPORT ONLY (deletes nothing)
Show a **numbered table**: `#`, `App / Category`, `Exact Folder Path`, `Size (MB / GB)`, `Description & Safety Impact`, plus a **tier badge** (🟢 Safe, 🟡 Caution, 🔴 Opt-in). Rows have a checkbox (Safe ticked; Caution/Opt-in unticked) and an "Open folder" link. **Total Recoverable Space** at the bottom. `Export report` → `.md` / `.csv`. Everything is measured with real enumeration; items older than a rule's `MinAge` only.

**A. AI IDEs & agents** — junk subfolders only: `Cache`, `Code Cache`, `CachedData`, `CachedExtensionVSIXs`, `GPUCache`, `DawnGraphiteCache`, `DawnWebGPUCache`, `GrShaderCache`, `ShaderCache`, `Crashpad` (reports/pending only), `logs`, `Service Worker\CacheStorage`, `Network\` temp files (**not** cookies), `tmp`, and **old update installers** (`*-updater`, `pending`, `inno_updater`, `*.exe`/`*.zip` inside `updater`/`Update` folders older than 7 days). Currently known on the dev machine: Cursor `CachedData`, VS Code `Code Cache`/`logs`.
**NEVER touch (hard deny-list, checked even if a rule matches):** `settings.json`, `keybindings.json`, `User\` (settings/snippets/History), `globalStorage`, `workspaceStorage`, `Local Storage`, `IndexedDB`, `Session Storage`, `Cookies`, `Login Data`, `Preferences`, `Local State`, any name containing `token`, `auth`, `credential`, `secret`, `apikey`, `.env`, `*.sqlite`/`*.db`/`*.vscdb` chat/project databases, `extensions\`, MCP configs, model weights (Ollama/LM Studio models), SSH keys, project folders.

**B. Windows temp & caches** — `%TEMP%` items older than 24 h (existing rule uses 48 h — make it a setting, default 24 h for Phase 1 rows, 48 h for Boost), `C:\Windows\Temp`, `C:\Windows\MEMORY.DMP`, `Minidump`, `LiveKernelReports`, WER (`%ProgramData%\Microsoft\Windows\WER\ReportArchive|ReportQueue`, `%LOCALAPPDATA%\Microsoft\Windows\WER`), shader caches (`%LOCALAPPDATA%\D3DSCache`, `NVIDIA\DXCache|GLCache`, `AMD\DxCache`), thumbnail cache `thumbcache_*.db`, Windows Update download cache (`C:\Windows\SoftwareDistribution\Download` — helper stops/restarts `wuauserv`, `bits`), Recycle Bin (shell API). *(Existing five categories fold into this.)*

**C. Developer & browser caches** — npm (`%LOCALAPPDATA%\npm-cache`, measured 1.94 GB on the dev machine), yarn, pnpm store (🟡), pip (`%LOCALAPPDATA%\pip\Cache`), NuGet (`v3-cache`; `.nuget\packages` 🟡), Gradle/Maven (🟡), Cargo registry cache (🟡), Go build cache, **Docker build cache only if Docker present** (`docker builder prune` — never images/containers/volumes), Visual Studio `ComponentModelCache`. **Browsers** (Chrome/Edge/Brave/Opera/Vivaldi/Firefox): `Cache`, `Code Cache`, `GPUCache`, `Service Worker\CacheStorage`, Firefox `cache2` **only**; extend beyond `Default` to every profile folder (`Profile *`). **NEVER** cookies, passwords, history, bookmarks, autofill, extensions, sessions. If the browser is running: skip the row and say why, or "close browser first".

**D. Large files** > 500 MB (setting) — list only, user decides.
**E. Duplicates** — Downloads/Documents/Desktop (+ custom): size → partial hash (first/last 64 KB) → **full SHA-256**; skip < 1 MB, system/hidden/reparse points; never auto-select all copies.
**F. Old Downloads** > 90 days (setting), grouped by type; default action Recycle Bin.
**G. Windows.old & Delivery Optimization** — Windows.old size/age (🔴, via official `cleanmgr /sagerun` profile or DISM, **never raw delete**, warn it removes rollback); Delivery Optimization cache (`Delete-DeliveryOptimizationCache` / its cache folder).
**H. Prefetch & font cache** — `C:\Windows\Prefetch` (🟡, rebuilt by Windows, little benefit); font cache `…\FontCache` (🔴, stop/start `FontCache` service via helper).
**I. Extras** — installer leftovers in temp, `Package Cache` orphans (🟡), `Installer\$PatchCache$` (🔴, **report only**; measured 529 MB), Teams/Slack/Discord/Spotify/Zoom cache folders only, Defender scan-history logs (🟡), `C:\Windows\Logs\CBS` old logs, `.log/.tmp/.old/.bak` in temp, empty folders and broken shortcuts (**report only**).

### 6.2 STOP & APPROVAL GATE (in-app and CLI)
After the report the engine **stops**. A command box (and matching buttons) accepts exactly:
- `proceed` — delete **all listed** items.
- `only 1, 2` — delete only #1 and #2.
- `keep 1, 2` — skip #1 and #2, delete the rest.
- Anything else (e.g. `remove 1 and 2`) → **no deletion**; inline message explaining the three valid forms.

`ICleanupCommandParser`: case-insensitive, tolerant of extra spaces and commas, **ranges** (`only 3-7`), rejects out-of-range or duplicate-conflicting numbers, returns a typed `ApprovedPlan` or a `Clarification`. Before executing, echo **"You are about to delete 7 items, 4.2 GB"**; typing `proceed` is the confirmation; checkbox mode needs a final **Confirm** click. Opt-in (🔴) rows are never included by `proceed` unless explicitly ticked.

### 6.3 PHASE 2 — EXECUTE APPROVED & VERIFY
1. **Offer a Restore Point** (default ON when any Caution/Opt-in/system item is selected): `Checkpoint-Computer -RestorePointType MODIFY_SETTINGS` via helper; handle the 24-hour limit and disabled System Protection gracefully (explain, let the user continue).
2. Delete **only approved numbered items**, safe order, **SafetyGuard before every deletion**.
3. **Skip locked / in-use files** (`IOException`, `UnauthorizedAccessException`, sharing violations) without aborting; count as `SKIPPED_LOCKED`. Optional "retry on reboot" via `MoveFileEx(MOVEFILE_DELAY_UNTIL_REBOOT)` for temp-location items only.
4. **Log every action** to `cleanup_log.txt` (append-only, rotated at 5 MB): timestamp, item #, full path, size, result `DELETED | SKIPPED_LOCKED | SKIPPED_PROTECTED | SKIPPED_ELEVATION | FAILED:<reason> | DRYRUN`. **View log** button.
5. **Final report:** cleaned vs skipped, total freed, **free space before → after per drive**, duration, skipped items with reasons, **Export report**.
6. **Verification pass:** re-measure cleaned folders and show *actual* freed space, not the estimate.

---

## 7. SAFETY ENGINE (most important component — currently absent as a runtime guard)

`ISafetyGuard` is called **before every deletion**, in the app and in the helper:
1. **Canonicalise** (`Path.GetFullPath`, resolve junctions/symlinks); **never follow reparse points** out of an allowed root; reject `..` traversal.
2. **Allow-list roots per rule**; anything outside → `SKIPPED_PROTECTED`.
3. **Hard deny-list:** drive roots, `C:\Windows` (except named subfolders), `Program Files*`, `C:\Users\<user>` itself, `Documents/Pictures/Videos/Desktop` roots, `.ssh`, `.aws`, `.gitconfig`, `.git`, credential/token/key/db patterns (§6.1-A), all `Farmanullah*` and `Microsoft.MicrosoftPCManager*` / `MSPCManager*` folders, `%LOCALAPPDATA%\Microsoft\*` except the named WER/Explorer cache targets, anything under `HKCU\Software\Microsoft\PCManager\*`.
4. **Whitelist Manager entries always win.**
5. **Dry-run** (setting + `--dry-run`): log what *would* be deleted.
6. **Delete method:** user files (large/duplicate/old downloads) → **Recycle Bin**; cache/temp junk → permanent delete, labelled as such.
7. Per-file failures are logged and skipped, never retried blindly; failed deletes never throw to the UI.

**Required unit tests** (new `PCManager.Tests`): deny-list; `..\` traversal; junction escape; locked-file skip; whitelist precedence; scanner-has-no-delete (reflection test); command parser (`proceed`, `only 1, 2`, `only 3-7`, `keep 1, 2`, ambiguous `remove 1 and 2`, out-of-range, empty); restore-point fallback; `StartupApproved` byte toggling; `ByteFormatter`; duplicate grouping; cleanup selection maths; **fixture-folder tests** proving A-category scans touch only junk subfolders and leave settings/tokens/DBs/extensions intact.

---

## 8. ELEVATED HELPER

`FarmanullahPCManager.Helper.exe` (`requireAdministrator`), launched on demand with `runas`, talks over the ACL-restricted pipe in §2. It accepts **only a closed set of typed commands**: `CleanWindowsTemp`, `CleanUpdateCache` (stop/start services), `CleanPrefetch`, `CleanFontCache`, `CreateRestorePoint`, `SetStartupHklm`, `WriteHostsBlock`/`RemoveHostsBlock`, `NetworkReset(Dns|Winsock|Renew)`, `EnableSandboxFeature`, `RunCleanmgrProfile`, `DismCleanup`. It **re-validates every path through SafetyGuard**, takes no arbitrary command or path strings from the client beyond rule IDs and approved item IDs, uses `ProcessStartInfo.ArgumentList` (no shell string concatenation), and shuts down after an idle timeout. If the user declines UAC the item is reported `SKIPPED_ELEVATION` with a clear reason, as today.

---

## 9. LOCALISATION, ACCESSIBILITY, RESILIENCE
- English default, **Urdu (RTL)**, Hindi, Arabic via `.resx`; every string, tooltip, error and notification externalised; culture-aware dates/numbers; language change prompts for restart where hot-swap is impossible.
- Full keyboard navigation, `AutomationProperties.Name` on every control, high-contrast support, status announcements for screen readers, focus visuals ≥ 3:1.
- Global exception handler + crash log; never crash on a locked file or permission error; degrade gracefully when WMI/WUA are unavailable (already the pattern — keep).
- Startup performance: the audit noted the **Task Scheduler scan is slow** — cache results, load lazily, show a progress ring.

---

## 10. INSTALLER, PORTABLE & BUILD
- **Installer:** Inno Setup `installer/FarmanullahPCManager.iss`, new fixed `AppId` GUID, `{autopf}\Farmanullah\PCManager` (per-user fallback `{localappdata}\Programs\Farmanullah\PCManager`), Start-menu shortcut, optional startup entry and scheduled task, uninstall removes **only** Farmanullah keys/folders/tasks, yellow wizard images.
- **Portable:** zip of the publish output + `portable.flag`.
- **README.md** (rewrite sections that changed): overview, screenshots (Light **and Dark**), feature list, exact build/run/test/publish commands, portable mode, safety model, **how to add a cleanup rule** (`cleanup-rules.json` format), coexistence note, troubleshooting (including the transient `MSB3026` retry with the IDE language server and `-m:1`), license.
- **Exact commands to document:**
  ```
  cd app
  dotnet restore PCManager.sln
  dotnet build PCManager.sln -nodeReuse:false -m:1
  dotnet test PCManager.sln
  dotnet run --project src/PCManager/PCManager.csproj
  dotnet publish src/PCManager/PCManager.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ../publish
  iscc ..\installer\FarmanullahPCManager.iss
  ```
- Keep the three audit files **current**: update `PROJECT_SPEC.md`, `BUILD_STATUS.md`, `CHANGELOG.md` at the end of every phase (same legend, same honesty: describe the code as it is).

---

## 11. EXECUTION PLAN (follow in order; no permission needed between phases)

**Phase 0 — Re-baseline (short).** Read the three audit files and the repo. Confirm the Feature Matrix still matches the code; fix any discrepancy; resolve nothing from §1.2 again. Commit.
**Phase 1 — Safety foundation:** `ISafetyGuard`, deny-list, allow-list roots, whitelist service + JSON, dry-run, `cleanup_log.txt`, `IScanner`/`ICleaner` split, `PCManager.Tests` project with the §7 tests that can already run. Registry root + `IBaselineService`.
**Phase 2 — Elevation:** helper project, pipe, typed commands, `IElevationService`; wire Windows Temp, update cache, HKLM startup toggles, restore point.
**Phase 3 — Cleanup engine:** rule catalog (A–I), AI-tool discovery, scanners, numbered report UI, parser + approval gate (GUI + CLI), executor, restore point, verification pass, final report/export. Replace the five-checkbox list with the engine page; keep the same categories as rules.
**Phase 4 — Storage & Apps completion:** treemap, downloads manager, 500 MB chip + categories, duplicate delete, Store/MSIX listing & uninstall, leftover scan with `.reg` backups, startup publisher/impact.
**Phase 5 — New screens:** AI Tools (OCR, hotkeys, providers), Restore (drift), Agents (sandbox), Feedback, Health Check dashboard + score, Protection additions (real scan, guard, popup/hosts, network + speed test), tips banner, coexistence card.
**Phase 6 — Settings & extras:** scheduler tasks, portable mode, localisation incl. Urdu RTL, usage tracker, update check, migration helper, floating-toolbar persistence, tray verification.
**Phase 7 — Verification & polish:** Dark theme pass + screenshots of every screen in both themes (`app/screenshots/`), contrast script, accessibility pass, idle-RAM measurement (< 50 MB), simultaneous-run test with Microsoft PC Manager, installer build, README, final audit-file update.

---

## 12. FINAL ACCEPTANCE CHECKLIST (report each line with evidence)

- [ ] Builds with 0 warnings / 0 errors; `dotnet test` green.
- [ ] Runs side by side with Microsoft PC Manager for 10 minutes with no shared process name, mutex, registry key, folder, hotkey, task or URI scheme; evidence recorded.
- [ ] Process Manager refuses protected, own and `MSPCManager*` processes (test + manual).
- [ ] Boost reports only measured values; only Safe-tier temp items are deleted.
- [ ] Phase 1 scan deletes nothing (reflection test + manual) and prints the numbered table + Total Recoverable Space.
- [ ] `proceed`, `only 1, 2`, `only 3-7`, `keep 1, 2` work; `remove 1 and 2` clarifies and deletes nothing.
- [ ] Antigravity / Freebuff / Omnirush / VS Code / Cursor scans touch **only** cache/log/crash/update-installer folders; settings, tokens, extensions, chat/project DBs untouched (fixture tests).
- [ ] Locked files skipped without aborting; every action in `cleanup_log.txt`.
- [ ] Restore point offered before deletion; failure handled gracefully.
- [ ] Before/after free space and total freed verified by re-measurement.
- [ ] Large files (> 500 MB), duplicates (SHA-256), old downloads (> 90 d), Windows.old/Delivery Optimization, Prefetch/Font cache scanned and correctly tiered.
- [ ] Startup toggles use `StartupApproved` and are reversible; HKLM toggles work through the helper.
- [ ] Whitelisted folders never scanned or cleaned.
- [ ] Scheduled auto-clean creates/removes only `\Farmanullah\*` tasks.
- [ ] Light and Dark yellow themes switch live; **Dark screenshots exist for all screens**; contrast script passes.
- [ ] English + Urdu (RTL) work.
- [ ] Tray icon visible in the notification area; quick-boost, floating toolbar (position persists), hotkeys work with no conflict with Microsoft PC Manager.
- [ ] Portable mode writes nothing to registry/startup/tasks.
- [ ] Idle RAM in tray < 50 MB; UI responsive during scans.
- [ ] Installer installs/uninstalls without touching Microsoft PC Manager.
- [ ] README and the three audit files are updated and truthful.

**Begin with Phase 0 now.**


---

# MASTER PROMPT v3 ADDENDUM — INTELLIGENT CLEANUP, STORAGE INTELLIGENCE & PRODUCT-QUALITY UPGRADES

> **Integration rule:** This addendum extends Master Prompt v2. Keep every compatible requirement in Sections 0–12. Where this addendum gives a more specific safety rule, discovery rule, UX requirement, or acceptance test, the more restrictive rule wins. Do not remove existing working functionality. Do not claim a feature is complete until it is implemented, tested, and evidenced in `BUILD_STATUS.md`.

## 13. PRODUCT DIRECTION — FROM JUNK CLEANER TO PC HEALTH MANAGER

Evolve Farmanullah PC Manager into a trustworthy, local-first Windows maintenance application with four connected capabilities:

1. **Understand the PC:** measure storage, resource usage, installed development tools, caches, temporary files, startup activity, and cleanup history.
2. **Explain findings:** show the exact path, owner application, file type, size, age, last-modified date, confidence, safety tier, and reason the item is considered disposable.
3. **Clean deliberately:** create a reviewable cleanup plan, obtain explicit approval, execute only approved actions, and verify the result.
4. **Learn safely from usage:** remember user choices, exclusions, scan preferences, and previous cleanup outcomes locally. Never silently broaden deletion permissions because a user approved a previous cleanup.

The application must not promise that cleaning a cache will make a computer faster. Use measured language:
- "Potential storage recovery" before cleanup.
- "Space released" only after verifying the result.
- "Performance observation" only when a before/after measurement exists.
- "May be rebuilt by the application" where cache removal can cause a slower first launch or re-download.
- "No measurable performance change detected" when that is the honest result.

No telemetry, advertising, forced cloud account, remote execution, or background upload. All discovery, analysis, reports, preferences, and cleanup history remain local unless the user explicitly exports or shares them.

## 14. EXPANDED CLEANUP DISCOVERY CATALOG

### 14.1 Application identity and discovery confidence

Create a central `ApplicationIdentity` model for every detected application:
- Stable internal ID and display name.
- Vendor/publisher and executable names, where verifiable.
- Known install locations and known data roots.
- Detection source: registry, uninstall entry, process, shortcut, package manifest, known folder, or user-added path.
- Version, install date, last observed time, and currently-running state when available.
- Detection confidence: `Verified`, `Probable`, or `Unverified`.
- Cleanup rule pack/version and the reason it matched.

Do not infer that an arbitrary folder belongs to Antigravity, Freebuff, or Omnirush merely because its name looks similar. Use multiple signals where possible (known path, executable metadata, publisher, manifest, or user-confirmed folder). If identity is uncertain, show it as unverified and do not offer automatic deletion. Allow the owner to confirm a folder as an application root and save that decision locally.

**Special handling for Google Antigravity, Freebuff and Omnirush:**
- Provide explicit application entries and dedicated rule packs for these names.
- Search known per-user application-data roots and user-confirmed custom locations.
- Support case variants, dot-folders, nested vendor folders, and versioned directories.
- Do not invent paths or assume that all three products use Electron, Chromium, VS Code, or the same folder layout.
- If no known disposable subfolder is found, report "No supported cleanup target found" rather than scanning or deleting the whole application directory.
- Record the actual matched path and detection evidence in the report.
- Add a "Help identify this app folder" flow: user selects a folder, the app inspects metadata and safe known cache-folder names, previews findings, and asks the user to confirm the application identity before saving a rule.

### 14.2 Additional AI, coding and developer tools

Expand the detection catalog to include, where actually installed:
- Google Antigravity, Freebuff, Omnirush.
- Visual Studio, Visual Studio Code, VS Code Insiders, VSCodium, Cursor, Windsurf, Zed, JetBrains IDEs, Android Studio, IntelliJ IDEA, PyCharm, WebStorm, Rider, Eclipse, NetBeans.
- Git clients and tools, GitHub Desktop, Git LFS cache (report/opt-in only), SourceTree.
- Node.js, npm, npx, Yarn Classic/Berry, pnpm, Bun.
- Python, pip, pipx, Poetry, uv, Conda and virtual-environment caches.
- .NET SDK, NuGet HTTP cache, NuGet temp files, MSBuild temporary outputs (never user project `bin`/`obj` by default).
- Java, Gradle, Maven, Kotlin and Android Gradle caches.
- Rust/Cargo, Go build cache, Ruby/Bundler, PHP/Composer, Dart/Flutter.
- Docker, Podman, WSL distributions, Kubernetes tools and local container runtimes.
- Local AI runtimes and model managers such as Ollama and LM Studio.
- API/database tools such as Postman, database clients and local development servers.
- Game development tools and engines only when installed and their cleanup targets are explicitly documented.

For each integration, document:
1. How the app detects the product.
2. The exact cache/temp/log subfolders it may inspect.
3. The files and folders it must never touch.
4. Whether the application must be closed.
5. Whether an official cleanup command exists.
6. Whether the cache is rebuildable, downloadable, expensive to recreate, or potentially user-created.
7. The cleanup tier and deletion method.
8. How the scanner proves it is operating inside the intended application root.

### 14.3 Package-manager and build-cache intelligence

Add a dedicated **Developer Cache** category with separate rows for each detected package manager/runtime. Measure each cache before suggesting an operation.

- npm: use `npm cache verify` as a diagnostic where available. Do not run `npm cache clean --force` automatically. Explain that npm cache is generally reusable/downloadable and may need to be fetched again.
- Yarn: distinguish global cache, project-local cache, and Plug'n'Play artifacts. Never remove project dependency state automatically.
- pnpm: show store size and use supported pnpm commands only after detecting the installed version. Explain that packages may be shared across projects.
- Bun: distinguish download/install cache from project files.
- pip: inspect pip cache; use the installed pip command's supported cache interface when available.
- uv, Poetry, pipx and Conda: show separate caches and environments. Environments, package installations and named Conda environments are not disposable cache.
- NuGet: distinguish HTTP cache, temporary scratch files and global packages. Global packages are shared across solutions; mark cleanup Caution and explain restore cost.
- Gradle: distinguish wrapper distributions, dependency caches and build caches. Never delete a running daemon's active files; require confirmation and show that builds may need to download dependencies again.
- Maven: separate repository artifacts from temporary files. Repository deletion is Caution/Opt-in, never a default safe action.
- Cargo: distinguish registry download cache, source cache, git checkouts and build outputs. Never remove user project `target` folders by default.
- Go: use `go clean -cache` only as a clearly described Caution action; do not remove module downloads by default.
- Docker/Podman: query actual runtime state. Offer build-cache pruning through the official CLI only. Never prune images, stopped/running containers, volumes, networks, or build contexts as part of routine cleanup. Show the exact command and parsed impact before approval.
- Visual Studio: distinguish component/model caches from installer package caches. Never delete Visual Studio Installer, shared components, SDKs, workloads or repair sources.
- Android Studio/Gradle: never remove SDK platforms, build-tools, emulator images, AVDs, keystores, signing files or project files.

If a package manager is unavailable, do not invoke it. If its version or command syntax cannot be verified locally, disable that action and explain why. Never use a shell command assembled from untrusted folder names.

### 14.4 Expanded application and browser caches

Add supported, narrowly scoped rules for:
- Chrome, Edge, Brave, Firefox, Opera, Vivaldi and other explicitly identified Chromium/Firefox browsers.
- Every detected browser profile, not only `Default`.
- Teams, Slack, Discord, Zoom, Spotify and other explicitly identified desktop apps.
- Microsoft Store app caches only where a documented, safe target exists.

For browsers, allow only documented cache targets such as HTTP cache, Code Cache, GPUCache, shader cache, and supported Service Worker CacheStorage paths. Browser must be closed before a target is cleaned unless a documented API safely handles the operation. Never touch cookies, passwords, login state, browsing history, bookmarks, autofill, extensions, open tabs, session restore data, or profile databases.

A generic Chromium/Electron folder-name match is discovery evidence only. It must not become permission to clean that folder. Require verified app identity or user confirmation before applying an app-specific rule.

### 14.5 More Windows and user-level leftovers

Add scan-only or appropriately tiered detection for:
- Old crash reports, application error reports, WER archives and dumps.
- Old installer/update download remnants with verified ownership.
- Old temporary extraction folders.
- Old log rotations and stale diagnostic bundles.
- Broken shortcuts, empty folders and orphan candidates.
- Old Windows error dump files and minidumps.
- Thumbnail and DirectX/GPU shader caches.
- Delivery Optimization cache using supported Windows interfaces.
- Windows Update download cache using a service-aware helper workflow.
- Recycle Bin contents by drive.
- Old files in user-selected temporary folders.
- Leftover folders after a confirmed application uninstall.

Do not call a folder "orphaned" only because its application is not currently running. Check install records, publisher, executable references, package ownership, shortcuts, services, scheduled tasks and user confirmation. Report uncertainty.

### 14.6 Cleanup rule metadata

Extend each rule with:
- `ApplicationId`, `RulePackVersion`, `DetectionConfidence`.
- `PathKind` (KnownFolder, RegistryDerived, UserConfirmed, PackageManagerReported).
- `LastModifiedCutoff`, `MinimumFileAge`, `MaximumFileAge` if relevant.
- `EstimatedRebuildCost` and `RebuildImpact`.
- `RequiresApplicationClosed`, `RequiresNetwork`, `RequiresElevation`.
- `CanUseOfficialCleanupCommand`, `CommandAvailabilityCheck`.
- `FileCount`, `FolderCount`, `MeasuredBytes`, `MeasurementTimestamp`.
- `SkipReasons`, `ExclusionPatterns`, `AllowedExtensions`.
- `RuleExplanation`, `RiskExplanation`, `RebuildExplanation`.
- `LastSuccessfulCleanup`, `LastResult`.

Never load executable cleanup code from `cleanup-rules.json`. The file may define declarative patterns and metadata only. Validate schema, paths, lengths, regex complexity and rule IDs. Only code-reviewed built-in handlers or a closed set of typed official-tool actions may perform cleanup.

## 15. NEW CLEANUP EXPERIENCE AND SMART WORKFLOWS

### 15.1 Cleanup Center

Create a dedicated Cleanup Center with these sections:
- Overview: measured junk candidates, potential recovery, last scan, last cleanup, and current scan status.
- AI & IDE Cleanup.
- Developer Cache.
- Windows Cleanup.
- Browser & App Cache.
- Logs & Crash Reports.
- Old Downloads.
- Large Files.
- Duplicate Files.
- Uninstall Leftovers.
- Custom Locations.
- Cleanup History.

Each category shows item count, measured size, risk tier, scan date, and a short explanation. Clicking a category opens a detailed list; no category should be a decorative tile with no working action.

### 15.2 Cleanup profiles

Provide built-in profiles:
- **Quick Clean:** Safe-tier temporary files and clearly disposable cache only.
- **Developer Clean:** developer/IDE/package-manager cache, with Caution items excluded until selected.
- **AI Workspace Clean:** supported AI IDE/agent caches and logs only; preserve prompts, chat history, workspace state, models and credentials.
- **Browser Clean:** cache only, never private browsing data.
- **Windows Clean:** safe Windows temporary files and supported diagnostic caches.
- **Deep Review:** scan all supported categories, but do not automatically approve any deletion.
- **Custom:** user-selected categories and paths.

Profiles are presets for scanning and selection, not permanent deletion permissions. A profile must never bypass the approval gate, SafetyGuard, whitelist, running-app checks or risk-tier rules. Show exactly which categories the profile includes before scanning.

### 15.3 Smart recommendations

Add a recommendation engine based only on measured local data:
- "Large cache found" with exact size and location.
- "This app is using significant cache storage" with last scan date.
- "Browser cache could be rebuilt after cleaning."
- "This folder has not changed in N days" (not proof it is unnecessary).
- "Low disk space" with drive-specific threshold.
- "Large downloads older than your selected age."
- "Potential duplicate group" with verification status.
- "Cleanup skipped because the application is running."
- "No safe cleanup targets found."

Recommendations must include **why**, **what will happen**, **risk**, and **how to undo or recover where applicable**. Never recommend a deletion solely because a folder is large or old.

### 15.4 Cleanup review table

For each candidate show:
- Selection checkbox.
- Item number and category.
- Application/vendor.
- Exact full path (copyable).
- File/folder count.
- Measured size and measurement time.
- Oldest/newest modified timestamp.
- Detection confidence.
- Risk tier.
- Application running/closed state.
- Rebuild impact.
- Deletion method.
- Reason found and exact rule that matched.
- Open folder, inspect files, exclude path, and rule details actions.

Add filters for Safe/Caution/Opt-in, application, category, size, age, and scan status. Sorting and filtering must not change item identity or approval IDs. If the scan result changes after review, invalidate the old approval and require a fresh review.

### 15.5 Before-and-after cleanup report

Show:
- Selected candidates and total selected bytes.
- Estimated recoverable space, explicitly labelled as estimate.
- Actual deleted bytes and actual free-space change, separately.
- Files deleted, skipped, failed and quarantined.
- Per-drive free space before/after.
- Duration and cleanup profile.
- Items skipped because they were locked, protected, whitelisted, changed since scan, or required elevation.
- Cache rebuild warning and official command used, if any.

Do not add file sizes and drive free-space changes as if they were guaranteed to match. Filesystem compression, hard links, sparse files, open handles, delayed deletion and concurrent disk activity can cause differences. Explain material differences.

### 15.6 Undo, quarantine and recovery

For user documents such as old downloads and duplicates, use Recycle Bin by default. Provide restore instructions and a link/button to open Recycle Bin.

For selected non-system cleanup targets, optionally support a **Quarantine** workflow:
- Only use for file types and locations where moving the item is safe.
- Store a manifest containing original path, quarantine path, timestamp, SHA-256 where practical, size and reason.
- Keep quarantine inside the app's own data directory with access restricted to the current user.
- Never quarantine active databases, application state, system files, credentials or locked files.
- Restore must verify that the original destination is not occupied; never overwrite silently.
- Retention and maximum-size limits must be configurable.
- Quarantine is not a substitute for a backup and must not be advertised as guaranteed rollback.

Cache/temp files may be permanently removed only when the rule explicitly defines that method and the review screen clearly labels it. Never empty Recycle Bin or delete user files silently.

## 16. STORAGE INTELLIGENCE AND PERFORMANCE DIAGNOSTICS

### 16.1 Storage overview

Add:
- Per-drive capacity, used, free and filesystem.
- Storage category breakdown with measured totals and an "Unclassified" category for space that cannot be attributed.
- Largest folders and files, with cancellable drill-down.
- Top storage-consuming applications, only where ownership can be reasonably attributed.
- Growth tracking based on local snapshots.
- Scan duration, last successful scan, and access-denied/unreadable totals.
- A warning when a scan is incomplete; never present partial totals as complete.

Use a treemap or sunburst only if the underlying folder measurements are complete enough to make it meaningful. Every chart must have a text/table alternative and exact values available on hover or selection.

### 16.2 Storage growth and trends

Maintain optional local snapshots:
- Daily or weekly storage totals per drive.
- Selected category sizes.
- Selected app-cache sizes.
- Cleanup history and actual bytes released.

Allow the user to pause, clear, export or disable history. Store minimal metadata; do not index file contents. Provide retention options such as 7, 30, 90 days and unlimited, with a clear storage cost.

### 16.3 Performance diagnosis

Add a Performance page that measures:
- CPU, memory, disk activity, GPU when available, uptime and battery information when exposed.
- Top processes by CPU, memory and disk I/O.
- Startup entry count and measured/estimated impact.
- Disk free-space pressure.
- Background activity during a user-triggered observation window.

Do not use arbitrary "optimization scores" or claim that a process is bad because it uses memory. Explain that Windows uses available RAM for caching. Show the source, sampling interval, permissions and limitations of every metric.

Provide a **Before/After Observation** workflow:
1. Capture a short baseline sample.
2. Run only the selected maintenance operation.
3. Wait for the user-selected settling period.
4. Capture a second sample under comparable conditions.
5. Show differences and state that other activity may affect the comparison.

Do not automatically terminate processes, disable services, change power plans, edit registry performance values, or alter virtual memory as part of a cleanup profile.

### 16.4 Safe maintenance actions

Add optional, individually confirmed actions:
- Open Storage Sense settings.
- Open Windows Disk Cleanup / Storage settings.
- Run supported Defender scan.
- Open Task Manager, Resource Monitor and Startup Apps.
- Review large startup impact.
- Clear DNS resolver cache only after explaining what it does.
- Check drive health using supported read-only Windows APIs/commands.
- Run Windows component cleanup only through supported DISM commands and a separate explicit approval.

Never run registry cleaners, RAM cleaners, driver updaters, "internet boosters", service-disabling scripts, or undocumented Windows tweaks. Do not advertise them as performance features.

## 17. USER EXPERIENCE, VISUAL DESIGN AND ACCESSIBILITY UPGRADE

### 17.1 Design direction

Retain the warm honey/yellow identity and existing canonical palette in Section 4. Do not replace the brand with a generic blue Windows clone.

Polish the interface into a cohesive premium Windows desktop application:
- Fluent-inspired surfaces, clear hierarchy and consistent spacing.
- Calm warm neutral background, white/light cards, deep charcoal dark surfaces, honey accent used for actions and active states.
- Consistent icon family and stroke weight.
- Clear typography, readable secondary text and numeric alignment.
- Consistent corner radii, card padding, separators, tooltips and focus states.
- Restrained gradients; use them only for hero/summary surfaces.
- Avoid excessive glass effects, glowing borders, oversized empty cards, random accent colors and decorative charts with no data.
- Use real application icons only when safely extracted; otherwise use consistent local vector icons.

### 17.2 Home dashboard refinement

Prioritise the information users need:
1. PC status and drive-space summary.
2. Primary Quick Scan / Cleanup action.
3. Measured storage recovery opportunity.
4. Health/performance observations.
5. Recent cleanup activity and important warnings.

Show a compact "Last scanned" timestamp and clear scan state. Do not show fake percentage gauges, arbitrary scores or placeholder graphs.

### 17.3 Interaction details

- Every button must have a working action or be visibly disabled with an explanation.
- Every scan supports cancellation; cancellation stops new work and safely completes/records the current file operation.
- Long-running operations show progress, current category, current path only where privacy-safe, elapsed time and cancel action.
- Avoid blocking the UI thread.
- Preserve scroll position and filters when returning from item details.
- Confirm destructive actions with a clear summary, not repeated generic dialogs.
- Provide empty states that explain how to get a real result.
- Provide actionable error states with retry, open folder, view log or learn-more actions.
- Use toast notifications sparingly; important results remain in the app.
- Add tooltips and keyboard shortcuts with conflict detection.
- Respect reduced motion, high contrast, Windows scaling and multi-monitor DPI.
- Support keyboard-only use, screen readers and visible focus.
- Localise all new strings through the existing localisation architecture, including Urdu RTL layout.

### 17.4 Visual states that must be designed

Produce and verify Light Yellow and Dark Yellow designs for:
- Home and Health Check.
- Cleanup Center and detailed scan results.
- Approval gate and final report.
- AI/Developer cache detail.
- Storage explorer and large files.
- Duplicate review.
- Apps and leftover scan.
- Protection and Restore.
- Agents/Sandbox.
- Settings, whitelist and schedules.
- Cleanup history/log viewer.
- Empty, loading, scanning, cancelling, success, partial success, warning, permission denied, offline and fatal-error states.

Capture actual application screenshots; do not create mock screenshots and label them as implementation evidence.

## 18. RELIABILITY, SECURITY AND PRIVACY HARDENING

### 18.1 Scanner integrity

- Scanner code must remain read-only and must not contain deletion or move APIs.
- Scans must use cancellation tokens, bounded concurrency and memory-efficient enumeration.
- Do not load an entire directory tree or every file path into memory for very large drives.
- Handle access denied, path too long, broken symlink, disconnected drive, cloud placeholder, reparse point, locked file, invalid Unicode and file disappearing mid-scan.
- Never follow symlinks/junctions outside an allowed root.
- Revalidate file identity, canonical path, size and last-modified metadata immediately before action. If changed since approval, skip and require a new scan.
- Protect against time-of-check/time-of-use path replacement as far as Windows APIs allow; use handle-based validation where practical.
- Never traverse network shares, removable drives or cloud-synced folders unless the user explicitly selects them.
- Never hydrate online-only cloud files to measure or clean them.

### 18.2 Rule quality and trust

Every built-in cleanup rule must have:
- A documented owner and purpose.
- A reviewed path allow-list.
- A deny-list and fixture tests.
- A documented age threshold.
- A risk classification and rebuild impact.
- A clear deletion method.
- A test for active/running application behavior.
- A test proving unrelated sibling folders remain untouched.

Rules must fail closed. An unknown folder, unsupported version, ambiguous match or failed identity check means scan-only or skip, never broader deletion.

### 18.3 Command and process safety

- Do not use `cmd.exe /c` or concatenate user-controlled strings into PowerShell.
- Prefer direct executable invocation with `ProcessStartInfo.ArgumentList`.
- Use a fixed executable allow-list and typed argument models.
- Validate executable path, signer where relevant, version and availability.
- Capture stdout/stderr, exit code, timeout and cancellation.
- Never run commands downloaded from the internet.
- Never accept a cleanup command from a rule JSON file.
- Do not elevate the whole main application.
- Helper IPC must validate the caller, message size, command type, approved plan ID and user SID.
- Reject replayed, expired or modified approval plans.

### 18.4 Privacy controls

Add a Privacy & Data page showing:
- Data stored locally and exact paths.
- Log and report retention.
- Usage tracking state.
- Scan-history state.
- Exported report location.
- Any optional network request and its purpose.
- Clear history, clear local reports and reset settings actions.

Do not log file contents, document names unnecessarily, command-line secrets, environment-variable values, tokens, passwords or API keys. Redact sensitive path segments in exported diagnostics by default, with an explicit option to include full paths for the user's own troubleshooting.

## 19. IMPLEMENTATION AND DELIVERY RULES

### 19.1 Phased implementation additions

Extend the existing execution plan without skipping the original phases:

**Phase 3A — Rule-pack foundation**
- Application identity model and confidence.
- Declarative, validated cleanup rule packs.
- Dedicated Antigravity, Freebuff and Omnirush rule packs.
- Verified app discovery and user-confirmed custom roots.
- Fixture tests for every supported rule pack.

**Phase 3B — Developer ecosystem**
- Package-manager cache integrations.
- IDE and browser cache coverage.
- Official-tool availability checks.
- Running-process detection and close-app guidance.
- Rebuild-impact explanations.

**Phase 3C — Cleanup Center**
- Profiles and category navigation.
- Review table and detailed item inspection.
- Smart recommendations.
- Typed approval plan and invalidation on changed scan results.
- Quarantine/Recycle Bin support where safe.
- Final report and history.

**Phase 4A — Storage intelligence**
- Storage breakdown and drill-down.
- Growth snapshots and retention.
- Large-file and duplicate workflows.
- Uninstall leftover review.

**Phase 5A — Performance diagnostics**
- Measured resource sampling.
- Before/after observation.
- Honest health indicators and actionable guidance.

**Phase 7A — UX and reliability**
- Complete Light/Dark visual verification.
- Accessibility and DPI testing.
- Large-directory and slow-disk testing.
- Cancellation, crash recovery and partial-failure testing.
- Privacy audit and exported-report redaction tests.

### 19.2 No fake completion

For every feature, record:
- Implementation status: `[DONE]`, `[PARTIAL]`, `[MISSING]`.
- Files/classes changed.
- Build/test evidence.
- Manual verification steps.
- Known limitations.
- Supported Windows versions and permission requirements.

A visible button, menu item, static chart, hard-coded number or placeholder message does not count as implementation. A feature is `[DONE]` only when its actual behavior is connected to real system data and passes tests.

### 19.3 Required test expansion

Add tests for:
- Unknown and ambiguous app identity.
- Antigravity, Freebuff and Omnirush fixture roots.
- Electron/Chromium false-positive detection.
- Multiple browser profiles.
- Package-manager unavailable/version mismatch.
- Project folder, `bin`, `obj`, `node_modules`, virtual environment and model-weight protection.
- Docker images/containers/volumes never selected by build-cache cleanup.
- Cloud placeholders and disconnected drives.
- Reparse point and junction escape.
- Path replacement between scan and execution.
- File changed after approval.
- Approval plan expiry/replay/tampering.
- Cancellation during enumeration and execution.
- Partial scan and partial cleanup reporting.
- Quarantine restore conflict and manifest integrity.
- Report redaction.
- Large directory memory/concurrency limits.
- Light/Dark theme resource completeness and RTL layout.
- Keyboard navigation and accessible names.

## 20. EXPANDED ACCEPTANCE CHECKLIST

Add these acceptance checks to Section 12:

- [ ] Cleanup Center contains working categories and real measured results.
- [ ] Antigravity, Freebuff and Omnirush have explicit rule packs; no guessed paths are silently cleaned.
- [ ] Application identity shows detection evidence and confidence.
- [ ] Unverified application folders are scan-only until confirmed.
- [ ] IDE, AI-agent, browser and developer-tool rules are independently testable.
- [ ] npm/Yarn/pnpm/Bun/pip/uv/Poetry/Conda/NuGet/Gradle/Maven/Cargo/Go cache handling uses supported local tooling and accurate warnings.
- [ ] Docker cleanup only targets build cache and never images, containers or volumes.
- [ ] Every browser profile is discovered; cookies, passwords, history, sessions and extensions remain untouched.
- [ ] Cleanup profiles never bypass review, whitelist, SafetyGuard or approval.
- [ ] Review table shows exact path, measured size, file count, tier, confidence and rebuild impact.
- [ ] Changed files and changed scan results invalidate previous approvals.
- [ ] Quarantine/Recycle Bin restoration is tested where supported.
- [ ] Storage breakdown distinguishes measured, estimated and unclassified space.
- [ ] Storage history is local, optional, retention-controlled and deletable.
- [ ] Performance observations use measured before/after samples and do not promise unverified gains.
- [ ] No registry cleaner, RAM cleaner, driver updater or undocumented tweak is shipped as an optimizer.
- [ ] All visible controls work or explain why they are disabled.
- [ ] Scan cancellation, locked files, permission failures and disconnected drives do not crash the app.
- [ ] Exported reports redact sensitive details by default.
- [ ] No scan reads file contents unless a separately explained feature explicitly requires it.
- [ ] All new screens and states are verified in Light Yellow and Dark Yellow.
- [ ] Large-folder scans remain responsive and memory-bounded.
- [ ] `PROJECT_SPEC.md`, `BUILD_STATUS.md`, `CHANGELOG.md` and README describe the real shipped behavior.

## 21. FINAL INSTRUCTION TO THE CODING AGENT

Start by reading the existing repository and all three audit documents. Then compare the current implementation against Master Prompt v2 and this v3 addendum.

Do not regenerate the project, replace working architecture, or create a visually impressive but nonfunctional mockup. Preserve the existing solution and implement in small, testable phases.

Prioritise in this order:
1. Safety and data preservation.
2. Correct discovery and measurement.
3. Review and approval workflow.
4. Reliable cleanup and verification.
5. Storage/performance insights.
6. Visual polish and convenience features.

When in doubt, do not delete. Report the item, explain the uncertainty, and let the user decide after a fresh review.

**The finished product must be a dependable Windows maintenance tool—not an aggressive "one-click optimizer". It must earn the user's trust through transparent measurements, narrow cleanup rules, reversible actions where possible, and honest reporting.**
