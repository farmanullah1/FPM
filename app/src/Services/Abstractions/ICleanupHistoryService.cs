using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Append-only audit history under the app's own data folder; never a system folder.</summary>
public interface ICleanupHistoryService
{
    string HistoryFilePath { get; }

    IReadOnlyList<CleanupHistoryEntry> GetRecent(int maximum = 20);

    void Add(CleanupReport report);
}

/// <summary>Writes a completed report as Markdown or CSV to the app's reports folder.</summary>
public interface ICleanupReportExporter
{
    string ReportsFolder { get; }

    string ExportMarkdown(CleanupReport report);

    string ExportCsv(CleanupReport report);
}
