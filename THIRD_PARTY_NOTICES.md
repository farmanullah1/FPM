# Third-party notices

FPM is licensed **GPL-3.0-or-later** (see `LICENSE`). This file lists the third-party material FPM
uses, ports or links against, with the notices their licences require.

## Ported / referenced projects

### Win11Debloat

FPM ports the *catalogue* of Windows debloat tweaks (app names, registry values, feature toggles).
It does not run, bundle or redistribute the PowerShell script.

```
MIT License

Copyright (c) 2020 Raphire (https://github.com/Raphire)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Source: <https://github.com/Raphire/Win11Debloat>

### Universal Android Debloater (UAD)

FPM ports UAD's package lists and debloat behaviour to native C#. UAD is licensed
**GPL-3.0**; FPM's GPL-3.0-or-later licence is compatible. UAD's source and licence text are
available at <https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation>.

When the ported list lands (`Assets/android/uad_lists.json`), keep the upstream copyright notice
in the file header and record the exact upstream commit here.

### Android platform-tools

`adb` is **not bundled**. FPM may offer to download Google's official platform-tools zip after the
user consents, and verifies the Authenticode signature is Google's before use. Google's Android SDK
licence applies to that download.

### Microsoft PC Manager

Used only as a **behavioural reference** (REA audit). No Microsoft code, icons, text or other
assets are copied or redistributed. FPM is not affiliated with Microsoft.

## NuGet dependencies

Resolved by `dotnet list package` on 2026-11-27:

| Package | Version | Licence |
|---|---|---|
| `CommunityToolkit.Mvvm` | 8.3.2 | MIT |
| `Microsoft.Extensions.DependencyInjection` | 8.0.1 | MIT |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 8.0.2 | MIT |
| `System.Drawing.Common` | 8.0.7 | MIT |
| `System.Management` | 8.0.0 | MIT |
| `System.ServiceProcess.ServiceController` | 8.0.0 | MIT |
| `System.Text.Json` | 8.0.5 | MIT |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | MIT |
| `xunit` | 2.9.2 | Apache-2.0 |
| `xunit.runner.visualstudio` | 2.8.2 | Apache-2.0 |

Future additions must be recorded here with their licence before merge. Planned, not yet added:
`LibreHardwareMonitorLib` (MPL-2.0, needs a licence review and attribution when used).

## Fonts and icons

FPM's imagery is original: the emblem and `Icon3D` geometry were drawn for this project. No vendor
icon packs (Fluent, Syncfusion, etc.) are redistributed. The UI uses the Segoe UI Variable family
shipped with Windows.

## Build and packaging tools (not redistributed)

Inno Setup (used to build the installer) and the .NET SDK are build-time tools; their licences apply
to the tools, not to FPM's output.
