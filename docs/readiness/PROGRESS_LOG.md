# Progress log

Only verified work is recorded here. Each entry: date, item, command/evidence, result.

## 2026-11-27 — Phase 1

| Step | Evidence | Result |
|---|---|---|
| Read the working protocol (`START` §1–§4) and all six plan documents; read v7 in full | `FPM_FreeBuff_0*.md`, `FPM_Master_Plan_v7.md` | Card list, laws, DoD and safety rules extracted (see `TRACEABILITY.md`) |
| Inspected repository state | `git rev-parse --abbrev-ref HEAD` → `main`; `git rev-parse --short HEAD` → `ccdbff4`; `git status --porcelain` → 0 | Clean tree at the U6 commit; 276 tracked files; no tags; not pushed |
| Recorded toolchain | `dotnet --list-sdks` → 8.0.428, 10.0.404; PowerShell 5.1.26100.10068; Git 2.56.0; Node 22.23.5; Bun 1.4.2 | Documented in `BASELINE.md`; `pwsh` absent, gitleaks absent, Inno not verified |
| Reproduced baseline gates | `dotnet build app/PCManager.sln … --no-restore` → 0 warnings/0 errors; `dotnet test` → 166 passed; `lint-xaml.ps1` → 380/10 files; `contrast-check.ps1` → OK | Baseline is green; recorded in `BASELINE.md` §3 |
| Checked required release artefacts | `ls` probes for LICENSE, THIRD_PARTY_NOTICES, CI workflows | Missing: license, notices, CI, community files (R2/R4 scope) |
| Located reference material | `reference/win11debloat`, `reference/universal-android-debloater` | Present and read-only; installer script `installer/FPM.iss` present |
| Skill inventory | account library list (50 enabled) | No WPF/.NET desktop skill exists; web/cloud skills dominate |
| Skill discovery | `npx skills find wpf`, `dotnet`, `windows desktop` | Only vendor/low-install skills; none suitable (details in `SKILLS.md` §4) |
| Authored two project skills | `docs/readiness/skills/fpm-wpf-verification.md`, `fpm-safety-contract.md` | Written |
| Attempted skill installation | `cloud_manage_skills save` ×2 | **Blocked:** “Your library is full at 50 skills.” Recorded in `SKILLS.md` §5 / R-06 |
| Sandbox-tested skill content | copied both files to `.tmp-skill-sandbox/`; resolved every path/command; scanned for destructive verbs; removed sandbox | 2 files, all paths and commands resolve, no destructive verbs, `Stop-Process` scoped to FPM only; sandbox removed |
| Wrote readiness artefacts | `docs/readiness/*.md`, `AGENTS.md` | README, baseline, skills, tools, decisions, risks, checklist, traceability, phase-2 plan |
| Phase 1 gate | This log + owner confirmation | **Pending owner** (Q-02 and Q-07) |

## 2026-11-27 — Phase 1 gate approved, R2 complete

