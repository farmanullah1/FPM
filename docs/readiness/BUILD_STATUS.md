# Build status and runtime footprint

Owner: Agent. Verified on the development machine, 2026-11-27.

## Build

| Gate | Command | Result |
|---|---|---|
| Build | `dotnet build app/PCManager.sln -nodeReuse:false -m:1 --no-restore` | **0 warnings, 0 errors** |
| Tests | `dotnet test app/tests/PCManager.Tests/PCManager.Tests.csproj --no-build -nodeReuse:false` | **188 passed**, 0 failed |
| XAML lint | `tools/lint-xaml.ps1` | 376 literals / 10 files, within baseline |
| Contrast | `tools/contrast-check.ps1` | all pairs pass in both themes |

## Toolbar-only footprint (W3)

Measured after `FPM.exe --toolbar-only` had been running for 8 s, tray icon and toolbar visible, no
main window (`Get-Process` → `WorkingSet64` / `PrivateMemorySize64`):

| Metric | Value |
|---|---|
| Working set | **124.6 MB** |
| Private memory | **50.2 MB** |
| Main window handle | none |
| Visible windows | `FPM Toolbar` |

This is above what “lightweight” suggests; recorded honestly in `RISKS.md` (R-19). No CPU figure is
recorded because a single idle sample would not be meaningful without a sampling window.

## Frame pacing (W1)

The development display runs at 40 Hz, so 16 ms frames cannot occur here. Measured traces and the
accepted gate are in `PROGRESS_LOG.md` and `DECISIONS.md` (D-027, D-028).
