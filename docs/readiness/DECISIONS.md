# Decision log

Format: `ID | date | decision | rationale | status | owner`. Only the owner may reverse an
accepted decision marked **owner**.

| ID | Date | Decision | Rationale | Status |
|---|---|---|---|---|
| D-001 | 2026-11-23 | Product identity **FPM** everywhere user-facing; namespaces/projects stay `PCManager.*`. | Plan v7 §3, §5.1; avoids a risky rename. | accepted (owner) |
| D-002 | 2026-11-23 | No `Wpf.Ui` package; hand-written Fluent XAML and tokens. | v7 §3; keeps styling under our control and the contrast gate honest. | accepted |
| D-003 | 2026-11-23 | License **GPL-3.0-or-later**; keep upstream notices (UAD GPL-3.0, Win11Debloat MIT). | Android lists derive from a GPL-3.0 project; GPL can include MIT. | accepted (owner) |
| D-004 | 2026-11-23 | No Rust; re-implement Android debloat natively in C#/WPF. | One toolchain, one UI, one installer. | accepted (owner) |
| D-005 | 2026-11-23 | Never run or bundle the Win11Debloat script; port its catalog to typed tweaks with undo. | Safety law L5/L3 and no remote execution. | accepted (owner) |
| D-006 | 2026-11-23 | Do not bundle Google platform-tools; consent + Google Authenticode check + `%LOCALAPPDATA%\FPM\tools`. | Redistribution restriction. | accepted (owner) |
| D-007 | 2026-11-23 | One `IMotionPolicy` (Windows setting + app setting + battery + RDP + Low Resource Mode + visibility). | U4; consistent, testable reduced motion. | accepted |
| D-008 | 2026-11-24 | Icons are generated geometry (`Icon3D`) with a runtime layered compositor; no raster packs or runtime downloads. | U2; crisp at every DPI, theme-aware, small. | accepted |
| D-009 | 2026-11-24 | Lint is a ratchet baseline, not a mass literal rewrite. | U1; keeps cards reviewable while the count falls to zero. | accepted |
| D-010 | 2026-11-25 | Splash uses staged startup (`OnExplicitShutdown` until the shell shows) and `--no-splash`. | U5; lets the splash close independently without ending the app. | accepted |
| D-011 | 2026-11-27 | Pages are cached visuals, pre-warmed at idle, with revisit reloads deferred 260 ms. | U6; removes measured multi-second page-swap stalls. | accepted |
| D-012 | 2026-11-27 | Phase 1 readiness lives in `docs/readiness/` (tracked); `AGENT_STATE.md` stays ignored. | One source of truth that reviewers can see, without publishing private agent state. | accepted |
| D-013 | 2026-11-27 | Two project skills are authored (`fpm-wpf-verification`, `fpm-safety-contract`) and kept in `docs/readiness/skills/` because the account library is full. | Skill install is blocked at 50/50; the content is still available to the agent. | provisional — owner to free a slot or accept repo-only |
| D-014 | 2026-11-27 | Commit after every green card on `main`; no branches or history rewrites without the owner. | Matches START §1.7; keeps the history reviewable. | accepted |

