# MECHANICS.md — System Implementation Details

This document records the real Windows APIs, thresholds, registry paths, and formulas discovered from decompiling the original Microsoft PC Manager (version 3.23.3.0). Use these values when implementing the .NET 8 WPF recreation.

## 1. Theme & Styling
- **ThemeOption enum** (found in `Microsoft.WIC.PCManager.Schema.ThemeOption`):
  - `Default = 0`
  - `Light = 1`
  - `Dark = 2`
  - `System = 3`  (follows Windows light/dark theme)
- The Theme is stored in user settings and applied to the shell's resources dictionary.

## 2. Health Check — System Scan Modules
| Module | Key API / Registry Path | What It Scans | Cleanup Action |
|--------|------------------------|---------------|----------------|
| **Temporary Files** | `%TEMP%` env‑folder (`%SystemRoot%\Temp` and `%USERPROFILE%\AppData\Local\Temp`) | Files older than 48 hours, size > 0 | `DeleteFile` after confirming not in use |
| **Browser/System Caches** | `HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings\Cache\Content` (URLCache) | IE/Edge cache index | Delete cache index entries (read‑only; actual `.tmp` files under `%LOCALAPPDATA%\Microsoft\Windows\INetCache`) |
| **Startup Impact** | `HKLM\Software\Microsoft\Windows\CurrentVersion\Run` and `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | App entries that run at logon | No direct cleanup; reports size/CPU impact if known |
| **System Updates** | `HKLM\Software\Microsoft\Windows\CurrentVersion\SideBySide\Updates` | Pending update metadata | No file cleanup |

- **One‑click cleanup** gathers all selected items, then calls `DeleteFile`/`RemoveItem` on each path, reporting total bytes freed.

## 3. PC Boost — Memory Optimisation
- **MemorySnapshot** structure (from `MemorySnapshot` class):
  - `TotalPhysicalBytes` — total physical RAM
  - `AvailableBytes` — currently available (free) RAM
  - `LoadPercent` — `(Total - Available) / Total * 100`
  - `CommitUsedBytes` — committed memory (pagefile + working set)
  - `WorkingSetsTrimmed` — count of process working sets that were trimmed
  - `ReclaimedBytes` — bytes reclaimed by the optimiser
- **Optimization routine**:
  1. Enumerate all running processes via `Process.GetProcesses()`
  2. For each non‑system process, call `SetProcessWorkingSetSizeEx(processHandle, -1, -1)` (Windows 10 1903+) to trim the working set
  3. Enumerate `%TEMP%` and delete files older than 48 h
  4. Return the new `MemorySnapshot`

## 4. Process & Startup Management
- **ProcessInfo** structure:
  - `ProcessId`, `Name`, `Path` (from `Module.FileName` of the main module)
  - `CpuPercent` — total CPU time / elapsed time (use `Process.TotalProcessorTime`)
  - `MemoryBytes` — `Process.WorkingSet64`
  - `MemoryPercent` — `MemoryBytes / (Total physical RAM) * 100`
  - `StartTime` — `Process.StartTime`
  - `IsSystem` — `Process.GetCurrentProcess().ProcessName.StartsWith("svchost")` or check `Process.SessionId == 0`
- **StartupApp** sources:
  - **RegistryCurrentUser**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, `RunOnce`
  - **RegistryLocalMachine**: `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`, `RunServices`
  - **TaskScheduler**: query `Task Scheduler Library\Microsoft\Windows\` for enabled tasks
- **End Task**: use `TerminateProcess(processHandle, 0)` after confirming the process is not a critical system process.

## 5. Storage Manager
- **DriveSpace** — query via `DriveInfo` or `GetLogicalDriveStrings` + `GetDriveType`.
  - `Name` (e.g. "C:\"), `Label`, `Format` (NTFS/exFAT), `TotalBytes`, `FreeBytes`, `IsReady`
- **FolderSize** — walk directory tree, sum file sizes, cap depth at 8 levels to avoid extremely deep scans.
- **LargeFile** — files > 100 MB (configurable threshold) found during folder scan.
- **Storage Sense** state:
  - Registry: `HKLM\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StorageSenseEnabled` (DWORD 0/1)
  - API: `SHGetKnownFolderPath` for `%SystemRoot%\System32\..\StorageSense\` then read the value.
- **Recycle Bin** — use `SHEmptyRecycleBinW(hwnd, NULL, 0x00000001)` (SHERB fall‑through).

## 6. Toolbox & Protection
- **Network/DNS info**:
  - Get adapters via `GetAdaptersInfo` (IP Helper) or `NetworkInterface.GetAllNetworkInterfaces()`
  - For each adapter: `IPv4`, `Gateway`, `DnsServers`, `IsUp`
- **DNS Lookup**: `Dns.GetHostAddresses(hostName)`; measure elapsed time with `Stopwatch`.
- **Windows Defender status** (DefenderStatus):
  - Available via `Windows.Security.Core.SecuritySystem` or WMI: `root\SecurityCenter2` → `AntiVirusProduct`
  - `RealTimeEnabled` — `AntiVirusProduct.IsAntivirusEnabled`
  - `AntivirusEnabled` — same property
  - `SignatureVersion` — from `AntiVirusProduct.SignatureLastUpdated` formatted as "YYYYMMDD"
  - `LastScan` — from last quick scan timestamp
- **Windows Update status** (UpdateStatus):
  - Service state via `ServiceController("wuauserv")`
  - `PendingReboot` — check registry `HKLM\Software\Microsoft\Windows\CurrentVersion\Component Based Servicing\SuccessStatus` or ` pending reboot` flag.

## 7. Extensibility Model (per original design)
- **IHealthCheckModule** — each module implements `Scan(List<HealthCheckItem>)` and exposes `Id` / `Title`.
- **IPcBoostService** — `GetMemorySnapshotAsync()`, `OptimizeMemoryAsync()`, `CleanTempFilesAsync()`.
- **IProcessService** — `GetProcessesAsync()`, `EndProcessAsync(int)`, `GetStartupAppsAsync()`, `SetStartupAppEnabledAsync(string, bool)`.
- **IStorageService** — `GetDrivesAsync()`, `ScanFolderAsync(string)`, `FindLargeFilesAsync(string, long)`, `RunStorageCleanupAsync()`, `GetStorageSenseEnabledAsync()`, `SetStorageSenseEnabledAsync(bool)`, `EmptyRecycleBinAsync()`.
- All services are registered in an `IServiceCollection` at startup (see `Microsoft.Extensions.DependencyInjection` usage in the original).