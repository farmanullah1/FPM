using System.IO;
using System.Runtime.InteropServices;
using PCManager.Core;
using PCManager.Core.Debloat;
using PCManager.Services.Debloat;

namespace PCManager.Cli;

/// <summary>
/// Debloat CLI, sharing the exact engine and journal as the page.
/// Exit codes: 0 applied (or dry-run plan ready); 1 failure/partial rollback;
/// 2 usage, unknown ids, app removal refused, or confirmation declined;
/// 3 administrator permission declined; 4 nothing runnable.
/// </summary>
internal static class DebloatCli
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    public static async Task<int> RunAsync(
        string[] args,
        DebloatEngine engine,
        IDebloatProfileService profiles)
    {
        TryAttachConsole();
        Console.WriteLine($"{AppInfo.ProductName} debloat engine");

        DebloatCommandParse parse = DebloatCommandLine.Parse(args);

        if (!parse.Success)
        {
            Console.Error.WriteLine("Refused: " + parse.Error);
            PrintUsage();
            return 2;
        }

        DebloatCommandOptions options = parse.Options!;
        DebloatCatalog catalog = DebloatCatalogLoader.LoadEmbedded();

        if (options.Apps.Count > 0)
        {
            Console.Error.WriteLine(
                "Refused: app removal is not enabled. Appx changes stay blocked until FPM can capture " +
                "and journal package state. Remove --apps to preview or apply tweaks only.");
            return 2;
        }

        DebloatTargetResolution target = profiles.Resolve(options.User);

        if (target.Target is not { } resolved)
        {
            Console.Error.WriteLine("Refused: " + target.Message);
            return 2;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        if (options.Preset is not null)
        {
            foreach (string id in DebloatPresetCatalog.Select(catalog, options.Preset))
            {
                ids.Add(id);
            }
        }

        var unknown = new List<string>();

        foreach (string id in options.Tweaks)
        {
            if (catalog.Tweaks.Any(tweak => tweak.Id.Equals(id, StringComparison.Ordinal)))
            {
                ids.Add(id);
            }
            else
            {
                unknown.Add(id);
            }
        }

        if (unknown.Count > 0)
        {
            Console.Error.WriteLine("Refused: unknown tweak id(s): " + string.Join(", ", unknown));
            return 2;
        }

        if (ids.Count == 0)
        {
            Console.Error.WriteLine("Refused: --preset custom needs --tweaks with catalog ids.");
            return 2;
        }

        int build = Environment.OSVersion.Version.Build;
        bool isWindows11 = build >= 22000;
        DebloatPlan plan = engine.Plan(ids, build, isWindows11);
        int ready = plan.Items.Count(item => item.IsRunnable);

        Console.WriteLine($"Target: {resolved.DisplayName}");

        if (!string.IsNullOrWhiteSpace(resolved.Warning))
        {
            Console.WriteLine("Warning: " + resolved.Warning);
        }

        Console.WriteLine($"Plan: {plan.Items.Count} selected, {ready} ready, {plan.Items.Count - ready} skipped.");

        foreach (DebloatPlanItem item in plan.Items)
        {
            Console.WriteLine(
                $"  {item.Tweak.Id,-55} {item.Tweak.Risk,-6} " +
                (item.IsRunnable ? "ready" : "skipped: " + item.Reason));
        }

        if (options.DryRun)
        {
            Console.WriteLine("Dry run: no elevation, no registry write, no journal or backup file.");
            return ready > 0 ? 0 : 4;
        }

        if (ready == 0)
        {
            Console.WriteLine("Nothing to apply.");
            return 4;
        }

        if (!options.Silent)
        {
            Console.Write($"Apply {ready} tweak(s) to {resolved.DisplayName}? [y/N] ");
            string? answer = Console.ReadLine();

            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled; nothing was changed.");
                return 2;
            }
        }

        DebloatRunRecord record = await engine.ApplyAsync(
            ids,
            build,
            isWindows11,
            createRestorePoint: true,
            targetUser: options.User);

        foreach (DebloatJournalEntry entry in record.Entries)
        {
            Console.WriteLine($"  applied  {entry.Title}");
        }

        if (record.RolledBack)
        {
            Console.Error.WriteLine("Failed and partially rolled back: " + record.FailureMessage);
            Console.Error.WriteLine("Journal: " + Path.Combine(AppInfo.DataFolder, "debloat", "journal"));
            return 1;
        }

        if (!record.Completed)
        {
            Console.Error.WriteLine("Stopped: " + record.FailureMessage);
            return record.FailureMessage.Contains("declined", StringComparison.OrdinalIgnoreCase) ? 3 : 1;
        }

        Console.WriteLine($"Completed: {record.Entries.Count} applied. Restore point: {record.RestorePointStatus}.");
        Console.WriteLine("Journal: " + Path.Combine(AppInfo.DataFolder, "debloat", "journal"));
        Console.WriteLine("Undo from the Debloat page or pass the same target after review.");
        return record.Entries.Count > 0 ? 0 : 4;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  FPM.exe --debloat --preset <recommended|privacy|performance|minimal|custom> [options]");
        Console.WriteLine("  FPM.exe --debloat --tweaks <id,id> [options]");
        Console.WriteLine("Options: --user <name|SID|default>  --dry-run  --silent --accept-risk");
        Console.WriteLine("--apps is reserved for the Apps flow; app removal is refused in this build.");
    }

    private static void TryAttachConsole()
    {
        try
        {
            AttachConsole(AttachParentProcess);
        }
        catch
        {
            // No parent console; the engine still logs.
        }

        try
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(stdout);
        }
        catch
        {
            // Output is not available.
        }
    }
}
