# Ordered checklist (Phase 1 → Phase 2)

Order matters: each item's verification is the next item's prerequisite. Estimates are focused
working time for one agent session, not calendar time. `☑` = verified, `☐` = pending, `⛔` = blocked.

## Phase 1 — readiness

| # | Item | Estimate | Completion criterion | State |
|---|---|---|---|---|
| 1.1 | Read the working protocol and all plan documents | 1.5 h | Notes exist; card list extracted; no invented requirements | ☑ |
| 1.2 | Inspect the repository, git state and existing docs | 0.5 h | `BASELINE.md` §1/§4 written from commands | ☑ |
| 1.3 | Record toolchain versions and compatibility | 0.5 h | `TOOLS.md` §1–§3 filled; gaps flagged | ☑ |
| 1.4 | Reproduce the baseline gates (build/tests/lint/contrast) | 0.5 h | Output pasted in `BASELINE.md` §3 | ☑ |
| 1.5 | Skill inventory + gap analysis (`/find-skills`) | 1 h | `SKILLS.md` lists available skills, gaps, search results | ☑ |
| 1.6 | Install the two project skills | 0.25 h | Installed **or** blocker recorded with resolution (currently blocked: library 50/50) | ⛔ |
| 1.7 | Sandbox-test the skill content | 0.25 h | Commands/paths in both skills resolve; no destructive step; result logged | ☑ |
| 1.8 | Scope and success criteria | 0.5 h | `README.md` §2–§3 approved | ☑ |
| 1.9 | Single source of truth | 0.25 h | `README.md` §1 + `AGENTS.md` point here | ☑ |
| 1.10 | Decision log and risk register | 0.75 h | `DECISIONS.md`, `RISKS.md` complete | ☑ |
| 1.11 | Definition of done (task + gate) | 0.25 h | `README.md` §4–§5 | ☑ |
| 1.12 | Version control, backup, rollback | 0.25 h | `README.md` + `BASELINE.md` procedure; baseline tag made | ☑ |
| 1.13 | Review checkpoints and approvers | 0.25 h | `README.md` §4 approved (Q-07) | ☑ (pending owner confirm) |
| 1.14 | Traceability matrix | 1 h | `TRACEABILITY.md` maps every plan area to cards/files/criteria | ☑ |
| 1.15 | Stakeholders and communication cadence | 0.25 h | `README.md` §4; owner + agent roles explicit | ☑ |
| 1.16 | Effort estimates for every checklist item | 0.5 h | this table has an estimate per row | ☑ |
| 1.17 | Phase-gate acceptance criteria | 0.5 h | `README.md` §4 + `PHASE2_PLAN.md` wave gates | ☑ |
| **1.G** | **Phase 1 gate** | — | All rows ☑ or owner-accepted ⛔; owner approval recorded in `DECISIONS.md` | ☐ |

## Phase 2 — document review and execution setup

| # | Item | Estimate | Completion criterion | State |
|---|---|---|---|---|
| 2.1 | Read v7 and all six FreeBuff documents in full | 2 h | Section map + card dependencies recorded | ☑ |
| 2.2 | Reconcile plan v7 cards with FreeBuff cards | 1 h | One ordered backlog in `PHASE2_PLAN.md`, no duplicates | ☑ |
| 2.3 | Identify prerequisites/blockers per wave | 0.5 h | Wave table has entry conditions | ☑ |
| 2.4 | Ask the owner the seven open questions | 0.5 h | Answers recorded in `DECISIONS.md` | ☐ |
| 2.5 | Approve the wave plan | 0.25 h | G-2 recorded | ☐ |
| 2.6 | Execute wave 1, one card per session | 20–30 h | Each card's DoD + acceptance evidenced | ☐ (R2, R4 done; W1 next) |

## Waves (details in `PHASE2_PLAN.md`)

| Wave | Cards | Estimate | Gate |
|---|---|---|---|
| W0 Foundations | R2 licence/notices, R4 CI, gitleaks, SDK pin | 8–10 h | **done**: workflow validated, every job command run locally green, history scan clean, SDK pinned |
| W1 UI finish | U7, U8, U9, U10, U11, U12 (+ localization foundation in U10) | 30–40 h | All screens at 3 widths, reduced motion, 16 ms budget |
| W2 Windows | W1, W2, W3, W4, W5 | 20–28 h | Window lifecycle + toolbar survive close; tests |
| W3 Sensors/toolbox | T1, T2, T3, T4 | 16–20 h | Real readings or `Unavailable`; no fabricated values |
| W4 Windows debloat | D1–D5 | 30–40 h | Catalog parity, journal/undo, no real-registry tests |
| W5 Android debloat | A1–A7 | 30–40 h | Typed ADB only, denylist, snapshot/undo |
| W6 Release | R3 installer, R5 README/screenshots, R6 metadata | 12–16 h | Test matrix, signed-or-documented, truthful README |
