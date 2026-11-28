# Traceability matrix

Links each requirement group to the card that delivers it, the primary files, and the evidence
that closes it. Card details live in the plan documents; this table is the index.

| Req ID | Requirement (source) | Card | Primary files / areas | Acceptance evidence |
|---|---|---|---|---|
| TR-01 | Protocol: one card per session, §1–§4 + card only (`START` §1) | all | `AGENTS.md`, `docs/readiness/` | `PROGRESS_LOG.md` card reports |
| TR-02 | Build 0 warnings/0 errors; tests green (`START` §1.4) | all | `app/PCManager.sln` | Build/test output in `PROGRESS_LOG.md` |
| TR-03 | No stubs/TODO/fake data (v7 L2) | all | whole app | `fpm-safety-contract` checklist |
| TR-04 | Files < 400 lines (`START` §1.5) | all | `app/src/**` | review; split when exceeded |
| TR-05 | Tokens only; no new literals (v7 §8, U1) | U1, U10 | `Styles/Spacing.xaml`, `Styles/Themes/*` | `tools/lint-xaml.ps1` ratchet |
| TR-06 | 3D icon system, no vendor packs (v7 §8.8, U2/U3) | U2, U3 | `Controls/Icon3D*`, `Assets/Icons3D*`, `tools/IconGen` | `IconGen --check` + gallery capture |
| TR-07 | Motion kit with one policy (U4) | U4 | `Core/MotionPolicy.cs`, `Motion/*` | unit tests + reduced-motion capture |
| TR-08 | Splash before shell, skippable, staged (U5) | U5 | `Views/SplashWindow*`, `App.Startup.cs` | captures Light/Dark/reduced; log timings |
| TR-09 | Page transitions + sliding indicator + scroll memory (U6) | U6 | `Views/MainWindow*`, `ViewModels/MainViewModel.cs` | frame trace; scroll percentage test |
| TR-10 | Boost animation from the real state machine (U7, W5) | U7, W5 | `BoostAnimationHost`, Home + toolbar | state-machine tests + captures |
| TR-11 | Remaining animations, scrolling, spacing (U8–U10) | U8, U9, U10 | views, `SmoothScroll`, `UI_SPACING_AUDIT.md` | captures at 360/560/1280, lint |
| TR-12 | Feature polish + dead-code cleanup (U11, U12) | U11, U12 | all views/services | tests green before/after; docs updated |
| TR-13 | Window chrome base, Snap Layouts (W1; v7 §8.3 D7) | W1 | `FpmWindow` base | manual min/max/close + automation names |
| TR-14 | Virtual-desktop following via documented COM (W2) | W2 | `VirtualDesktopFollower` | 3-desktop test notes |
| TR-15 | Toolbar survives close; one instance (W3) | W3 | tray/lifecycle, pipe | lifecycle tests + manual |
| TR-16 | Toolbar edge docking, persistence (W4) | W4 | toolbar window | multi-monitor/DPI notes |
| TR-17 | Sensors: battery/temps, honest states (T1, T2) | T1, T2 | `ISensorService`, helper `ReadSensors` | parser fixtures + unavailable states |
| TR-18 | Toolbox widget layout drives toolbar (T3, T4) | T3, T4 | `toolbox.layout.json`, toolbar | restart/toolbar-only persistence |
| TR-19 | Windows debloat catalog + typed engine (D1–D3) | D1–D3 | `Assets/debloat/catalog.json`, planner/executor | schema tests; journal undo |
| TR-20 | Debloat advanced: other user, sysprep, CLI, presets, drift (D4) | D4 | `Core/Debloat/DebloatTargets.cs`, `DebloatPresetFile.cs`, `DebloatCommandLine.cs`; `Helper/HelperHive.cs`, `DebloatRegistry.cs`; `Cli/DebloatCli.cs`; `ViewModels/DebloatViewModel.Advanced.cs` | resolver + hive cleanup tests; preset round-trip; CLI dry-run exit 0; UIA probe reads target/preset/drift surface; no live UAC run (R-33) |
| TR-21 | Debloat tests incl. reference parity (D5) | D5 | `DebloatSafetyTests`, `DebloatConflicts`, `DebloatRestoreGuard` | 267 tests; map 106 = 90 ported + 16 excluded; detect/action invariant; conflict/restore/dry-run evidence in `PROGRESS_LOG.md` |
| TR-22 | ADB service/consent/signature (A1) | A1 | `IAdbService` | unsigned adb rejected; consent flow |
| TR-23 | Android lists + schema (A2) | A2 | `Assets/android/uad_lists.json` | schema validation tests |
| TR-24 | Package engine, denylist, undo (A3, A5) | A3, A5 | `IPackageEngine`, snapshots | fake-runner tests; snapshot restore |
| TR-25 | Android UI + multi-device/wireless (A4, A6) | A4, A6 | Debloat → Android tab | warning flow; typed confirmation |
| TR-26 | Android tests (A7) | A7 | tests | command-builder rejection tests |
| TR-27 | License, notices, community files (R2) | R2 | `LICENSE`, `THIRD_PARTY_NOTICES.md`, `.github/` | files present; GPL text; notices match packages |
| TR-28 | Installer, upgrade, uninstall (R3) | R3 | `installer/FPM.iss` | test matrix in `BUILD_STATUS.md` |
| TR-29 | CI + release (R4) | R4 | `.github/workflows/*` | green CI run; checksums |
| TR-30 | README, logo, real screenshots (R5) | R5 | `README.md`, `docs/images/` | screenshots of the running app |
| TR-31 | Repo description/topics (R6) | R6 | GitHub settings | description/topics applied or commands printed |
| TR-32 | Repo hygiene + secret scan (R1) | R1 (done) | `.gitignore`, `REPO_AUDIT.md` | `git check-ignore` proofs; gitleaks pending |
| TR-33 | Safety laws: scan → approve → apply → verify → undo (v7 §2, §10–§12) | all destructive cards | `ISafetyGuard`, helper, journals | `fpm-safety-contract` checklist + tests |
| TR-34 | No telemetry, no remote execution (v7 L5) | all | whole app | review; no network calls without user action |
| TR-35 | Localization English + Urdu RTL (v7 §18, START §1.10) | U10 + all UI | `.resx` + `ILocalizationService` | every string keyed; RTL layout capture |
| TR-36 | Accessibility: keyboard, names, contrast, reduced motion (v7 §18) | all UI | views, `MotionPolicy` | UIA names; contrast gate; reduced-motion captures |
| TR-37 | Performance budgets (v7 §17, U4) | W1, U6, V1 | frame probe, startup path | p50/p95 recorded; 16 ms target |
| TR-38 | Privacy: local data only, exact paths, reset (v7 §18) | Settings cards | UI copy + `%LOCALAPPDATA%\FPM` | audit of logs/exports for personal data |
| TR-39 | Coexistence with Microsoft PC Manager (v7 §5.2) | existing + W/D | protected lists | 10-minute side-by-side test |
| TR-40 | Migration from legacy identity (v7 §5.3) | done | `ILegacyIdentityMigrationService` | migration tests green |

