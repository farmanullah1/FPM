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
| TR-20 | Debloat advanced: other user, sysprep, CLI, presets (D4) | D4 | helper hive load/unload, CLI | hive cleanup tests; dry-run writes nothing |
| TR-21 | Debloat tests incl. reference parity (D5) | D5 | `PCManager.Tests` | parity map + green tests |
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
| TR-45 | One chrome base: rounded corners, dark frame, Snap Layouts, Alt+Space (W1) | W1 | `Views/FpmWindow.cs`, `MainWindow.xaml`, `IconGalleryWindow.xaml` | build 0/0; max/restore/minimise verified by UIA; Light+Dark at 560/1280 |
| TR-46 | Every window control reachable by keyboard/automation (W1) | W1 | `Styles/Controls.xaml` (`NavItemStyle`), title-bar buttons | UIA names: `Home…Settings`, `Minimise`, `Maximize or restore`, `Close` |
| TR-47 | Frame pacing within the 16 ms budget (W1, U4) | W1 | `Motion/FrameTimingProbe.cs` | **blocked**: 40 Hz panel (25 ms refresh); trace recorded, 16 ms not evidenceable — Q-08 |
| TR-44 | Release workflow (tag → installer + checksums) | R4 (release half), R3 | `.github/workflows/release.yml` | deferred: needs the owner's push/signing decision; blocker recorded in `RISKS.md` |

## Coverage notes

- TR-35 has **no implementation yet** and no card in the FreeBuff plan except “new strings go
  through localization” (START §1.10). This is the largest uncovered process requirement; Q-03 asks
  how to schedule it.
- TR-37 is partially blocked by the `AllowsTransparency` decision (Q-04).
- TR-32's secret-scan half is incomplete because `gitleaks` is not installed (R-07).
