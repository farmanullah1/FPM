# FPM — Phase 1 readiness: source of truth

This folder is the single source of truth for how FPM is planned, verified and approved.
It complements (and never overrides) `docs/freebuff/FPM_FreeBuff_00_START_HERE.md` §1–§4,
which remains the working protocol, and `FPM_Master_Plan_v7.md`, which remains the product
specification. If this folder and those documents disagree, the plan documents win for product
behaviour; this folder wins for process, status and evidence.

The plan copies in `docs/freebuff/`, `AGENT_STATE.md`, the root plan/prompt/handoff files and the
read-only reference tree are intentionally local-only and git-ignored (D-066). This folder stays
tracked; no status, decision, risk or evidence file depends on an ignored path.

Status date: **2026-11-28**. Baseline commit: **`e33b5cb62a1aab340087261df0a4d24e2129dd3c`** (A2 implementation; the evidence follow-up records the full hash in `PROGRESS_LOG.md`).

## 1. Documents in this folder

| File | Purpose |
|---|---|
| `README.md` | This index: scope, success criteria, gates, ownership, cadence, DoD. |
| `BASELINE.md` | Current repository/tool/environment baseline with evidence. |
| `SKILLS.md` | Skill inventory, gap analysis, install attempts, sandbox test. |
| `TOOLS.md` | Prerequisites, dependencies, required tools and version compatibility. |
| `CHECKLIST.md` | Ordered checklist with effort estimates and completion criteria. |
| `DECISIONS.md` | Decision log (accepted decisions and their rationale). |
| `RISKS.md` | Risk register and open questions. |
| `TRACEABILITY.md` | Requirement → card → file → acceptance-criteria matrix. |
| `PROGRESS_LOG.md` | Dated log of verified steps; the only place progress is claimed. |
| `PHASE2_PLAN.md` | The approved-order execution plan for the plan documents. |

## 2. Scope

**In scope:** every card in `docs/freebuff/FPM_FreeBuff_00_START_HERE.md` §5 — G0–R1 (done),
U1–U12, W1–W5, T1–T4, D1–D5, A1–A7, R2–R6 — executed one card per session, plus the safety,
quality and documentation requirements of `FPM_Master_Plan_v7.md` §2, §8, §10–§22.

**Out of scope for this plan:** renaming the GitHub repository (explicitly forbidden), adding
Rust or a second GUI, bundling Google platform-tools, bundling or executing Win11Debloat, any
telemetry/accounts/remote execution, Microsoft assets or branding, and any undocumented Windows
API used without a guarded, off-by-default fallback.

**Deliberate deviations** (recorded, not defects): FPM is GPL-3.0-or-later because the Android
lists derive from UAD; FPM keeps safer behaviour than Microsoft PC Manager where they differ
(v7 §2 L4, `DEVIATION-SAFETY`); the toolbar-only process and sensors follow the documented-API
subset only.

## 3. Success criteria

A release is successful when every item below is true **with evidence in the repo**:

1. Every card in the plan is closed against its own acceptance criteria and the DoD in §5.
2. `dotnet build app/PCManager.sln -nodeReuse:false -m:1` → **0 warnings, 0 errors**.
3. `dotnet test` green, with the count recorded; no skipped tests used to hide failures.
4. `tools/lint-xaml.ps1` passes its ratchet (no file above baseline; baseline only decreases).
5. `tools/contrast-check.ps1` passes every documented pair in Light **and** Dark.
6. Every screen verified at 360 / 560 / 1280 px, Light and Dark, reduced motion, keyboard and
   automation names present, tokens only, real data only.
7. `FPM_Master_Plan_v7.md` §22 final acceptance checklist reported line by line with evidence.
8. `LICENSE`, `THIRD_PARTY_NOTICES.md`, CI, installer, README and screenshots exist and are
   truthful (R2–R6).
9. No secrets, proprietary binaries, reference code or AI working files in the public repo.

## 4. Phase gates and approvers

| Gate | Exit criteria | Approver |
|---|---|---|
| **G-1 Readiness (this phase)** | All files in §1 complete; baseline reproduced; blockers either resolved or explicitly accepted; checklist ordered. | Owner (Farmanullah) |
| **G-2 Plan** | `PHASE2_PLAN.md` approved; open questions answered or defaulted in writing; order and gates accepted. | Owner |
| **G-card** | Card DoD (§5) + card-specific acceptance criteria; evidence pasted in `PROGRESS_LOG.md`; committed. | Agent self-approves reversible work; owner for anything destructive/irreversible/release-facing |
| **G-release** | §3 items 2–9, installer test matrix, release notes. | Owner |

**Who approves what:** the owner approves phase gates, license/notices, anything pushed to
GitHub, releases, code signing, deleting user data or history, and changes to safety posture.
The agent decides reversible implementation details inside an approved card and must not ask
for those.

**Review cadence:** a written card report at the end of each card (what changed, gates with
output, what was not verified, next card); a phase review at each gate; an ad-hoc review
whenever a blocker or a destructive decision appears. No silent work across gates.

## 5. Definition of done

Every task/card is done only when all of these hold:

- [ ] Acceptance criteria of the card are met and written in `PROGRESS_LOG.md`.
- [ ] Build 0/0, tests green, lint and contrast gates pass (fresh output, not remembered).
- [ ] Light and Dark verified; 360/560/1280 reviewed where the change is visible.
- [ ] Reduced motion respected; keyboard path and automation names present for new controls.
- [ ] Tokens only (no new spacing/colour literals above the lint baseline); no stubs/TODO/fake data.
- [ ] Files stay under 400 lines; new logic has a focused test where testable.
- [ ] `CHANGELOG.md` and `AGENT_STATE.md` updated; committed on `main` with a conventional message.
- [ ] Anything not verified is stated plainly in the report.

A phase gate is done when every card in the phase is done, the gate's exit criteria are met, and
the owner has recorded approval in `DECISIONS.md`.

## 6. Working agreements

- One card per session; read only §1–§4 plus the card's section (START §1).
- Commit after every green card; never push without the owner's confirmation.
- Never store, print or commit tokens, keys or personal data; logs live in `%LOCALAPPDATA%\FPM`.
- When unsure, don't delete: report, explain, let the owner decide (v7 L7).
- Progress is only claimed from the `PROGRESS_LOG.md`; chat summaries are not evidence.