| TR-41 | CI: build, tests, lint, contrast, icons (R4) | R4 | `.github/workflows/ci.yml` | actionlint exit 0; every `run:` executed locally green (`PROGRESS_LOG.md`) |
| TR-42 | Secret scan over working tree **and history** (R1, R4) | R4 | `tools/secret-scan.ps1`, CI `secret-scan` job | gitleaks 8.30.1: 77 commits, no leaks; checksum-verified install branch tested |
| TR-43 | Reproducible SDK for local and CI builds | R4 | `global.json` | `dotnet --version` = 8.0.428; Release build `-warnaserror` + 166 tests green |
| TR-68 | Debloat catalog ported with typed actions and OS gating (D1) | D1 | `tools/debloat_catalog_gen.py`, `Core/Assets/debloat/catalog.json`, `Core/Debloat/*` | 90 tweaks / 161 apps, 8 catalog tests |
| TR-69 | Every reference feature mapped or excluded with a reason (D1) | D1 | `docs/dev/win11debloat-feature-map.md` | 106 rows = 90 ported + 16 excluded |
| TR-70 | Hive/path allow-list and denylist enforced at load (D1) | D1 | `DebloatCatalogValidator` | tests reject Run-key path and Store removal |
| TR-64 | Typed temperature readings with zone/source labels (T2) | T2 | `Core/Models/TemperatureModels.cs`, `Services/Sensors/WindowsTemperatureProbe.cs` | 8 temperature tests; ACPI Kelvin fixture |
| TR-65 | ACPI zone never called CPU; unavailable/needs-permission with reason (T2) | T2 | `TemperatureText`, `TemperatureTile`, `TemperatureStatusViewModel` | live `NeedsPermission` on Home, toolbar, toolbar-only, Settings |
| TR-66 | Same sampled temperature in Home and toolbar, refreshing on the shared cadence (T2) | T2 | `SensorService`, `SensorCadence` | Samples 16:36:50 → 16:36:55 |
| TR-67 | °C/°F setting and 70/85 thresholds (T2) | T2 | `AppSettings.TemperatureUnit`, `TemperatureText` | fixture tests; toggle not captured |
| TR-60 | Typed battery readings, never fabricated (T1) | T1 | `Core/Models/SensorModels.cs`, `Core/Formatting/BatteryText.cs` | 8 service tests incl. no-battery and unknown-state paths |
| TR-61 | Shared 5 s / 30 s sampler, paused demand (T1) | T1 | `Services/Sensors/BatterySensorService.cs`, `ISensorService` | Settings sample time advanced 16:25:09 → 16:25:19; log 5 s steps |
| TR-62 | Battery on Home, toolbar (docked, hidden main, toolbar-only) and Settings (T1) | T1 | `BatteryStatusViewModel`, `Controls/BatteryTile.xaml`, `HomeView.xaml`, `FloatingToolbarWindow.xaml`, `SettingsView.xaml` | screenshots at 100 % / AC in all four surfaces |
| TR-63 | Honest unavailable state hides the desktop tile but explains itself in Settings (T1) | T1 | `BatteryTile` visibility, Settings card | fixture tests; not rendered (R-23) |
| TR-57 | One Boost state machine and confirmation rule app-wide (W5/U7) | W5 | `Core/Models/BoostPhase.cs`, `Services/Boost/BoostCoordinator.cs` | 6 tests; toolbar run logged “Boost (Manual)…” |
| TR-58 | Compact Boost animation in the toolbar, working hidden/toolbar-only (W5) | W5 | `Controls/BoostAnimationHost.xaml(.cs)`, `FloatingToolbarWindow.xaml` | toolbar chip “2 MB freed” with main window hidden; working-state screenshot |
| TR-59 | Boost result updates Home (W5) | W5 | `HomeViewModel` subscription to the coordinator | Home automation text “2 MB of temporary files released.” |
| TR-54 | Dock the toolbar to any edge with a slim tab and hover expansion (W4) | W4 | `Core/Models/ToolbarDock.cs`, `FloatingToolbarWindow.xaml(.cs)` | docked 40×210 px, expanded 442×105 px with drawer, collapsed back; 6 tests |
| TR-55 | Toolbar opacity, always-on-top, pin, reset and hide, persisted (W4) | W4 | `ToolboxModels.cs`, context menu, `FloatingToolbarWindow.xaml.cs` | slider + presets wired; persistence saved; menu items not clicked |
| TR-56 | Hide the toolbar under fullscreen apps, never steal focus (W4) | W4 | `SHQueryUserNotificationState` poll, `ShowActivated=False`, host without `Activate` | inspection only; fullscreen not exercised (R-21) |
| TR-50 | Remember window size/position per monitor and DPI; clamp on restore (W1) | W1 | `Core/Models/WindowBounds.cs`, `AppSettings.WindowBoundsByMonitor`, `MonitorIdentity.cs`, `MainWindow.xaml.cs` | 7 tests; runtime key `\\.\DISPLAY1@120` persisted and restored |
| TR-51 | Closing the main window keeps FPM and the toolbar alive (W3) | W3 | `AppLifetimePolicy`, `App.OnMainWindowClosing`, `App.Startup.cs` | alive=True, main hidden, `FPM Toolbar` visible; 8 policy tests |
| TR-52 | `--toolbar-only` runs tray + toolbar without the shell (W3) | W3 | `App.StartToolbarOnly`, `StartToolbarOnlyProcess` | no main handle, toolbar visible, memory recorded in `BUILD_STATUS.md` |
| TR-53 | Second launch hands off to the running instance (W3) | W3 | `SingleInstanceChannel`, `App.OnShowRequested` | second process exit 0, first window visible; toolbar-only handed off to the full UI |
| TR-48 | Follow the active virtual desktop without pinning or stealing focus (W2) | W2 | `Services/Desktop/VirtualDesktopManagerApi.cs`, `VirtualDesktopFollower.cs`, `PCManager/Services/DesktopFollowLoop.cs` | 7 unit tests; COM activation logged at runtime |
| TR-49 | “Show FPM on all desktops” defaults: toolbar on, main window off; persists (W2) | W2 | `ToolboxModels.cs`, `SettingsViewModel.cs`, `SettingsView.xaml` | toggle wrote `ToolbarFollowDesktops: false`; card renders at 560 px |
| TR-45 | One chrome base: rounded corners, dark frame, Snap Layouts, Alt+Space (W1) | W1 | `Views/FpmWindow.cs`, `MainWindow.xaml`, `IconGalleryWindow.xaml` | build 0/0; max/restore/minimise verified by UIA; Light+Dark at 560/1280 |
| TR-46 | Every window control reachable by keyboard/automation (W1) | W1 | `Styles/Controls.xaml` (`NavItemStyle`), title-bar buttons | UIA names: `Home…Settings`, `Minimise`, `Maximize or restore`, `Close` |
| TR-47 | Frame pacing within the 16 ms budget (W1, U4) | W1 | `Motion/FrameTimingProbe.cs` | **blocked**: 40 Hz panel (25 ms refresh); trace recorded, 16 ms not evidenceable — Q-08 |
| TR-71 | Typed debloat apply/revert/detect over the existing IPC (D2) | D2 | `Core/Elevation/HelperProtocol.cs`, `Helper/DebloatCommands.cs`, `Services/Elevation/ElevationService.cs` | helper validates catalog ids/pre-state; 232 tests green |
| TR-72 | Ordered typed plan with OS gate, denylist and reboot/Explorer flags (D2) | D2 | `Core/Debloat/DebloatPlanner.cs` | planner tests (ordering, OS, key-delete/Appx/protected) |
| TR-73 | `.reg` backup, typed pre-state journal, reverse-order undo (D2) | D2 | `Core/Debloat/DebloatJournal.cs`, `Services/Debloat/DebloatEngine.cs` | apply→undo exact-restore test; backup file test |
| TR-74 | Restore point attempted, skipped with a reason when unavailable (D2) | D2 | `DebloatEngine.TryRestorePointAsync` | fake restore-point test records `Skipped` |
| TR-75 | Mid-run failure rolls back the run truthfully (D2) | D2 | `DebloatEngine.ApplyAsync`/`RollbackAsync` | failure test asserts `RolledBack` + restored first tweak |
| TR-76 | UI surface invokes detect/apply/revert/undo (D2) | D2 | `Views/SettingsView.xaml`, `ViewModels/DebloatEngineViewModel.cs` | UIA probe: card + plan visible; UAC path not exercised (R-28) |
| TR-77 | D3 categories, search and documented preset rules over the catalog (D3) | D3 | `Core/Debloat/DebloatPresets.cs`, `DebloatPageCatalog.Filter`, `DebloatViewModel` | preset/filter tests; UIA page probe |
| TR-78 | D3 computed plan with per-tweak risk/impact and skipped reasons (D3) | D3 | `DebloatPageCatalog.BuildImpactSummary`, `DebloatPlanRow`, `DebloatView.xaml` | plan summary test; D5 probe read 21 ready / 32 conflict-skipped |
| TR-79 | D3 Detect/Apply/Undo through the real engine, report and history (D3) | D3 | `DebloatViewModel.Runs.cs`, `DebloatJournalStore.LoadAll` | report tests; restore-toggle test |
| TR-80 | D3 Apps tab catalog surface, with removal refused (D3) | D3 | `DebloatView.xaml` Apps tab, `DebloatAppRow` | probe found “Preinstalled apps”; no removal offered |
| TR-81 | D3 empty/no-catalog/running/partial-rollback/error states (D3) | D3 | `DebloatView.xaml`, report model | XAML bindings + rollback report test |
| TR-82 | D3 runtime page evidence through UIA (D3) | D3 | `tools/ui-probe-debloat.ps1` | probe output in `PROGRESS_LOG.md`; UAC not approved (R-29) |
| TR-44 | Release workflow (tag → installer + checksums) | R4 (release half), R3 | `.github/workflows/release.yml` | deferred: needs the owner's push/signing decision; blocker recorded in `RISKS.md` |

## Coverage notes

- TR-35 has **no implementation yet** and no card in the FreeBuff plan except “new strings go
  through localization” (START §1.10). This is the largest uncovered process requirement; Q-03 asks
  how to schedule it.
- TR-37 is partially blocked by the `AllowsTransparency` decision (Q-04).
- TR-32's secret-scan half is incomplete because `gitleaks` is not installed (R-07).