| D-015 | 2026-11-27 | Phase 1 readiness pack approved as written. | Owner confirmed the baseline, scope, decisions, risks, checklist and plan. | accepted (owner) |
| D-016 | 2026-11-27 | Execution order: **R2 → R4 → W1 → U7–U12**, then windows/toolbar/sensors, debloat and release. | CI and the frame-budget fix guard the later cards. | accepted (owner) |
| D-017 | 2026-11-27 | The two FPM skills stay repo-only (`docs/readiness/skills/`). | Account library is full; repo copies are read directly by the agent. | accepted (owner) |
| D-018 | 2026-11-27 | Localization: build the `.resx` foundation at **U10**, then convert strings progressively. | Avoids a single risky sweep; new strings become keys from U10 onward. | accepted (owner) |
| D-019 | 2026-11-27 | W1 may replace `AllowsTransparency` with DWM rounded corners after visual regression captures. | Needed for the 16 ms frame budget; keeps the chrome appearance checked. | accepted (owner) |
| D-020 | 2026-11-27 | Commits stay local; push and signing are decided at R4. | No CI or notices were in place before R2. | accepted (owner) |
| D-021 | 2026-11-27 | Accept `REA_REFERENCE_AUDIT.md` as the parity gate; per-card reports + phase-gate reviews. | Existing audit is sufficient; no re-run requested. | accepted by default (Q-07 unanswered) |
| D-022 | 2026-11-27 | **Pin the SDK** with `global.json` → `8.0.428`, `rollForward: latestFeature`. | The product targets net8.0; the pin removes the SDK-10-vs-8 ambiguity. Re-verified: `dotnet --version` = 8.0.428, Release build with `-warnaserror` green (0/0) and all 166 tests pass under the pin. | accepted |
| D-023 | 2026-11-27 | CI secret scanning runs the committed `tools/secret-scan.ps1` (gitleaks 8.30.1, checksum-verified) instead of a marketplace action, on `windows-latest`. | Every CI command must be executable and verifiable locally; the script is identical in both places and pins the scanner version. | accepted |
| D-024 | 2026-11-27 | CI builds **Release** with `-warnaserror` and runs tests `--no-build` in Release. | Stronger than the local Debug habit and already proven green locally. | accepted |
| D-025 | 2026-11-27 | Drop `AllowsTransparency` and use DWM rounded corners (`DWMWA_WINDOW_CORNER_PREFERENCE`) with a square fallback. | Layered windows force software composition and cap frame pacing; the plan already allowed the square-corner fallback on Windows 10. | accepted (W1) |
| D-026 | 2026-11-27 | Chrome metrics: 40 px title bar, caption buttons 46×36, 6 px resize border. | The plan's Windows 11 reference metrics; verified by UI Automation. | accepted (W1) |
| D-027 | 2026-11-27 | Treat the 16 ms frame budget as **unmeasurable on the current 40 Hz panel**, and report measured intervals against the refresh period instead of silently failing or claiming a pass. | One refresh is 25 ms here, so a 16 ms frame cannot occur; faking a pass would violate the evidence rule. | accepted (Q-08 answered) |
| D-028 | 2026-11-27 | W1 frame gate = “no missed refresh beyond one frame on the available hardware”; the 16 ms figure is re-measured on 60 Hz hardware before any claim. | The owner answered “continue” to Q-08; this keeps the card shippable without overstating evidence. | accepted |
| D-030 | 2026-11-27 | W1 placement is stored per monitor device + effective DPI (`WindowBoundsByMonitor`), with legacy `MainWindow*` values as fallback; clamping to the work area stays in a pure, tested helper. | 02 §W1 requires per monitor/DPI; the pure helper keeps the rule testable and the Win32 lookup thin. | accepted |
| D-031 | 2026-11-27 | Closing the main window hides to tray only when `CloseToTray` **and** the toolbar is enabled; otherwise the process exits. The tray Exit question is skipped when there is no toolbar to keep. | Matches 02 §W3, and avoids a process that keeps running with nothing visible. | accepted |
| D-032 | 2026-11-27 | Hand-off releases the mutex and closes the pipe **before** spawning the next process; toolbar-only hands over by starting a normal `FPM.exe --no-splash` and exiting. | Prevents the new process from forwarding into a dying instance or starting a duplicate toolbar. | accepted |
| D-045 | 2026-11-27 | The debloat catalog is embedded in `PCManager.Core` and validated at load; `tools/debloat_catalog_gen.py` regenerates it from the read-only reference. | Tests and the future helper share one immutable, versioned source without shipping reference files. | accepted |
| D-046 | 2026-11-27 | Reference features that need `HKEY_CLASSES_ROOT`, a service-SID `HKU` hive, Store-DB edits, forced Edge removal or Start-layout templates are excluded with a written reason. | D1's hive/path allow-list is a safety boundary; weakening it to reach parity would defeat the contract. | accepted |
| D-047 | 2026-11-27 | `Microsoft.WindowsStore` is removed from the app catalog even though the reference lists it as “unsafe”. | FPM's denylist says the Store is never offered for removal, regardless of reference metadata. | accepted |
| D-043 | 2026-11-27 | T2 ships the unprivileged temperature path now (NVML + ACPI zone, typed states, shared sampler, UI); LibreHardwareMonitorLib in the elevated helper is deferred pending the owner's decision. | LHM brings a kernel-mode driver, a UAC prompt and a Defender/AV check that cannot be verified unattended; the owner must approve that dependency. | open — owner |
| D-044 | 2026-11-27 | Temperature unit is stored in `AppSettings.TemperatureUnit` and conversion/formatting stay in `TemperatureText`; the sampler always stores Celsius. | One representation, one formatting place, so Home/toolbar/Settings cannot disagree. | accepted |
| D-040 | 2026-11-27 | Battery data comes from `GetSystemPowerStatus` plus `root\WMI` (`BatteryStatus` rate, `BatteryStaticData`/`BatteryFullChargedCapacity`), with capacities cached; no SetupAPI battery IOCTL in T1. | The WMI classes expose the same driver rate/capacity without a second native surface; the IOCTL path would add untestable P/Invoke for no new data. | accepted |
| D-041 | 2026-11-27 | The battery tile hides itself when `HasBattery` is false; Settings always shows the tile so “No battery detected” is still visible with its reason. | T1 requires a hidden desktop tile **and** a truthful Settings message; hiding everywhere would lose the explanation. | accepted |
| D-042 | 2026-11-27 | Visibility demand is a refcounted owner set (`main`, `toolbar`) in `BatteryStatusViewModel`; any visible owner runs the 5 s cadence, none runs 30 s. | Matches T1's shared-cadence rule without each surface starting its own timer or polling. | accepted |
| D-037 | 2026-11-27 | One `BoostCoordinator` owns the Boost run for Home and the toolbar; the shared temporary-file rule is “only an explicit manual Boost may delete files, Smart/Automatic Boost trims working sets only”. | W5 asks for the same state machine and confirmation rule; unattended deletion would be the wrong default and a modal dialog would block toolbar-only use. | accepted |
| D-038 | 2026-11-27 | `BoostAnimationHost` is one control with an `IsCompact` scale (0.6×) instead of two animation implementations; reduced motion keeps only fades. | Avoids duplicated animation logic and satisfies W5's compact-scale requirement. | accepted |
| D-039 | 2026-11-27 | The result chip count-up starts from either the phase or the byte binding, whichever arrives last. | WPF binding order is not guaranteed; the first run proved the chip could freeze at “0 B freed”. | accepted |
| D-034 | 2026-11-27 | W4 docking is a 32 px edge tab that expands on hover/click into the full drawer, auto-collapses 1.5 s after the pointer leaves, and stays open when pinned. | The plan's 24–32 px strip and 150 ms expansion; keeping the strip separate avoids a second always-visible chrome. | accepted |
| D-035 | 2026-11-27 | Fullscreen hiding uses `SHQueryUserNotificationState` (busy, D3D fullscreen, presentation mode) polled once a second; the toolbar is hidden, not disabled. | Documented API, cheap poll, and the setting stays on so it returns by itself. | accepted |
| D-036 | 2026-11-27 | Toolbar floating position is stored per monitor + DPI alongside the dock edge and pin state; dragging the expanded toolbar away from an edge undocks it. | W4 asks for per monitor/DPI persistence; reuse of `WindowBoundsTable` keeps the rule tested. | accepted |
| D-033 | 2026-11-27 | Sign-in writes `"FPM.exe" --toolbar-only` when the toolbar is left on, via an optional `SetEnabled(enabled, toolbarOnly)` overload. | 02 §W3 asks for a toolbar-only sign-in; the overload keeps existing callers and fakes unchanged. | accepted |
| D-029 | 2026-11-27 | W2 uses only the documented `IVirtualDesktopManager` (follow, not pin); toolbar follows by default, main window does not; polling stops when the window is hidden or minimised. | There is no documented pin-to-all-desktops API, and the plan forbids the undocumented one without guards. | accepted |

