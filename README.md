# Farmanullah PC Manager

A lightweight, modular Windows 10/11 desktop system utility built with **C# (.NET 8 WPF)** and **Windows 11 Fluent Design (`Wpf.Ui`)**. Designed to deliver real-time memory optimization, deep disk cleanup, process and startup control, system protection checks, and a floating desktop quick-bar—all cleanly structured so you can inspect, customize, and extend every feature.

---

## Table of Contents

1. [Key Features](#key-features)
2. [Side-by-Side Isolation (Zero Conflict with Microsoft PC Manager)](#side-by-side-isolation-zero-conflict-with-microsoft-pc-manager)
3. [Tech Stack & Requirements](#tech-stack--requirements)
4. [Project Architecture](#project-architecture)
5. [Getting Started (Build & Run)](#getting-started-build--run)
6. [How to Add or Modify Features](#how-to-add-or-modify-features)
7. [Permissions & Administrator Mode](#permissions--administrator-mode)
8. [Troubleshooting](#troubleshooting)
9. [License & Attribution](#license--attribution)

---

## Key Features

### 1. Home
* **PC Boost Hero Card:** Displays live physical memory usage (`%` and `Used / Total GB`) alongside detected temporary file size, a one-click **Boost** button (trims process working sets via Win32 `EmptyWorkingSet` and purges safe temp directories), and a **Smart Boost** toggle for automatic background memory relief when RAM usage crosses your configured threshold.
* **Quick-Action Grid:** Direct 2×2 navigation cards for **Health Check**, **Process Management**, **Deep Cleanup**, and **Startup Apps**.

### 2. Protection
* **System Security & Update Status:** Checks Windows Defender real-time protection status and queries Windows Update state.
* **System Repair & Guards:**
  * **Pop-up Management:** Controls notification and background overlay clutter.
  * **Default Browser Protection:** Verifies and restores your preferred default web browser associations.
  * **Taskbar Repair:** Restarts `explorer.exe` and resets stuck Windows taskbar/shell states safely.
  * **Restore Default Apps:** Quick access to reset file-type and protocol defaults.

### 3. Storage
* **System Drive (`C:`) Telemetry:** Visual capacity bar displaying used vs. free storage in GB.
* **Deep Cleanup:** Scans and cleans categorized system clutter with individual checkboxes and real byte counts:
  * Windows Update delivery & download caches
  * System temporary files (`%TEMP%` and `C:\Windows\Temp`)
  * Recycle Bin contents
  * Browser caches (Edge, Chrome, Firefox, Brave)
  * Windows error reports and old log files
* **Manage Large Files:** Multi-threaded directory scanner with instant size filter pills (`>10 MB`, `>50 MB`, `>100 MB`, `>1 GB`) and one-click Explorer reveal or deletion.
* **Duplicate Files & Storage Sense:** Identifies redundant files by size/hash and links directly to Windows Storage Sense automation.

### 4. Apps
* **Process Management:** Live list of active user applications sorted by RAM footprint (`MB`), with a safe **End** button per process.
* **Startup Apps:** Enumerates startup entries from `HKCU\...\Run`, `HKLM\...\Run`, and the Windows Startup folder with live **On / Off** toggle switches.
* **App Management:** Lists installed Win32 and MSIX/Store packages with instant search filtering and uninstaller launch support.

### 5. Toolbox & Floating Desktop Widget
* **Floating Toolbar:** Toggles an always-on-top, draggable Windows 11 pill widget on your desktop featuring a live RAM gauge, one-click **Boost**, **Screenshot**, **Notepad**, and **Calculator** shortcuts.
* **Built-in Utilities:** Quick-launch cards for Screen Capture, Screen Recorder, Calculator, Notepad, DNS/Network Cache Flush (`ipconfig /flushdns`), and custom Quick Web Links.

### 6. Settings
* **Startup Behavior:** Toggle automatic launch at Windows sign-in.
* **Smart Boost Threshold:** Adjustable RAM usage slider (`50%`–`95%`) that triggers automatic background optimization.
* **Appearance:** Seamless switching between **Windows 11 Light Mode** (`#F3F3F3` canvas / `#FFFFFF` cards) and **Dark Mode** (`#202020` canvas / `#2B2B2B` cards).

---

## Side-by-Side Isolation (Zero Conflict with Microsoft PC Manager)

**Farmanullah PC Manager** is engineered to coexist peacefully on a machine that already has **Microsoft PC Manager** installed:

| Isolation Layer | Implementation in Farmanullah PC Manager |
| :--- | :--- |
| **Executable & Assembly** | Compiled as `FarmanullahPCManager.exe` (never `MSPCManager.exe` or `PCManager.exe`). |
| **Single-Instance Mutex** | Uses a unique GUID-scoped global mutex (`Global\FarmanullahPCManager_SingleInstance_...`) so launching either utility never blocks the other. |
| **App Data & Logs** | Stores all JSON settings, scan caches, and logs exclusively in `%LocalAppData%\FarmanullahPCManager\`. |
| **Registry Namespace** | Writes app preferences under `HKCU\Software\FarmanullahPCManager\`. |
| **Windows Startup Entry** | Registers its own distinct value name (`"FarmanullahPCManager"`) under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` without touching Microsoft's entry. |
| **URI Protocols** | Does **not** register or hijack `mspcmanager://` or `pcmanager://` protocol handlers. |
| **Boost & Task Exclusions** | Explicitly excludes its own `Environment.ProcessId` and `MSPCManager` during working-set trims and process termination. |

---

## Tech Stack & Requirements

* **Operating System:** Windows 10 (version 1809 / build 17763 or later) or Windows 11 (x64 / ARM64)
* **Framework:** .NET 8.0 (`net8.0-windows`)
* **UI Layer:** WPF with **WPF-UI (`Wpf.Ui`)** Fluent Design controls & Mica/Acrylic backdrop support
* **Architecture:** MVVM (`CommunityToolkit.Mvvm`) + Dependency Injection (`Microsoft.Extensions.DependencyInjection`)
* **Windows Interop:** `System.Management` (WMI), `Microsoft.Win32` (Registry), and Win32 P/Invoke (`psapi.dll`, `kernel32.dll`, `shell32.dll`)

### Development Prerequisites
1. [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Visual Studio 2022** (with the *.NET desktop development* workload) or **Visual Studio Code** (with the *C# Dev Kit* extension)

---

## Project Architecture

Every component is decoupled into distinct folders so UI styling, state logic, and Windows system calls never bleed together:

```text
FarmanullahPCManager/
├── FarmanullahPCManager.sln          # Visual Studio solution file
├── FarmanullahPCManager/
│   ├── FarmanullahPCManager.csproj   # Project config, NuGet packages, app manifest
│   ├── app.manifest                  # Windows compatibility & execution level settings
│   ├── App.xaml / App.xaml.cs        # Startup lifecycle, single-instance Mutex, DI container
│   ├── MainWindow.xaml / .cs         # Main Fluent NavigationView shell & title bar
│   ├── Views/                        # XAML UI pages & windows
│   │   ├── HomeView.xaml             # PC Boost hero card & 2x2 quick-action grid
│   │   ├── ProtectionView.xaml       # Security status, browser guard, taskbar repair
│   │   ├── StorageView.xaml          # C: drive bar, Deep Cleanup, Large File scanner
│   │   ├── AppsView.xaml             # Process manager, Startup apps, Installed apps
│   │   ├── ToolboxView.xaml          # Floating toolbar switch & utility launcher cards
│   │   ├── SettingsView.xaml         # Theme, Smart Boost slider, auto-start, About
│   │   └── FloatingToolbarWindow.xaml# Draggable always-on-top desktop widget
│   ├── ViewModels/                   # Observable state, commands, and UI bindings
│   │   ├── MainViewModel.cs
│   │   ├── HomeViewModel.cs
│   │   ├── ProtectionViewModel.cs
│   │   ├── StorageViewModel.cs
│   │   ├── AppsViewModel.cs
│   │   ├── ToolboxViewModel.cs
│   │   └── SettingsViewModel.cs
│   ├── Services/                     # Isolated Windows system services & interfaces
│   │   ├── IMemoryBoostService.cs    # RAM telemetry & Win32 working-set trimming
│   │   ├── ICleanupService.cs        # Temp, cache, Recycle Bin, & Windows Update cleanup
│   │   ├── IStorageAnalyzerService.cs# Drive stats, large file & duplicate scanners
│   │   ├── IProcessService.cs        # Live process enumeration & safe termination
│   │   ├── IStartupService.cs        # Registry & Startup folder enumeration/toggling
│   │   ├── IProtectionService.cs     # Defender, Windows Update, & shell/taskbar repair
│   │   └── ISettingsService.cs       # JSON config persistence & theme management
│   ├── Models/                       # Plain C# data models
│   │   ├── ProcessItem.cs
│   │   ├── StartupAppItem.cs
│   │   ├── CleanupCategory.cs
│   │   └── LargeFileItem.cs
│   └── Helpers/                      # P/Invoke declarations, byte formatters, converters
│       ├── NativeMethods.cs
│       └── ByteSizeConverter.cs
└── README.md
```

---

## Getting Started (Build & Run)

### Option A: Using the .NET CLI (PowerShell / Terminal)

1. Navigate to the `app` folder containing the `.sln` file:
   ```powershell
   cd "C:\Users\farma\Documents\Craft Anything\<your-project-folder>\app"
   ```
2. Restore NuGet dependencies:
   ```powershell
   dotnet restore
   ```
3. Build the solution:
   ```powershell
   dotnet build --configuration Release
   ```
4. Run the application:
   ```powershell
   dotnet run --project FarmanullahPCManager --configuration Release
   ```

### Option B: Using Visual Studio 2022

1. Double-click `FarmanullahPCManager.sln`.
2. Ensure **FarmanullahPCManager** is set as the Startup Project in Solution Explorer.
3. Select **Release** (or **Debug**) and **x64** in the top toolbar, then press **`F5`** (Start Debugging) or **`Ctrl + F5`** (Start Without Debugging).

### Publishing a Standalone `.exe`

To generate a self-contained single-file executable you can copy anywhere without needing the .NET runtime pre-installed:

```powershell
dotnet publish FarmanullahPCManager -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
The compiled `FarmanullahPCManager.exe` will be output to `FarmanullahPCManager\bin\Release\net8.0-windows\win-x64\publish\`.

---

## How to Add or Modify Features

Because every utility is isolated behind an interface in `Services/`, adding a new cleaner or tool takes four quick steps:

### 1. Create a New Service Interface & Implementation
Add `INetworkMonitorService.cs` and `NetworkMonitorService.cs` inside `Services/`:
```csharp
public interface INetworkMonitorService
{
    Task<long> FlushDnsCacheAsync();
    (double DownloadMbps, double UploadMbps) GetCurrentThroughput();
}
```

### 2. Register the Service in `App.xaml.cs`
Register it inside the Dependency Injection container in `ConfigureServices`:
```csharp
services.AddSingleton<INetworkMonitorService, NetworkMonitorService>();
```

### 3. Inject into a ViewModel
Pass `INetworkMonitorService` into any existing ViewModel constructor (such as `ToolboxViewModel.cs`) and expose a `[RelayCommand]` method so the UI can trigger it asynchronously without freezing the window.

### 4. Bind in XAML (`Views/`)
Add a `ui:CardAction` or `ui:Button` inside `Views/ToolboxView.xaml` (or create a new `NavigationViewItem` in `MainWindow.xaml` for a brand-new sidebar tab) and bind its `Command` property to your new ViewModel command.

---

## Permissions & Administrator Mode

* **Standard User Mode:** Works out of the box for RAM boost on user processes, `%TEMP%` and browser cache cleanup, `HKCU` startup toggles, large-file scanning, and the floating toolbar.
* **Administrator Mode (Optional):** Cleaning system-wide directories (`C:\Windows\Temp`, `C:\Windows\SoftwareDistribution\Download`), toggling `HKLM` machine-wide startup items, or ending elevated background processes requires running `FarmanullahPCManager.exe` as **Administrator**.
  * To require elevation automatically on every launch, open `app.manifest` and change `level="asInvoker"` to `level="requireAdministrator"`.

---

## Troubleshooting

* **Some temp files are skipped during Deep Cleanup:** Files actively locked by running browsers or Windows services are intentionally caught (`IOException` / `UnauthorizedAccessException`) and skipped safely so cleanup never crashes the app. Close open browsers before running a full cache purge for maximum space recovery.
* **WPF-UI icons or theme look unstyled:** Verify that `<ResourceDictionary Source="pack://application:,,,/Wpf.Ui;component/Resources/Theme/Light.xaml" />` and `<ui:ControlsDictionary />` are loaded in `App.xaml`.
* **Antivirus false positive on custom build:** Unsigned freshly compiled `.exe` files that call Win32 process/memory APIs (`EmptyWorkingSet`) can occasionally trigger heuristic warnings in Windows Defender. Add your project's `bin\` output folder to **Windows Security → Exclusions** during local development.

---

## License & Attribution

* Built for personal and educational Windows system management.
* All application branding, titles, and custom icons belong to **Farmanullah PC Manager** and contain no copyrighted Microsoft visual assets.