# Skill: fpm-wpf-verification

*Intended for the account skill library; kept here because that library is full (see `SKILLS.md`).
Description for installation: “Run the FPM WPF card verification gates (0-warning build, tests,
XAML lint ratchet, contrast, icon generator check, runtime screenshots) and handle this repo's
known build/lock pitfalls before claiming a card is done.”*

## 1. Stop the app before building

A running `FPM.exe` locks `PCManager.ViewModels.dll` and produces MSB3026/MSB3027 retries:

```
powershell -NoProfile -Command "Get-Process FPM -ErrorAction SilentlyContinue | Stop-Process -Force"
```

## 2. Build (must be 0 warnings, 0 errors)

From the repo root, never from a background subshell:

```
dotnet build app/PCManager.sln -nodeReuse:false -m:1 --no-restore
```

Grep the log for `Warning(s)` and `Error(s)`; never accept “Build succeeded” alone.

Known failures:
- `CS2001 ... obj\Debug\net8.0-windows\Views\*.g.cs could not be found` → delete every `obj`/`bin`
  under `app/`, restore, rebuild.
- MSB3026/MSB3027 naming `FPM`, `PCManager.Core.dll` or the IDE BuildHost → stop FPM or wait ~45 s
  for the IDE BuildHost; never kill the IDE.

## 3. Tests

```
cd app && dotnet test tests/PCManager.Tests/PCManager.Tests.csproj --no-build -nodeReuse:false
```

Report the exact `Passed! ... Failed: 0` line and the total. Never add skips to go green.

## 4. Static gates

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/lint-xaml.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/contrast-check.ps1
dotnet run --project tools/IconGen -- --check
```

Lint is a ratchet: the count may not rise above the baseline; never raise the baseline to pass a
card. `pwsh` is not on PATH — use `powershell`.

## 5. Runtime checks (no fake success)

- Settings live in `%LOCALAPPDATA%\FPM\settings.json` (`Theme` 1=Light, 2=Dark). Restore the
  owner's theme/settings after a test that changes them.
- Logs: `%LOCALAPPDATA%\FPM\logs\app-YYYYMMDD.log`.
- Startup takes up to ~10 s: poll `$proc.MainWindowHandle` for up to 40 s.
- Capture windows with `PrintWindow(hwnd, hdc, 2)` only after `SetProcessDPIAware()` in the capture
  process; otherwise coordinates are DPI-virtualized and the crop is wrong.
- Navigate with UI Automation: find the nav `Text` label, walk `ControlViewWalker` up to the
  `ListItem`, then `SelectionItemPattern.Select()`.
- Scroll-retention test: find the scrollable with `VerticallyScrollable` and `VerticalViewSize < 100`,
  `SetScrollPercent(NoScroll, 60)`, navigate away and back, compare the reported percentage.
- Anything you did not look at is unverified: say so explicitly.

## 6. Report

State per gate: command run, result, and any check not run and why. Update `CHANGELOG.md` and
`docs/readiness/PROGRESS_LOG.md`, then commit with a conventional message; never push without the
owner's confirmation.
