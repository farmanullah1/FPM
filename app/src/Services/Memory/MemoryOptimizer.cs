using System.Diagnostics;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Interop;

namespace PCManager.Services.Memory;

/// <summary>
/// Working-set trimmer.
///
/// For each user process (session != 0, pid > 4) the optimiser opens a handle with
/// PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION and calls
/// SetProcessWorkingSetSizeEx(h, -1, -1). That asks the memory manager to page out the
/// process's least recently used pages, which is exactly what Task Manager's
/// "Empty working set" does. It cannot fail destructively: worst case Windows ignores
/// the request.
/// </summary>
public sealed class MemoryOptimizer : IMemoryOptimizer
{
    public MemorySnapshot GetSnapshot()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            return new MemorySnapshot();
        }

        return new MemorySnapshot
        {
            TotalPhysicalBytes = status.ullTotalPhys,
            AvailableBytes = status.ullAvailPhys,
            LoadPercent = status.dwMemoryLoad,
            CommitUsedBytes = status.ullTotalPageFile > status.ullAvailPageFile
                ? status.ullTotalPageFile - status.ullAvailPageFile
                : 0
        };
    }

    public MemorySnapshot Optimize()
    {
        MemorySnapshot before = GetSnapshot();
        int trimmed = 0;

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                // PIDs 0-4 are the idle/system processes; session 0 holds services,
                // and trimming those is both useless and risky.
                if (process.Id <= 4 || process.SessionId == 0)
                {
                    continue;
                }

                // Never trim ourselves or Microsoft PC Manager: trimming the working set
                // of either utility while it is being used would make it stutter.
                if (process.Id == Environment.ProcessId || AppInfo.IsProtectedProcessName(process.ProcessName))
                {
                    continue;
                }

                IntPtr handle = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_SET_QUOTA | NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
                    false,
                    process.Id);

                if (handle == IntPtr.Zero)
                {
                    // Access denied: a protected or elevated process. Skip it.
                    continue;
                }

                try
                {
                    // (SIZE_T)-1 for both limits = "trim to minimum".
                    if (NativeMethods.SetProcessWorkingSetSizeEx(handle, new IntPtr(-1), new IntPtr(-1), 0))
                    {
                        trimmed++;
                    }
                }
                finally
                {
                    NativeMethods.CloseHandle(handle);
                }
            }
            catch (Exception)
            {
                // Process exited while enumerating.
            }
            finally
            {
                process.Dispose();
            }
        }

        MemorySnapshot after = GetSnapshot();
        after.WorkingSetsTrimmed = trimmed;
        after.ReclaimedBytes = after.AvailableBytes > before.AvailableBytes
            ? (long)(after.AvailableBytes - before.AvailableBytes)
            : 0;

        return after;
    }
}
