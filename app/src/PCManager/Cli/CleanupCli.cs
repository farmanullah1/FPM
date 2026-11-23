using System.IO;
using System.Runtime.InteropServices;
using PCManager.Core;
using PCManager.Core.Elevation;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Cli;

/// <summary>
/// Command-line access to the same two-phase engine as the UI:
/// <code>
/// FPM.exe --scan
/// FPM.exe --clean --ids 1,2
/// FPM.exe --clean --ids 1,2 --dry-run
/// FPM.exe --clean --ids 1,2 --restore-point
/// FPM.exe --clean --ids 1,2 --quarantine
/// FPM.exe --auto-clean
/// </code>
/// <c>--scan</c> deletes nothing. <c>--clean</c> requires an explicit id list, so a typo
/// cannot approve everything. Output goes to the parent console when one exists.
/// </summary>
internal static class CleanupCli
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    public static bool IsCleanupCommand(string[] args)
        => args.Any(arg =>
            arg.Equals("--scan", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--clean", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--auto-clean", StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RunAsync(
        string[] args,
        ICleanupScanner scanner,
        ICleanupCommandParser parser,
        ICleanupExecutor executor,
        ICleanupLog log,
        IRestorePointService restorePoint)
    {
        TryAttachConsole();

        bool dryRun = HasFlag(args, "--dry-run");
        bool quarantine = HasFlag(args, "--quarantine");
        bool clean = HasFlag(args, "--clean") || HasFlag(args, "--auto-clean");
        bool autoClean = HasFlag(args, "--auto-clean");

        try
        {
            Console.WriteLine($"{AppInfo.ProductName} cleanup engine");
            Console.WriteLine("Phase 1: read-only scan. Nothing is deleted by --scan.");

            CleanupScanResult scan = await scanner.ScanAsync();
            PrintScan(scan);

            if (!clean)
            {
                Console.WriteLine();
                Console.WriteLine("Nothing was deleted. To clean, review the numbers and run:");
                Console.WriteLine($"  {AppInfo.ProductName}.exe --clean --ids 1,2   (add --dry-run, --quarantine or --restore-point)");
                return 0;
            }

            string command;
            string? ids = GetOption(args, "--ids");

            if (autoClean)
            {
                command = "proceed";
            }
            else if (ids is not null)
            {
                command = $"only {ids}";
            }
            else
            {
                Console.WriteLine("--clean needs an explicit --ids list (for example --ids 1,2).");
                return 2;
            }

            CleanupCommandResult approval = parser.Parse(command, scan);

            if (approval.Plan is null)
            {
                Console.WriteLine("Refused: " + approval.Message);
                return 2;
            }

            Console.WriteLine(approval.Message);

            if (HasFlag(args, "--restore-point") && !dryRun)
            {
                Console.WriteLine("Creating a System Restore point...");
                ElevationOutcome restore = await restorePoint.CreateAsync();
                Console.WriteLine(restore.Success
                    ? restore.Message
                    : "Restore point not created: " + restore.Message + " Continuing.");
            }

            Console.WriteLine(dryRun ? "Dry run: no file will be removed." : "Executing the approved plan...");

            CleanupReport report = await executor.ExecuteAsync(approval.Plan, dryRun, quarantine);
            Console.WriteLine(report.Summary);

            foreach (CleanupItemResult item in report.Items)
            {
                Console.WriteLine(
                    $"  {item.OutcomeText,-18} #{item.Number} {item.Name}" +
                    (item.BytesDeleted > 0 ? $" ({ByteFormatter.Format(item.BytesDeleted)})" : string.Empty) +
                    (item.BytesQuarantined > 0 ? $" (quarantined {ByteFormatter.Format(item.BytesQuarantined)})" : string.Empty));
            }

            Console.WriteLine($"Log: {log.FilePath}");

            return report.Expired ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Cleanup CLI failed: " + ex.Message);
            return 1;
        }
    }

    private static void PrintScan(CleanupScanResult scan)
    {
        Console.WriteLine();
        Console.WriteLine($"{"#",-4}{"Size",11}  {"Tier",-8}{"Category",-24}Name");

        foreach (CleanupCandidate item in scan.Items)
        {
            Console.WriteLine(
                $"{item.Number,-4}{item.SizeText,11}  {item.TierText,-8}{Trim(item.Category, 23),-24}{item.Title}");

            foreach (string root in item.Roots)
            {
                Console.WriteLine($"      {root}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total measured: {ByteFormatter.Format(scan.Items.Sum(item => item.Bytes))} " +
                          $"across {scan.Items.Count} row(s).");

        foreach (string note in scan.Notes)
        {
            Console.WriteLine("Note: " + note);
        }
    }

    private static string Trim(string value, int length)
        => value.Length <= length ? value : value[..(length - 1)] + "…";

    private static bool HasFlag(string[] args, string flag)
        => args.Any(arg => arg.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static void TryAttachConsole()
    {
        try
        {
            // A GUI-subsystem executable starts without a console. Attach to the caller's
            // when there is one; when stdout is already redirected, the inherited handle
            // works without attaching.
            AttachConsole(AttachParentProcess);
        }
        catch
        {
            // No parent console: the process still runs and writes the cleanup log.
        }

        try
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(stdout);
        }
        catch
        {
            // Output is not available; the cleanup log still records every action.
        }
    }
}