## Pending owner decisions

| ID | Question | Recommendation |
|---|---|---|
| Q-01 | Continue UI cards U7–U12 first, or switch to release foundations (R2 license/notices, R4 CI) now? | Finish U7–U9 (boost, animation polish, scrolling), then R2+R4 before W cards, so CI guards the rest. |
| Q-02 | Free a slot in the 50-skill library for the two FPM skills, or accept them as repo files? | Free two unused slots; keep repo copies as documentation. |
| Q-03 | Localization: build the `.resx` foundation now, or defer all strings to one later sweep? | Build the foundation at the start of U10 so new strings land as keys. |
| Q-04 | Approve changing `MainWindow` away from `AllowsTransparency` (DWM rounded corners) to reach the 16 ms frame budget? | Approve in W1, with visual regression checks first. |
| Q-05 | Is pushing to GitHub authorised now, and who signs releases? | Keep local commits; authorise push when R2/R4 are green. |
| Q-06 | Re-run the REA parity audit or accept the existing `REA_REFERENCE_AUDIT.md`? | Accept the existing audit unless parity claims are challenged. |
| Q-07 | Confirm the review cadence (per-card report + phase gates) and the approver. | Owner approves; per-card reports in `PROGRESS_LOG.md`. |

## D2 decisions

| ID | Date | Decision | Rationale | Status |
|---|---|---|---|---|
| D-048 | 2026-11-27 | The elevated helper gets exactly three typed debloat commands (`ApplyTweak`, `RevertTweak`, `DetectTweak`); the client sends only a catalog tweak id plus, for revert, captured pre-state values. | Keeps the helper's attack surface catalog-scoped; no script, path or command string crosses the pipe. | accepted |
| D-049 | 2026-11-27 | A restore point is attempted for non-Low-risk plans, but its absence is recorded as `Skipped: reason` and does not block the run; the `.reg` backup and journal are the primary safety net. | Windows often disables restore points; blocking debloat on it would make the feature unusable, while claiming success would be dishonest. | accepted |
| D-050 | 2026-11-27 | Journal and `.reg` backups live under `%LOCALAPPDATA%\FPM\debloat\{journal,backups}`, not in the repo or Program Files. | Per-user state survives upgrades, needs no elevation to read, and never publishes registry contents. | accepted |
| D-051 | 2026-11-27 | D2's executor supports registry values and optional features only; key deletion, Appx removal and other-user hives are refused by the planner until they have typed snapshots. | Undo must be exact; a key or package cannot yet be journaled faithfully, so the engine must not pretend it can. | accepted |
| D-052 | 2026-11-27 | Detect runs for every selected tweak before the first write, and an already-applied tweak is skipped rather than re-applied. | Prevents the journal from recording a post-change “pre-state” and avoids duplicate no-op writes. | accepted |

