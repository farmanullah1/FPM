using System.Text;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Cleanup;

/// <summary>
/// Human-readable exports of the same measured report shown in the app. Files are written only
/// under %LOCALAPPDATA%\FarmanullahPCManager\reports; no Save dialog can redirect them elsewhere.
/// </summary>
public sealed class CleanupReportExporter : ICleanupReportExporter
{
    private readonly IAppLogger _logger;

    public CleanupReportExporter(IAppLogger logger, string? reportsFolder = null)
    {
        _logger = logger;
        ReportsFolder = reportsFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FarmanullahPCManager",
            "reports");
    }

    public string ReportsFolder { get; }

    public string ExportMarkdown(CleanupReport report)
    {
        var text = new StringBuilder();

        text.AppendLine("# Farmanullah PC Manager cleanup report");
        text.AppendLine();
        text.AppendLine($"- Plan: `{report.PlanId}`");
        text.AppendLine($"- Completed: {report.CompletedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        text.AppendLine($"- Mode: {(report.DryRun ? "Dry run (nothing deleted)" : "Executed")}");
        text.AppendLine($"- Estimated: {ByteFormatter.Format(report.EstimatedBytes)}");
        text.AppendLine($"- Space released: {ByteFormatter.Format(report.BytesDeleted)}");
        text.AppendLine($"- Files removed: {report.FilesDeleted:N0}");
        text.AppendLine($"- Files skipped: {report.FilesSkipped:N0}");
        text.AppendLine($"- Duration: {report.Duration.TotalSeconds:0.0} s");
        text.AppendLine();
        text.AppendLine("## Summary");
        text.AppendLine();
        text.AppendLine(report.Summary);
        text.AppendLine();
        text.AppendLine("## Items");
        text.AppendLine();
        text.AppendLine("| # | Item | Outcome | Removed | Files | Message |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- |");

        foreach (CleanupItemResult item in report.Items)
        {
            text.AppendLine(
                $"| {item.Number} | {EscapeMarkdown(item.Name)} | {item.OutcomeText} | " +
                $"{ByteFormatter.Format(item.BytesDeleted)} | {item.FilesDeleted:N0} | " +
                $"{EscapeMarkdown(item.Message)} |");
        }

        if (report.DriveChanges.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Drive free space");
            text.AppendLine();

            foreach (string change in report.DriveChanges)
            {
                text.AppendLine("- " + change);
            }
        }

        return Write(report, "md", text.ToString());
    }

    public string ExportCsv(CleanupReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("Number,Item,Outcome,BytesDeleted,FilesDeleted,FilesSkipped,Message");

        foreach (CleanupItemResult item in report.Items)
        {
            text.Append(item.Number).Append(',')
                .Append(Csv(item.Name)).Append(',')
                .Append(Csv(item.OutcomeText)).Append(',')
                .Append(item.BytesDeleted).Append(',')
                .Append(item.FilesDeleted).Append(',')
                .Append(item.FilesSkipped).Append(',')
                .Append(Csv(item.Message)).AppendLine();
        }

        return Write(report, "csv", text.ToString());
    }

    private string Write(CleanupReport report, string extension, string content)
    {
        Directory.CreateDirectory(ReportsFolder);

        string stamp = report.CompletedUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss");
        string plan = report.PlanId.Length >= 8 ? report.PlanId[..8] : report.PlanId;
        string path = Path.Combine(ReportsFolder, $"cleanup-{stamp}-{plan}.{extension}");

        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _logger.Info($"Cleanup report exported to {path}.");

        return path;
    }

    private static string EscapeMarkdown(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private static string Csv(string value)
        => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