| Step | Evidence | Result |
|---|---|---|
| Owner answers recorded | owner replies to Q-01…Q-06; Q-07 defaulted | D-015…D-021 written in `DECISIONS.md` |
| **Phase 1 gate (G-1)** | approval of the readiness pack | **approved** |
| R2 licence text | copied the GPL-3.0 text from the read-only UAD reference (`LICENSE`, 674 lines, verified header and tail; gnu.org was unreachable) | `LICENSE` added; “or later” stated in README/notices |
| R2 notices | `dotnet list package` on 2026-11-27; Win11Debloat `LICENSE` read from the reference | `THIRD_PARTY_NOTICES.md` lists the MIT notice verbatim, UAD GPL reference, platform-tools note and all 10 resolved packages |
| R2 community files | files written and staged | `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1), `SECURITY.md`, `.github/ISSUE_TEMPLATE/{bug,feature}.yml`, `PULL_REQUEST_TEMPLATE.md`, `dependabot.yml`, `CODEOWNERS` |
| R2 README disclaimer | patched the outdated “personal and educational” licence section | GPL-3.0-or-later, risk warning, non-affiliation, trademark attribution |
| R2 gate evidence | `dotnet build …` → 0 warnings/0 errors; `dotnet test` → 166 passed | docs-only card, gates re-run and green |

**Next card: R4** (CI workflow, secret scan tool, SDK pin).

## 2026-11-27 — R4 complete (CI + secret scanning + SDK pin)

| Step | Command / evidence | Result |
|---|---|---|
| Installed gitleaks 8.30.1 | release zip from `github.com/gitleaks/gitleaks` + `checksums.txt`; `sha256sum -c` reported **OK**; binary at `%LOCALAPPDATA%\FPM\tools\gitleaks\gitleaks.exe` | `gitleaks version` → **8.30.1** |
| Secret scan, full history | `gitleaks git . --redact --no-banner --verbose --log-opts=--all --report-format json` | **all commits scanned (78 at the R4 commit), no leaks found**, exit 0 |
| Committed scanner wrapper | `tools/secret-scan.ps1` (pinned 8.30.1, checksum-verified download, redacted output, exit code preserved) | CI and local run the identical command |
| Scanner fresh-install branch | temp `-ToolsRoot`, empty tools folder | downloaded, checksum verified, **installed under temp root: True**, scan clean |
| Installed actionlint 1.7.12 | release zip from `github.com/rhysd/actionlint` | `actionlint --version` → 1.7.12 |
| Workflow validation | `actionlint -no-color -oneline .github/workflows/ci.yml` | **exit 0**, no findings |
| CI job 1: restore | `dotnet restore app/PCManager.sln` | exit 0 |
| CI job 1: build | `dotnet build app/PCManager.sln -c Release --no-restore -nodeReuse:false -m:1 -warnaserror` | **Build succeeded, 0 warnings, 0 errors**, exit 0 |
| CI job 1: tests | `dotnet test app/tests/PCManager.Tests/PCManager.Tests.csproj -c Release --no-build -nodeReuse:false` | **166 passed**, 0 failed, 0 skipped, exit 0 |
| CI job 2: lint | `powershell … tools/lint-xaml.ps1` | OK: 380 literals / 10 files, none above baseline, exit 0 |
| CI job 2: contrast | `powershell … tools/contrast-check.ps1` | OK: every pair meets its ratio in both themes, exit 0 |
| CI job 2: icons | `dotnet run --project tools/IconGen -- --check` | “113 icons, 213 geometries, up to date”, exit 0 |
| CI job 3: secret scan | `powershell … tools/secret-scan.ps1` | gitleaks 8.30.1, 78 commits, **no leaks**, exit 0 |
| Working-tree scan (extra) | `gitleaks dir . --redact` | **2 findings**, both `jwt` matches inside `investigation.cany/evidence/…` — an ignored path (`.gitignore:92`), never committed (`git log --all -- investigation.cany` empty). Not a repository leak; see R-16. |
| SDK pin | `global.json` → 8.0.428, `rollForward: latestFeature`; `dotnet --version` in repo | **8.0.428**; restore, Release build (`-warnaserror`) and 166 tests re-run green under the pin |

Not executed locally, by nature: the `actions/checkout` and `actions/setup-dotnet` steps (GitHub-hosted actions). Every `run:` command of every job was executed here with the results above, and the workflow passes actionlint.

**R4 gate: met.** Next card: **W1** (window chrome + frame budget), per the approved order.

## 2026-11-27 — W1 implementation (frame gate blocked by the display)

| Step | Evidence | Result |
|---|---|---|
| Shared base | new `Views/FpmWindow.cs`: DWM rounded corners (Win11) with square fallback, dark system frame that follows the app theme, Alt+Space system menu, WM_NCHITTEST → `HTMAXBUTTON` for Snap Layouts | builds 0/0 |
| Layered window removed | `MainWindow` no longer sets `AllowsTransparency`; background is the solid `App.Background`; the clipped shell drop shadow is gone | composition no longer software-forced |
| Chrome metrics | title bar 40 px, caption buttons 46×36 with automation names `Minimise` / `Maximize or restore` / `Close` | verified by UI Automation |
| Windows on the base | `MainWindow` and `IconGalleryWindow`; splash and floating toolbar stay borderless by design | — |
| Window controls | UIA invoke: maximize → `IsZoomed=True` (700→1936 px), restore → `False`, minimise → `IsIconic=True` | pass |
| Nav automation names | were the CLR type name (`PCManager.ViewModels.NavigationItem`); now `Home … Settings` via the container style | defect found and fixed |
| 560 px overlap | “Start with Windows” and “Smart Boost” toggles overlapped their text at 560 px; cards now use two columns | defect found and fixed |
| Visual regression | Light/Dark at 560 and 1280; both themes render correctly, no clipping after the fix | reviewed |
| Frame trace | `PCMANAGER_FRAME_TRACE=1`, page switches: 9–14 frames per ~280 ms, worst frame 32.0 / 32.1 / 42.9 / 91.6 / 275.3 ms | **over the 16 ms budget** |
| Why | `Render tier 2; DWM composition on.` but `Win32_VideoController.CurrentRefreshRate = 40` (AMD Radeon, 1920×1080) | 25 ms per refresh: 16 ms is unreachable on this panel |
| Gates | build 0/0, 166 tests, lint **376** (baseline ratcheted), contrast OK, IconGen unchanged | pass |

**W1 is implemented and green except the frame-budget acceptance, which this 40 Hz display cannot evidence.**
**Q-08 answered by the owner (“continue”): accept the refresh-limited result.** W1 gate: met with that
caveat recorded (R-17); 16 ms must be re-measured on 60 Hz hardware before it is claimed anywhere.

## 2026-11-27 — W2 complete (follow the user across virtual desktops)

| Step | Evidence | Result |
|---|---|---|
| Documented API only | `Services/Desktop/VirtualDesktopManagerApi.cs`: COM `CLSID_VirtualDesktopManager`, `IsWindowOnCurrentVirtualDesktop`, `GetWindowDesktopId`, `MoveWindowToDesktop`; no `IVirtualDesktopPinnedApps` | COM instance created at runtime (log: “Virtual desktop tracking started”) |
| Follower logic | `Services/Desktop/VirtualDesktopFollower.cs`: skip when disabled/hidden/minimised, skip when already current, take the foreground window's desktop id, never guess when it is unknown, move without activating | 7 unit tests: move, already current, disabled, no handle, unknown id, failed move, dispose |
| 500 ms loop | `PCManager/Services/DesktopFollowLoop.cs`, UI-thread `DispatcherTimer`, one per followed window; logic stays WPF-free in Services | starts on `SourceInitialized`, disposed on `Closed` |
| Settings | `MainWindowFollowDesktops` (default **off**), `ToolbarFollowDesktops` (default **on**); “Virtual desktops” card in Settings explains the no-pin limitation | toggling “Toolbar follows me” wrote `ToolbarFollowDesktops: false` to `settings.json` |
| Toolbar wiring | `FloatingToolbarWindow` follows while `IsVisible` | wired |
| Gates | build 0/0, **173 tests** pass | pass |

Not checked: a real desktop switch (that would create a desktop on this machine) and Windows 10. The
move path is exercised by unit tests against the interface, and the COM activation is proven; the
switch itself is recorded as R-18 until it can be tried on a scratch desktop.

### W1 addendum — per-monitor/per-DPI placement (2026-11-27)

The card (02 §W1) does require “remember size/position per window type, per monitor/DPI; clamp to
visible area on restore”, and the first pass only remembered one set of values. Now implemented:

- `Core/Models/WindowBounds.cs`: `WindowBoundsTable.Key(device, dpi)`, `Resolve` (exact monitor+DPI,
  then the same monitor at another DPI, else nothing) and `Clamp` (work area, minimum size).
- `AppSettings.WindowBoundsByMonitor` persists a placement per monitor device and DPI; the legacy
  `MainWindow*` fields remain as the fallback for older settings files.
- `PCManager/Services/MonitorIdentity.cs` supplies the monitor device and effective DPI;
  `MainWindow` saves on close and restores before showing.
- 7 unit tests (`WindowBoundsTableTests`).
- Runtime: moved the window to 60,50 700×975 px, closed it; `settings.json` gained
  `\\.\DISPLAY1@120` → `left=48 top=40 w=560 h=780`; relaunch restored 60,45 700×975 px
  (same placement at 120 DPI).

## 2026-11-27 — W3 complete (toolbar survives closing the app)

| Behaviour | Evidence from a real launch | Result |
|---|---|---|
| Close to tray | main window closed with `WM_CLOSE`: `alive=True mainVisible=False`, visible windows `FPM Toolbar`; log “Main window closed to the tray; the toolbar keeps FPM running.” | pass |
| Restore toolbar | startup now shows the toolbar when the setting is on (a gap found by the first probe, fixed) | pass |
| Single-instance hand-off | second `FPM.exe`: `exited=True exitCode=0`; first instance `mainVisible=True`; log “Single-instance hand-off received; showing FPM instead of starting a second process.” | pass |
| `--toolbar-only` | `alive=True mainHandle=0`, visible window `FPM Toolbar`; log “Toolbar-only mode started (requested on the command line)”; working set 124.6 MB, private 50.2 MB | pass |
| Hand-off from toolbar-only | normal launch while toolbar-only ran: launcher exited, toolbar-only exited, a new process appeared with a main window; log “Hand-off: starting the full UI and ending toolbar-only mode.” | pass |
| Toolbar × | sets `ShowFloatingToolbar=false` and hides (code path, not clicked in this probe) | not exercised |
| Exit dialog / tray menu | `ExitDialog` with three choices and “Remember my choice”; `AppLifetimePolicy` unit-tested (8 tests) | dialog not clicked headlessly |
| Sign-in mode | `AppStartupService.SetEnabled(enabled, toolbarOnly)` writes `"exe" --toolbar-only` when the toolbar is on | registry write not exercised |

Followed by a build 0/0 and **188 tests** passing. Remaining gaps are listed in `RISKS.md` (R-19, R-20)
and were not implied as passed.

## 2026-11-27 — W4 complete (toolbar edge docking)

| Step | Evidence | Result |
|---|---|---|
| Geometry rules | `Core/Models/ToolbarDock.cs`: 24 px snap threshold, 32 px strip, centred tab, expanded drawer clamped to the work area | 6 unit tests |
| Docked tab | restored with `ToolbarDock=Right`: window rect **40×210 px** at x=1880 on a 1920-wide screen (32×168 DIP at 120 DPI), flush right; log “Toolbar restored docked to the right edge.” | pass |
| Hover expansion | pointer over the tab: **442×105 px** at x=1438 with the full tool drawer (Shot / Note / Calc / Task / Open) | pass |
| Auto-collapse | pointer away for 3 s: back to **40×210 px** at x=1880 | pass |
| Pin / opacity / topmost / reset / hide | pin persists in settings and blocks auto-collapse; context menu with opacity slider (60–100 %) and presets, always-on-top toggle, reset, hide | menu items not clicked headlessly |
| Drag away | dragging the expanded toolbar away from the edge calls `Undock()` and keeps the new floating position (code path) | gesture not simulated |
| Never steals focus | `FloatingToolbarHost` no longer calls `Activate()`; window has `ShowActivated=False` | pass by inspection |
| Fullscreen hiding | 1 s poll of `SHQueryUserNotificationState`; hides on busy / D3D fullscreen / presentation mode and restores afterwards | not exercised |
| Persistence | dock edge, pin, opacity, topmost and floating position stored per monitor + DPI (`ToolbarBoundsByMonitor`) | save path wired |
| Gates | build 0/0, **194 tests**, lint 376, contrast OK | pass |

Next card: **W5** (Boost inside the toolbar).

## Earlier entry

## Carried state from the last code cards

- U5 splash committed; U6 page caching/transitions committed as `ccdbff4`.
- Last verified gates: build 0/0, 166 tests, lint 380, contrast OK.
- Frame trace after U6: 11–12 frames per 280 ms window, worst frame 32–45 ms — the multi-second
  page stalls are fixed; 60 fps remains blocked by the layered-window decision (Q-04).
- Settings restored to Light, `ReduceMotion=false`.

## Blockers

| Blocker | Impact | Resolution |
|---|---|---|
| Skill library full (50/50) | The two skills cannot be installed into the account library | Owner frees two slots (Q-02) or accepts repo-only skills |
| No `gitleaks`/`trufflehog` | Secret scan cannot be evidenced for R4 | Install before W0 gate |
| Only 125 % DPI verified | UI scaling claims at 100/150/200 % unproven | Verify in W1/U10 |

## Next step

Owner answers Q-01…Q-07; then the Phase 1 gate is marked approved and Wave 1 starts with **U7**
(boost animation), per the default order in `PHASE2_PLAN.md`.
