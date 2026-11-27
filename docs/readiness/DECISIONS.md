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
| D-027 | 2026-11-27 | Treat the 16 ms frame budget as **unmeasurable on the current 40 Hz panel**, and report measured intervals against the refresh period instead of silently failing or claiming a pass. | One refresh is 25 ms here, so a 16 ms frame cannot occur; faking a pass would violate the evidence rule. | pending owner (Q-08) |

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
