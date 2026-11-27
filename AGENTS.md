# FPM — agent instructions

FPM is a WPF/.NET 8 Windows PC manager (`app/PCManager.sln`). This file is the entry point for any
coding agent. Read it first, then only the documents the current card needs.

## Start here

1. `docs/freebuff/FPM_FreeBuff_00_START_HERE.md` §1–§4 — the working protocol (one card per
   session; never invent paths/APIs/versions; 0 warnings/0 errors; commit after each green card).
2. `docs/readiness/README.md` — scope, success criteria, phase gates, definition of done.
3. `docs/readiness/PHASE2_PLAN.md` — the ordered wave/card plan and current position.
4. `docs/readiness/PROGRESS_LOG.md` — the only place progress is evidenced.
5. The current card's section in `docs/freebuff/FPM_FreeBuff_0*.md`.

`docs/readiness/skills/` contains two project skills that must be applied on every card:
`fpm-wpf-verification.md` (build/test/lint/contrast/runtime evidence) and
`fpm-safety-contract.md` (any cleanup, debloat, ADB or helper change). They are repo files because
the account skill library is full; read them directly.

## Non-negotiables

- Build: `dotnet build app/PCManager.sln -nodeReuse:false -m:1 --no-restore` → **0 warnings, 0 errors**.
- Tests: `cd app && dotnet test tests/PCManager.Tests/PCManager.Tests.csproj --no-build -nodeReuse:false`.
- Gates: `tools/lint-xaml.ps1` (ratchet, never raised), `tools/contrast-check.ps1`, `IconGen --check`.
- Stop `FPM.exe` before building (DLL locks); never kill the IDE BuildHost.
- No stubs, `TODO`, fake/sample data; unavailable data says “Unavailable” and why.
- Files under 400 lines; tokens only; Light **and** Dark; 360/560/1280; reduced motion; keyboard and
  automation names.
- Never store/print/commit tokens, keys or personal data. Never push without the owner's explicit
  confirmation. Never run downloaded or third-party scripts.
- Safety: scan → review → explicit approval → typed actions → verify → log → undo. Never touch
  Microsoft PC Manager, Windows critical processes, or protected Android packages.

## State and reporting

- `AGENT_STATE.md` (git-ignored) carries the live handoff: current card, done cards, next three,
  blockers, last build/test output.
- `docs/readiness/PROGRESS_LOG.md` carries verified evidence per card, including what was **not**
  checked.
- Update `CHANGELOG.md` and `AGENT_STATE.md`, then commit with a conventional message.

## Reference material (read-only, never committed or built)

- `reference/win11debloat` — Win11Debloat source (MIT); port data to typed C# tweaks.
- `reference/universal-android-debloater` — UAD source/lists (GPL-3.0); port to C# package logic.
- Never execute their scripts; never bundle Google platform-tools; never copy Microsoft assets.