## D3 decisions

| ID | Date | Decision | Rationale | Status |
|---|---|---|---|---|
| D-053 | 2026-11-28 | The five Windows-tab presets are documented rules over catalog category and risk, not hidden reference presets; Custom means the user's own selection. | The catalog has no tweak presets, and inventing opaque defaults would be worse than showing the rule. | accepted |
| D-054 | 2026-11-28 | The Apps tab is a searchable catalog surface only; it does not offer removal or claim live installed detection. | Appx is refused by the planner/helper until package state can be journaled (D-051); a disabled removal button would be a fake affordance. | accepted |
| D-055 | 2026-11-28 | Review shows the typed target changes, not per-value old→new diffs; current values are captured by the helper immediately before the write. | Old values would require a protected read per tweak before review; that noise is deferred until the helper can batch reads. | accepted |
| D-056 | 2026-11-28 | History shows the latest 10 journal runs, while Undo remains the engine's latest-run reverse replay. | The journal is per-run; undoing an arbitrary older run safely needs an engine API and dependency-aware replay. | accepted |
| D-057 | 2026-11-28 | Detect and Apply stay on the existing one-id-per-call helper contract; the page explains the administrator prompt but does not hide how many may appear. | No batched helper protocol exists yet; changing it would be a protocol extension, not a page change. | accepted |
| D-058 | 2026-11-28 | The review's “Create restore point first” toggle is passed to the engine and recorded as `Skipped: turned off for this run` when off. | The user's choice must be visible in the journal/report rather than silently ignored. | accepted |

## D4 decisions

| ID | Date | Decision | Rationale | Status |
|---|---|---|---|---|
| D-059 | 2026-11-28 | The only user-target value crossing the helper pipe is a typed token (`current`, `default`, `sid:<sid>`, `user:<name>`); the helper resolves it against its own ProfileList. Paths and hive files never cross the pipe. | Keeps the elevated surface catalog-scoped and prevents arbitrary hive loading. | accepted |
| D-060 | 2026-11-28 | A signed-in other profile is addressed through its live `HKU\<sid>` hive; only a profile whose hive is not loaded gets `HKU\FPM_<id>` and an offline `NTUSER.DAT` mount. | Avoids loading an in-use hive and follows D4's “live hive unless logged in” rule. | accepted |
| D-061 | 2026-11-28 | The Default-user target is `HKU\FPM_Default`, requires Windows 10 build 10240+, and always shows a SYSPREP warning. | The Default hive changes every future account; it must be deliberate and version-gated. | accepted |
| D-062 | 2026-11-28 | `fpm-debloat-preset.json` v1 contains only tweak ids and app ids; unknown ids are ignored with a warning and the file is never interpreted as commands or paths. | Makes presets shareable across catalog versions without widening the execution surface. | accepted |
| D-063 | 2026-11-28 | `--apps` is accepted by the parser but the CLI refuses app execution until Appx package state can be journaled; app ids are still valid in preset files. | Consistent with D-051: no irreversible package removal without an exact undo record. | accepted |
| D-064 | 2026-11-28 | Drift checking is a manual, report-only page action using the journal target; it never reapplies a tweak. | The D4 scheduled check is optional; reapplying silently would violate the safety contract. | accepted |
| D-065 | 2026-11-28 | A target is resolved and its hive loaded/unloaded per helper command, so an other-user run may prompt once per tweak and target resolution happens again helper-side. | Reuses the existing one-shot helper protocol; batching is a later protocol change, not a D4 fork. | accepted |

