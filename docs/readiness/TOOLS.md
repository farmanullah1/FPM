# Prerequisites, dependencies and required tools

Compatibility rule: **confirm the version on this machine before installing anything**; never
assume a tool exists. Baseline versions are in `BASELINE.md`.

## 1. Required for every card

| Tool | Required version | Compatibility check | Status |
|---|---|---|---|
| .NET SDK | 8.0.x or newer that can target net8.0 | `dotnet --list-sdks` shows 8.0.428 and 10.0.404 | ✅ |
| Windows PowerShell | 5.1+ | `powershell -NoProfile -Command "$PSVersionTable.PSVersion"` | ✅ |
| Git | 2.4x+ | `git --version` → 2.56.0 | ✅ |
| Windows 10/11 x64 | 10.0.19041+ for the shell's Windows SDK APIs | runtime check; product supports both | ✅ |
| Display at 100–200 % DPI | needed for U2/U10/W1 verification | currently only 125 % verified | ⚠️ gap |

## 2. Required for specific cards

| Card | Tool / dependency | Notes |
|---|---|---|
| U2/U3 | `tools/IconGen` (`dotnet run --project tools/IconGen -- --check`) | repo-local; no install |
| R2/R4 | `dotnet-project-licenses` or equivalent license report | optional; notices may be written manually from `dotnet list package` |
| R3 | **Inno Setup 6** (`iscc`) | not installed/verified yet; Windows-only |
| R3 | `dotnet publish -r win-x64 --self-contained true` | works with the installed SDK |
| R4 | GitHub Actions, `gh` CLI (optional for local release) | owner's account; never print tokens |
| R5 | screenshot capture script (UIA + PrintWindow) | repo-local `tools/` scripts, DPI-aware |
| D1–D5 | Win11Debloat reference source at `reference/win11debloat` | present, read-only |
| A1–A7 | Android platform-tools `adb` (user-consented download, Google-signed) | never bundled |
| T2 | `LibreHardwareMonitorLib` NuGet (MPL-2.0) | licence review required before adding |
| W2 | COM `IVirtualDesktopManager` | documented API only |
| R1/R4 | `gitleaks` or `trufflehog` | **not installed**; install before R4 |

## 3. NuGet and runtime dependencies already in use

Read from the solution (`dotnet list package` verifies exact resolved versions; pin there, do not
guess here):

- `CommunityToolkit.Mvvm` 8.3.x — MVVM source generators.
- `Microsoft.Extensions.DependencyInjection` 8.0.x — composition root.
- `Microsoft.Extensions.Hosting` abstractions where used.
- `System.Management` — WMI reads (sparingly).
- `xunit`, `Moq`, `FluentAssertions` — tests.
- `Microsoft.Toolkit.Uwp.Notifications` — toasts (verify presence before using).

Rules: add packages only with `dotnet add package <name>` and accept the resolved version; check
the licence against GPL-3.0-or-later compatibility; record new dependencies in
`THIRD_PARTY_NOTICES.md` (R2).

## 4. Install/verification procedure (before installing anything)

1. `command -v <tool>` / `Get-Command` → is it already present? Use the present version.
2. Read the version and compare with the requirement above.
3. Confirm the licence and redistribution terms (especially platform-tools, LibreHardwareMonitor,
   Inno Setup, and any code-signing service).
4. Install only if the card needs it, with an explicit, recorded command.
5. Re-run the version check and paste both outputs into `PROGRESS_LOG.md`.

## 5. Not to be installed or bundled

- Microsoft PC Manager files, the installer, or any Microsoft binary/asset.
- Win11Debloat's PowerShell script (port the data/logic instead).
- Google platform-tools in the repo or installer (download only after consent + Authenticode check).
- Rust, a second GUI toolchain, or a WPF UI package (`Wpf.Ui` is explicitly excluded).
- Telemetry/analytics SDKs.
