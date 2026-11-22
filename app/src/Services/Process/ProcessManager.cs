using System.Diagnostics;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Processes;

/// <summary>
/// Process enumeration with CPU deltas and End Task support.
///
/// CPU usage cannot be read directly: Process.TotalProcessorTime is a cumulative
/// counter, so the optimiser keeps the previous value per PID and divides the
/// difference by (elapsed wall time * logical processors).
/// </summary>
public sealed class ProcessManager : IProcessManager
{
    /// <summary>Processes that must never be terminated from the UI.</summary>
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost"
    };

    private readonly Dictionary<int, (TimeSpan Cpu, DateTime SampleTime)> _samples = new();
    private readonly object _gate = new();

    public IReadOnlyList<ProcessInfo> GetProcesses()
    {
        var results = new List<ProcessInfo>();
        var alive = new HashSet<int>();
        DateTime now = DateTime.UtcNow;
        ulong totalRam = GetTotalRam();

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                int pid = process.Id;
                alive.Add(pid);

                string name = process.ProcessName;
                string path = string.Empty;
                int threads = 0;
                DateTime? startTime = null;
                long memory = 0;
                double cpu = 0;
                int sessionId;

                try { threads = process.Threads.Count; } catch { /* exiting */ }
                try { startTime = process.StartTime; } catch { /* access denied */ }
                try { memory = process.WorkingSet64; } catch { /* exiting */ }
                try { sessionId = process.SessionId; } catch { sessionId = 1; }

                try
                {
                    // MainModule throws for protected and cross-bitness processes.
                    path = process.MainModule?.FileName ?? string.Empty;
                }
                catch (Exception) { }

                try
                {
                    TimeSpan cpuTime = process.TotalProcessorTime;

                    lock (_gate)
                    {
                        if (_samples.TryGetValue(pid, out var previous))
                        {
                            double elapsedMs = (now - previous.SampleTime).TotalMilliseconds;
                            if (elapsedMs > 0)
                            {
                                double usedMs = (cpuTime - previous.Cpu).TotalMilliseconds;
                                cpu = Math.Clamp(usedMs / (elapsedMs * Environment.ProcessorCount) * 100.0, 0, 100);
                            }
                        }

                        _samples[pid] = (cpuTime, now);
                    }
                }
                catch (Exception) { }

                results.Add(new ProcessInfo
                {
                    ProcessId = pid,
                    Name = name,
                    Path = path,
                    CpuPercent = Math.Round(cpu, 1),
                    MemoryBytes = memory,
                    MemoryPercent = totalRam == 0 ? 0 : Math.Round((double)memory / totalRam * 100.0, 2),
                    StartTime = startTime,
                    ThreadCount = threads,
                    // Our own process and Microsoft PC Manager are flagged as system so the
                    // Apps page never offers an End button for them.
                    IsSystem = sessionId == 0 || ProtectedNames.Contains(name) || AppInfo.IsProtectedProcessName(name)
                });
            }
            catch (Exception)
            {
                // The process disappeared between GetProcesses and the property reads.
            }
            finally
            {
                process.Dispose();
            }
        }

        // Drop samples of processes that are gone so the dictionary cannot grow forever.
        lock (_gate)
        {
            foreach (int stale in _samples.Keys.Where(pid => !alive.Contains(pid)).ToList())
            {
                _samples.Remove(stale);
            }
        }

        return results
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.MemoryBytes)
            .ToList();
    }

    public OperationResult EndProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);

            if (process.Id <= 4 || ProtectedNames.Contains(process.ProcessName))
            {
                return OperationResult.Fail($"{process.ProcessName} is a critical system process and cannot be ended.");
            }

            if (process.Id == Environment.ProcessId)
            {
                return OperationResult.Fail($"{AppInfo.ProductName} cannot end its own process.");
            }

            if (AppInfo.IsProtectedProcessName(process.ProcessName))
            {
                return OperationResult.Fail($"{process.ProcessName} is protected. Microsoft PC Manager and {AppInfo.ProductName} are never terminated from here.");
            }

            if (process.SessionId == 0)
            {
                return OperationResult.Fail("This process runs in session 0 (services) and cannot be ended from here.");
            }

            string name = process.ProcessName;
            process.Kill(entireProcessTree: false);
            lock (_gate) { _samples.Remove(processId); }
            return OperationResult.Ok($"{name} (PID {processId}) was ended.");
        }
        catch (ArgumentException)
        {
            return OperationResult.Fail($"Process {processId} is no longer running.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return OperationResult.Fail($"Access denied: {ex.Message} Run PC Manager as administrator for elevated processes.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private static ulong GetTotalRam()
    {
        var status = new Interop.NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Interop.NativeMethods.MEMORYSTATUSEX>()
        };

        return Interop.NativeMethods.GlobalMemoryStatusEx(ref status) ? status.ullTotalPhys : 0;
    }
}