## D5 decisions

| ID | Date | Decision | Rationale | Status |
|---|---|---|---|---|
| D-066 | 2026-11-28 | `docs/freebuff/`, `AGENT_STATE.md`, root plan/prompt/handoff files and the reference tree are intentionally local-only and stay in `.gitignore`; `docs/readiness/` is the tracked single source of truth for status, decisions, risks and evidence. | `FPM_FreeBuff_00_START_HERE.md` says the six plan copies are git-ignored by design, and `REPO_AUDIT.md` records the same policy. The readiness folder remains visible to Git, so no required deliverable is hidden by accident. | accepted |
| D-067 | 2026-11-28 | The planner refuses contradictory selected tweaks with `SkippedConflict` before elevation: same registry target with different final data, or the same optional feature both enabled and disabled. Presets still select every matching tweak, and the plan names both alternatives. | The reference contains mutually exclusive choices (Spotlight, search-box styles, taskbar modes, Start layouts, drive letters); silently applying catalog order would make the last write win and leave a misleading journal. | accepted |
| D-068 | 2026-11-28 | `tools/debloat_catalog_gen.py` records every dynamically excluded reference feature in `catalog.excludedFeatures`, not only the hand-written `EXCLUDED` map; the catalog now carries all 16 exclusions. | D1's map already said 16, but the embedded catalog still had 11 because five dynamic exclusions never reached the output. The D5 parity test now compares the map and catalog sets. | accepted |

## A1 decisions

| ID | Date | Decision | Rationale | Status |
|---|---|---|---|---|
| D-069 | 2026-11-28 | A1 offers the pinned **Google platform-tools 35.0.2** archive (`platform-tools_r35.0.2-win.zip`, SHA-256 `2975a3ea…300bb9`) rather than the mutable `platform-tools-latest-windows.zip` URL used as an example in the card. The URL, version, byte count and checksum are constants in `Core/Android/AdbPackage.cs`. | A checksum is only meaningful for an immutable package. Pinning makes consent, checksum validation and the Authenticode gate reproducible; the archive was verified against Google's URL on 2026-11-28. No platform-tools file is bundled or committed. | accepted |
| D-070 | 2026-11-28 | A1's ADB command surface is a closed enum: `StartServer`, `KillServer`, `Devices`, `GetProperties`, `ListUsers`. Package read/apply commands are not reachable until A2/A3 add them to the typed surface. | The card is an onboarding/read-only identity card. A closed command set means no free-form shell or package mutation can leak through the A1 service while later cards extend it deliberately. | accepted |
| D-071 | 2026-11-28 | The locator checks the configured path, then `%LOCALAPPDATA%\FPM\tools\platform-tools`, then common Android SDK locations (`ANDROID_HOME`, `ANDROID_SDK_ROOT`, `%LOCALAPPDATA%\Android\Sdk`, `%ProgramFiles%\Android`), then `PATH`. An unsigned candidate does not prevent a later signed candidate from being selected. | The owner explicitly asked for common install locations; keeping them after the FPM-owned copy preserves the card's order while avoiding a false “unverified” result when a later signed adb exists. The runtime probe found the real SDK copy at `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe` this way. | accepted |
| D-072 | 2026-11-28 | Server ownership is inferred only from the `adb start-server` output (`daemon not running` / `daemon started successfully`); FPM runs `kill-server` on shutdown only when that output says FPM started it. | ADB has no ownership token. The output is the only documented signal, and the rule avoids stopping a server started by another developer tool. Unit tests cover both branches; the runtime log shows FPM started and stopped its own server. | accepted |
| D-073 | 2026-11-28 | The Authenticode verifier requires a valid Google signer (`O=Google LLC`) and a buildable chain; revocation is not checked online. | Revocation checks need network access and can fail offline; the pinned SHA-256 already authenticates the transport, while the signature prevents running an arbitrary local `adb.exe`. The limitation is recorded as R-42. | accepted |
| D-074 | 2026-11-28 | The Debloat → Android tab in A1 is read-only identity/onboarding; it does not expose package scanning, removal, disabling or restore actions. | A2–A5 own those operations. Showing disabled package controls now would be a fake affordance and would imply a safety path that does not exist yet. | accepted |

