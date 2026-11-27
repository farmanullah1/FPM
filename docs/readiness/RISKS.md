# Risk register and open questions

Scoring: likelihood (L) and impact (I) are Low/Medium/High. Every risk has an owner and a
mitigation that is actionable inside the plan.

| ID | Risk | L | I | Mitigation | Owner | Status |
|---|---|---|---|---|---|---|
| R-01 | **Safety regression** in cleanup/debloat (deleting the wrong file, unvalidated helper input). | M | H | Typed actions only; SafetyGuard before every delete; denylist; tests with fakes; `fpm-safety-contract` checklist on every such card. | Agent | open |
| R-02 | **Frame budget** stuck at ~30 fps because the main window uses `AllowsTransparency`, blocking U7/U8 quality. | H | M | Fix in W1 (DWM rounded corners) after visual regression capture; keep the frame probe as a gate. | Owner decision Q-04 | open |
| R-03 | **Localization debt**: every string is a literal; each new card adds more. | H | M | Build the `.resx` foundation before U10/W/D UI cards (Q-03); add a test that no new literal lands without a key. | Owner decision | open |
| R-04 | **Responsive defects** at 360/560 (overlap, clipped labels) remain while later cards add layout. | H | M | U10 immediately after U9; capture 360/560/1280 in each UI card report. | Agent | open |
| R-05 | **Scope explosion**: v7 has ~200 requirements, FreeBuff adds ~40 cards; context and time are finite. | H | H | One card per session; phase gates; single source of truth; stop and hand off at 70 % context with `AGENT_STATE.md`. | Both | open |
| R-06 | **Skill library full (50/50)** blocks installing the two FPM skills. | H | L | Repo copies exist (D-013); owner frees slots (Q-02). | Owner | blocked |
| R-07 | **Secret or proprietary file leaks** into the public repo. | L | H | Closed for the current content: gitleaks 8.30.1 scanned all 77 commits (no leaks) and CI runs the same pinned script on every push/PR. Keep R1 audit current. | Agent/Owner | **closed (R4)** |
| R-08 | **Installer/upgrade breakage** or data loss from Inno Setup changes. | M | H | R3 test matrix (clean 10/11, upgrade, silent, uninstall, coexistence); never delete user data by default. | Agent | open |
| R-09 | **Android/ADB misuse** (wrong package, bootloop) or bundling platform-tools. | M | H | Denylist + Expert typed confirmation; snapshot before apply; consent + signature check for adb. | Agent | open |
| R-10 | **Third-party licence incompatibility** (UAD GPL, LHM MPL, Syncfusion-like assets). | M | H | R2 notices; licence review before adding a package; no vendor icon packs. | Owner | open |
| R-11 | **WMI/registry/sensor reads fail or block** on the owner's machine, producing slow or missing data. | H | M | Typed status `Unavailable` + reason; timeouts; no fabrication; tests with recorded fixtures. | Agent | open |
| R-12 | **Proprietary Microsoft material** in history or assets (installer, REA screenshots). | M | H | R1 audit; history check; owner approval before any history rewrite; never publish those files. | Owner | open |
| R-13 | **Verification theatre**: claims without fresh output. | M | M | `verification-before-completion` + `fpm-wpf-verification`; evidence in `PROGRESS_LOG.md`; state what was not checked. | Agent | open |
| R-14 | **Toolchain drift** (SDK 10 building net8.0 projects; future SDK removal). | L | M | `global.json` pins 8.0.428 with `latestFeature`; CI installs 8.0.x; build/tests re-verified under the pin. Re-check when the SDK is removed. | Agent | mitigated (R4) |
| R-17 | **The frame budget cannot be evidenced on the available display.** The panel runs at 40 Hz (25 ms per refresh), so the 16 ms criterion is physically unreachable and traces show 32–275 ms worst frames. | H | L | Gate accepted as refresh-limited (D-028); re-measure on 60 Hz hardware before claiming 16 ms. | Owner/Agent | mitigated |
| R-19 | **Toolbar-only is not as light as “lightweight” implies.** Measured working set 124.6 MB (private 50.2 MB) because the WPF shell assemblies and theme dictionaries are loaded. | M | M | Accept for now (the card only asks for the budget to be recorded); consider trimming startup services or lazy page assembly if the owner wants a lower footprint. | Agent | open |
| R-20 | **Tray menu and exit dialog were not clicked in automation.** Their rules are unit-tested and the paths are wired, but the WinForms menu and the modal dialog were not exercised end to end. | M | L | Click through once on a desktop session: Exit → keep toolbar, and toolbar × turning the setting off. | Agent | open |
| R-18 | **Desktop following is unproven against a real desktop switch.** The COM activation and decision logic are tested, but creating a desktop on the user's machine was not done; behaviour on Windows 10 and with the lock screen is unverified. | M | M | Try on a scratch desktop: toolbar visible on desktop 2, main window off, no focus theft; recheck on Win10. | Agent | open |
| R-16 | **Local, untracked evidence contains a JWT.** `investigation.cany/evidence/…` (ignored, never committed) triggers the `jwt` rule on a working-tree scan. | M | M | Not in the repo or its history, so nothing is published. Owner should delete the evidence folder or treat that token as compromised/rotate it if it is live; keep `investigation.cany/` ignored. | Owner | open |
| R-15 | **UI automation brittleness** (DPI virtualization, PrintWindow on layered windows). | M | L | DPI-aware capture; UIA selectors with fallbacks; accept manual verification when automation fails and say so. | Agent | open |

## Resolved in R4

- “Secret scanning not evidenced” — **closed**: tool installed, full-history scan run, result recorded,
  CI runs the identical pinned command.
- SDK pin ambiguity — **decided and verified** (D-022).
- CI runnability without a push — **closed**: actionlint validates the YAML and every `run:` command
  was executed locally with a passing result (see `PROGRESS_LOG.md`).

## Open questions for the owner

- **Q-08 (W1): answered** — the owner chose “continue”, so the refresh-limited gate (D-028) stands.


1. **Q-01 Priority:** finish the UI set (U7–U12) or switch to release foundations (R2/R4) first?
2. **Q-02 Skills:** free two slots in the 50-skill library for `fpm-wpf-verification` and
   `fpm-safety-contract`, or keep them as repo files only?
3. **Q-03 Localization:** build the string-resource foundation now, or accept literal strings until
   a dedicated sweep (and risk a much larger conversion later)?
4. **Q-04 Performance:** approve replacing `AllowsTransparency` with DWM rounded corners in W1 so the
   16 ms frame budget is reachable?
5. **Q-05 Release authority:** when may commits be pushed, who signs installers, and is there a
   code-signing budget (SignPath/Azure Trusted Signing/cert)?
6. **Q-06 REA:** accept the existing `REA_REFERENCE_AUDIT.md` as the parity gate, or re-run it?
7. **Q-07 Cadence:** per-card report + phase gate review with you as approver — confirm.

Defaults if the owner does not answer: Q-01 → U7–U9 then R2/R4; Q-02 → repo-only; Q-03 → foundation
at U10; Q-04 → capture regressions, then proceed; Q-05 → no push, unsigned local builds; Q-06 →
accept; Q-07 → per-card report.
