# Baseline — 2026-11-27

All values below were read from this machine during Phase 1; commands are reproducible.

## 1. Repository

| Item | Value |
|---|---|
| Branch | `main` |
| HEAD | `ccdbff4 feat(ui): cache page visuals and slide a shared nav indicator` |
| Working tree | clean (0 modified/untracked) |
| Remote | `origin https://github.com/farmanullah1/FPM.git` (fetch/push) |
| Tags | none |
| Push state | branch is ahead of `origin/main`; nothing has been pushed |
| Tracked files | 276 |
| Largest tracked files | `app/screenshots/{Storage,Protection,Apps,Settings,Toolbox}.png` (92–124 KB each) — documentation images |
| Solution | `app/PCManager.sln` (Core, Services, ViewModels, PCManager shell, Helper, Tests) |
| Target | .NET 8 (`net8.0`, `net8.0-windows`), C# 12, WPF, nullable on |
| Ignored reference material | `reference/win11debloat`, `reference/universal-android-debloater` (read-only references) |
| Ignored AI working files | `AGENT_STATE.md`, `docs/freebuff/`, prompt files (per R1 rules) |

## 2. Toolchain

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | **10.0.404** (also **8.0.428** installed) | Projects target net8.0; SDK 10 builds them |
| `dotnet` runtime used by tests | net8.0 | `dotnet test` reports `PCManager.Tests.dll (net8.0)` |
| PowerShell | **5.1.26100.10068** (Windows PowerShell) | `pwsh` is not on PATH; scripts must use `powershell` |
| Git | **2.56.0.windows.2** | |
| Node.js | **22.23.5** | used only by `npx skills` |
| Bun | **1.4.2** | not used by this repo |
| OS | Windows (win32), display at **125 %** DPI | Windows 10/11 support is a product requirement |
| Inno Setup (`iscc`) | not verified in this baseline | required later by R3 |
| `gitleaks` | **8.30.1** | `%LOCALAPPDATA%\FPM\tools\gitleaks\gitleaks.exe`; checksum-verified release; used by `tools/secret-scan.ps1` and CI |
| `actionlint` | **1.7.12** | `%LOCALAPPDATA%\FPM\tools\actionlint\actionlint.exe`; workflow validation |
| SDK pin | `global.json` → 8.0.428 (`latestFeature`) | `dotnet --version` inside the repo reports 8.0.428 |
| `gh` CLI | present (`C:\Program Files\GitHub CLI`) but not authenticated in this session | R4/R6 publishing needs the owner's account |

## 3. Verified gates at this commit

| Gate | Command | Result |
|---|---|---|
| Build | `dotnet build app/PCManager.sln -nodeReuse:false -m:1 --no-restore` | **0 warnings, 0 errors** |
| Tests | `cd app && dotnet test tests/PCManager.Tests/PCManager.Tests.csproj --no-build -nodeReuse:false` | **166 passed**, 0 failed, 0 skipped |
| CI-equivalent build (R4) | `dotnet build app/PCManager.sln -c Release --no-restore -nodeReuse:false -m:1 -warnaserror` | **0 warnings, 0 errors** under the pinned SDK |
| Secret scan (R4) | `powershell -File tools/secret-scan.ps1` (gitleaks 8.30.1) | **77 commits scanned, no leaks** |
| Workflow lint (R4) | `actionlint -no-color -oneline .github/workflows/ci.yml` | exit 0 |
| XAML lint | `powershell -File tools/lint-xaml.ps1` | **380 literals across 10 files**, none above baseline |
| Contrast | `powershell -File tools/contrast-check.ps1` | every documented pair passes in both themes |
| Icons | IconGen `--check` | generated dictionaries are up to date (run before claiming U2/U3 changes) |

Runtime evidence from the last cards:

- U5 splash: Light/Dark/reduced captures; visible 757–1231 ms (inside 600–3000 ms).
- U6 pages: scroll offset 60 % preserved across navigation; 11–12 frames per 280 ms window,
  worst frame 32–45 ms; page-swap stalls (0.3–6.8 s) are gone after page caching.
- Settings and theme were restored to **Light**; `ReduceMotion=false`.

## 4. Existing documentation assets

Present: `README.md`, `PROJECT_SPEC.md`, `BUILD_STATUS.md`, `CHANGELOG.md`,
`REA_REFERENCE_AUDIT.md`, `docs/dev/REPO_AUDIT.md`, `installer/FPM.iss`, `.editorconfig`,
`.gitattributes`, `tools/` (IconGen, lint, contrast), `app/screenshots/*.png` (older set).

Added by R2: `LICENSE` (GPL-3.0), `THIRD_PARTY_NOTICES.md`, `CONTRIBUTING.md`, `SECURITY.md`,
`CODE_OF_CONDUCT.md`, `.github/` templates, dependabot and CODEOWNERS. Added by Phase 1:
`AGENTS.md` and `docs/readiness/`.

Added by R4: `.github/workflows/ci.yml` (build/test, UI gates, secret scan), `tools/secret-scan.ps1`,
`global.json`.

**Still missing and required by the plan:** `.github/workflows/release.yml` (R4 release half, needs
the owner's push/signing decision) and `docs/images/` (README screenshots, R5).

## 5. Known gaps carried into the plan

1. **Frame budget not met:** 32–45 ms worst frame (~30 fps). Page stalls fixed; the remaining
   ceiling is likely `AllowsTransparency="True"` on the main window forcing layered composition.
   Owner decision needed: fix in **W1** with DWM rounded corners.
2. **No localization layer.** Every string is a literal although START §1.10 and v7 §18 require
   externalized English + Urdu (RTL) keys. Must be decided before more UI lands.
3. **DPI coverage** is 125 % only; 100/150/200 % unverified (icons, splash, hit targets).
4. **No automated UI smoke test**; verification is still scripted UIA + screenshots per card.
5. **Secret scanning tool absent**; R1 only had a textual scan.
6. **First-run splash path** (“Welcome” chip) was never exercised; needs a fresh settings file.
7. **Skill library is full (50/50)** so the two new FPM skills could not be installed; they live
   in `docs/readiness/skills/` until a slot is freed (owner decision).
8. **Layout defects at small widths** (carried from U4 review): 560 px “Start with Windows”
   toggle overlaps its description; 360 px compact rail clips long nav labels; Home bento stays
   two-column at 360 px. Owned by U10.