## A2 decisions

| ID | Date | Decision | Rationale | Status |
|---|---|---|---|---|
| D-075 | 2026-11-28 | A2 embeds the UAD list from reference commit `11f27c671cba278d71296cdef4c5a5dba06add5e` exactly as supplied: 2,400 source rows, 2,394 unique package ids, 6 duplicate ids. Duplicates are normalized **last-wins** and the count is surfaced in the catalog rather than silently discarded. | The upstream loader inserts into a `HashMap`, so last-wins is the reference behaviour. Rejecting the official file would make the embedded fallback unavailable; hiding duplicates would make updates look cleaner than they are. | accepted |
| D-076 | 2026-11-28 | A2 accepts the bare `android` id and dotted package ids in the data schema (`^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*$`); it still rejects empty/leading-digit ids and unknown list/removal values. | The reference includes `android`, a real package id. A3's action validator can remain stricter for mutations without making the read-only catalog reject valid upstream metadata. | accepted |
| D-077 | 2026-11-28 | The upstream list is cached at `%LOCALAPPDATA%\FPM\android\lists\uad_lists.json` with a `.sha256` sidecar; the embedded list is the fallback whenever the cache is absent or invalid. A remote response is never written until the same schema validator accepts it, and it replaces the cache through a temporary file plus move. | This gives a real user-controlled update path without making startup depend on GitHub, and prevents a malformed update from destroying the last good cache. | accepted |
| D-078 | 2026-11-28 | A2's Android-tab surface is limited to the list status, embedded/cached source, counts, diff and **Check for list updates**; package rows, filters, actions and review stay in A3/A4. | A2 owns data/provenance, not package operations. The small card makes the user-initiated update reachable without pretending A3/A4 already exist. | accepted |
| D-079 | 2026-11-28 | The remote endpoint is fixed to UAD's official raw GitHub URL and fetched with a 30 s timeout and 4,000,000-byte bound; the response is parsed as data only and is never executed. | The list is untrusted community metadata. A bounded read plus schema validation limits resource and injection risk; A3's denylist remains the execution gate. | accepted |

## A3 decisions

| ID | Date | Decision | Rationale | Status |
|---|---|---|---|---|
| D-080 | 2026-11-28 | A3 extends the closed command surface with `ListPackages`, `UninstallPackage`, `DisablePackage`, `EnablePackage` and `RestorePackage` only. Package ids must match a dotted `^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)+$`; user ids are non-negative integers; every argument uses `ArgumentList`. | The card requires typed ADB only. Making the data schema accept `android` does not make it a valid mutation target; the stricter action regex prevents shell text and accidental framework removal. | accepted |
| D-081 | 2026-11-28 | State is computed from four `pm list packages` queries (all incl. uninstalled, system, enabled, disabled) and joined to UAD metadata. A package absent from enabled and disabled but present in all is `Uninstalled`; a package absent from system is not marked system. | This mirrors UAD's `fetch_packages` and avoids inferring uninstalled state from a failed command. Tests use recorded text with no device. | accepted |
| D-082 | 2026-11-28 | Batches run sequentially, return one typed result per request, continue after an ordinary failure, and stop only on a classified device disconnect. `Already` and denylist blocks do not run a mutating command. After any successful action the engine re-queries all state and records verified `After` states. | The safety contract requires per-item isolation and a truthful verify step. A command exit code alone is not proof that the phone reached the requested state. | accepted |
| D-083 | 2026-11-28 | The critical-package denylist is static, and every UAD `Unsafe` row is also treated as protected. A denylisted action is rejected unless `ExpertMode` is set and `ExpertConfirmation` exactly equals the package id; even then the batch logs the warning. | A4 will own the typed confirmation UI. Keeping the gate in the engine means a UI bug cannot bypass it by constructing a request directly. | accepted |
| D-084 | 2026-11-28 | The .NET 11 `System.*` and `Microsoft.Extensions.*` package updates introduced by the background dependency merge were reverted to the last .NET 8-compatible versions; no `SuppressTfmSupportBuildWarnings` was added. | The .NET 8 build gate must stay at 0 warnings. Suppressing TFM warnings would hide a real unsupported runtime combination and violate the project's no-suppression rule. | accepted |
