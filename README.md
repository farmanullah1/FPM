# Farmanullah PC Manager

A Windows 10/11 system utility built with **C# / .NET 8 / WPF (MVVM)**: live memory boost,
deep disk cleanup, process and startup control, protection checks, a toolbox and an
always-on-top quick bar.

The application lives in [`app/`](app/) — see [`app/README.md`](app/README.md) for build
details and the behaviour of every page.

## Tech stack

| Area | What the code actually uses |
| --- | --- |
| Runtime | .NET 8 (`net8.0` for the model project, `net8.0-windows` for the rest), C# 12, nullable reference types enabled |
| UI framework | WPF with hand-written Fluent-inspired XAML — **no third-party UI library** (there is no `Wpf.Ui` or `MahApps` dependency anywhere) |
| Look and feel | `Styles/Themes/Light.xaml` and `Dark.xaml` define the same brush keys and are swapped at index 0 of the merged dictionaries by `ThemeService`; `Styles/Controls.xaml` holds templated controls (cards, buttons, toggle switch, slider, pills); `Styles/Icons.xaml` holds original 24×24 `Geometry` icons |
| Windowing | Borderless shell with a custom title bar via `WindowChrome`; `FloatingToolbarWindow` is a transparent, topmost pill |
| MVVM | `CommunityToolkit.Mvvm` 8.3.2 — `ObservableObject`, `[ObservableProperty]`, `RelayCommand`, `AsyncRelayCommand` |
| Composition | `Microsoft.Extensions.DependencyInjection` 8.0.1 with a single registration point (`Services/DependencyInjection/ServiceCollectionExtensions.cs`); WPF-only services are registered in `App.xaml.cs` |
| System access | Win32 P/Invoke (`Services/Interop/NativeMethods.cs`), WMI via `System.Management`, `Microsoft.Win32.Registry`, `ServiceController`, `System.Drawing.Common` for screen capture |
| State | JSON settings and rolling logs under `%LOCALAPPDATA%\FarmanullahPCManager`; `Core/AppInfo.cs` is the single source of identity, paths and protected process names |

`UseWindowsForms` is enabled in the shell project for one reason only — the tray
`NotifyIcon`. Everything the user sees is WPF.

## Architecture

Four projects with a one-way dependency chain:

```
PCManager.Core            models, formatting, identity constants      (no Windows UI)
      ▲
PCManager.Services        all system access behind interfaces
      ▲
PCManager.ViewModels      page and shell view models
      ▲
PCManager                 WPF shell, styles, views  →  FarmanullahPCManager.exe
```

| Layer | Responsibility | Key files |
| --- | --- | --- |
| `Core` | Plain models, byte formatting and app identity (`ProductName`, data folder, Run value, protected process names) | `AppInfo.cs`, `Models/*.cs`, `Formatting/ByteFormatter.cs` |
| `Services` | One interface per capability in `Abstractions/`, implementations grouped by feature (`Boost`, `Cleaners`, `Process`, `Storage`, `Protection`, `Toolbox`, `Settings`, `Startup`), shared helpers in `Infrastructure/` | `DependencyInjection/ServiceCollectionExtensions.cs`, `Storage/DeepCleanupService.cs`, `Process/StartupManager.cs` |
| `ViewModels` | Six page view models plus the shell; no Win32 calls, only `Services.Abstractions` | `MainViewModel.cs`, `HomeViewModel.cs`, `StorageViewModel.cs` |
| `PCManager` | XAML views, themed styles, converters, DI composition root, tray icon, floating toolbar window | `App.xaml(.cs)`, `Views/*.xaml`, `Styles/*` |

How a page is composed:

1. `MainViewModel` owns the six page view models and exposes the rail (`Pages` plus the pinned
   `SettingsItems`); `App.xaml` maps each view model to its view with a `DataTemplate`, so the
   shell's `ContentControl` needs no code-behind switching.
2. Pages never reference the shell: quick-action cards call `INavigationService`, whose
   `NavigationService` broker forwards the page key to `MainViewModel`.
3. A page talks only to interfaces (`IStorageService`, `IDeepCleanupService`, …). The facade
   implementations move enumeration, WMI and registry work onto the thread pool with
   `Task.Run`, so the dispatcher stays responsive.
4. Anything destructive goes through `Infrastructure/SafeFileSystem` and the per-feature
   cleaners, which return the bytes actually released instead of assuming success.

## Quick start

```powershell
cd app
dotnet build PCManager.sln
dotnet run --project src/PCManager/PCManager.csproj
```

Windows 10/11 with the .NET 8 SDK (`app/global.json` pins the feature band). The executable
is `app/src/PCManager/bin/Debug/net8.0-windows/FarmanullahPCManager.exe`.

## Pages

| Page | What it does |
| --- | --- |
| Home | Live memory usage, temporary-file size, one-click Boost and Smart Boost auto-trim, four quick actions. |
| Protection | Defender and Windows Update status plus pop-up, browser, Explorer-restart and default-apps repair cards. |
| Storage | Drive usage, deep cleanup with measured sizes, large-file filters, duplicate finder and Storage Sense. |
| Apps | Running applications with End Task, startup entries with real toggles, installed apps with search and uninstall. |
| Toolbox | Floating toolbar toggle, screenshot, screen recorder, calculator, notepad, DNS flush, quick links. |
| Settings | Start with Windows, Smart Boost threshold, Light/Dark theme, About and data/log paths. |

## Side-by-side with Microsoft PC Manager

Everything about the identity is distinct so both utilities can be installed at once:
`FarmanullahPCManager.exe`, its own single-instance mutex, AppUserModelID, data folder
(`%LOCALAPPDATA%\FarmanullahPCManager`) and Run value. No `mspcmanager://` or `pcmanager://`
protocol is registered, and Boost / End Task never touch this process or any `MSPCManager*`
process.

## Screenshots

`app/screenshots/` contains a capture of each page and of the floating toolbar.

## License & attribution

Personal and educational Windows system management. All branding, titles and icons belong to
Farmanullah PC Manager; no Microsoft visual assets are redistributed.
