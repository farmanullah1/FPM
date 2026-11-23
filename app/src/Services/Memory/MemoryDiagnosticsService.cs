using System.Diagnostics;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Interop;

namespace PCManager.Services.Memory;

public sealed class MemoryDiagnosticsService : IMemoryDiagnosticsService, IMemoryReliefService
{
    private readonly IAppLogger _logger;

    public MemoryDiagnosticsService(IAppLogger logger)
    {
        _logger = logger;
    }

    public ExtendedMemorySnapshot GetExtendedSnapshot()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            return new ExtendedMemorySnapshot(
                0, 0, 0, 0, 0,
                MemoryPressureLevel.Healthy,
                DateTime.UtcNow,
                Array.Empty<ProcessMemoryItem>());
        }

        uint load = status.dwMemoryLoad;
        MemoryPressureLevel pressure = load >= 85
            ? MemoryPressureLevel.High
            : (load >= 70 ? MemoryPressureLevel.Moderate : MemoryPressureLevel.Healthy);

        var topConsumers = GetEligibleProcesses()
            .OrderByDescending(p => p.WorkingSetBytes)
            .Take(10)
            .ToList();

        return new ExtendedMemorySnapshot(
            status.ullTotalPhys,
            status.ullAvailPhys,
            load,
            status.ullTotalPageFile - status.ullAvailPageFile,
            status.ullTotalPageFile,
            pressure,
            DateTime.UtcNow,
            topConsumers);
    }

    public IReadOnlyList<ProcessMemoryItem> GetEligibleProcesses()
    {
        var list = new List<ProcessMemoryItem>();

        foreach (Process proc in Process.GetProcesses())
        {
            try
            {
                if (proc.Id <= 4 || proc.SessionId == 0)
                {
                    continue;
                }

                bool isProtected = AppInfo.IsProtectedProcessName(proc.ProcessName) ||
                                  proc.Id == Environment.ProcessId;

                long ws = 0;
                long priv = 0;

                try
                {
                    ws = proc.WorkingSet64;
                    priv = proc.PrivateMemorySize64;
                }
                catch
                {
                    // Access denied on some elevated or system processes
                }

                // Can relieve only non-protected GUI / user applications
                bool canRelieve = !isProtected && proc.MainWindowHandle != IntPtr.Zero;

                list.Add(new ProcessMemoryItem(
                    proc.Id,
                    proc.ProcessName,
                    ws,
                    priv,
                    isProtected,
                    canRelieve));
            }
            catch
            {
                // Process terminated during enumeration
            }
            finally
            {
                proc.Dispose();
            }
        }

        return list;
    }

    public async Task<MemoryReliefResult> ExecuteReliefPlanAsync(MemoryReliefPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.ProcessIdsToClose.Count == 0)
        {
            return new MemoryReliefResult(0, 0, 0, 0, Array.Empty<string>(), "No processes selected for relief.");
        }

        int attempted = 0;
        int succeeded = 0;
        int failed = 0;
        long bytesFreed = 0;
        var closedNames = new List<string>();

        foreach (int pid in plan.ProcessIdsToClose)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempted++;

            try
            {
                using var proc = Process.GetProcessById(pid);

                if (AppInfo.IsProtectedProcessName(proc.ProcessName) || proc.Id == Environment.ProcessId)
                {
                    failed++;
                    _logger.Warn($"Skipped protected process {proc.ProcessName} (PID {pid}) during memory relief.");
                    continue;
                }

                string name = proc.ProcessName;
                long ws = 0;
                try { ws = proc.WorkingSet64; } catch { }

                bool closed = false;
                if (plan.GracefulCloseFirst && proc.MainWindowHandle != IntPtr.Zero)
                {
                    proc.CloseMainWindow();
                    closed = await Task.Run(() => proc.WaitForExit(3000), cancellationToken);
                }

                if (!closed)
                {
                    proc.Kill();
                    closed = await Task.Run(() => proc.WaitForExit(2000), cancellationToken);
                }

                if (closed)
                {
                    succeeded++;
                    bytesFreed += ws;
                    closedNames.Add(name);
                    _logger.Info($"Successfully relieved process {name} (PID {pid}), estimated space: {ws} bytes.");
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.Warn($"Failed to close process PID {pid}: {ex.Message}");
            }
        }

        string summary = $"Closed {succeeded} of {attempted} applications, releasing ~{bytesFreed / (1024 * 1024)} MB of memory.";
        return new MemoryReliefResult(attempted, succeeded, failed, bytesFreed, closedNames, summary);
    }
}
