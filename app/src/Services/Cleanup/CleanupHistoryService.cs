using System.Text.Json;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Cleanup;

/// <summary>
/// Keeps the last 50 completed runs in `cleanup_history.json`. History is advisory: a read or
/// write failure is logged and never prevents cleanup from running.
/// </summary>
public sealed class CleanupHistoryService : ICleanupHistoryService
{
    private const int MaximumEntries = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IAppLogger _logger;
    private readonly object _gate = new();

    public CleanupHistoryService(IAppLogger logger, string? historyFilePath = null)
    {
        _logger = logger;
        HistoryFilePath = historyFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FarmanullahPCManager",
            "cleanup_history.json");
    }

    public string HistoryFilePath { get; }

    public IReadOnlyList<CleanupHistoryEntry> GetRecent(int maximum = 20)
    {
        lock (_gate)
        {
            return Read().Take(Math.Max(0, maximum)).ToList();
        }
    }

    public void Add(CleanupReport report)
    {
        lock (_gate)
        {
            try
            {
                var entries = Read();
                entries.Insert(0, new CleanupHistoryEntry(
                    report.CompletedUtc,
                    report.PlanId,
                    report.DryRun,
                    report.EstimatedBytes,
                    report.BytesDeleted,
                    report.FilesDeleted,
                    report.FilesSkipped,
                    report.Summary));

                string? directory = Path.GetDirectoryName(HistoryFilePath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temporary = HistoryFilePath + ".tmp";
                File.WriteAllText(
                    temporary,
                    JsonSerializer.Serialize(entries.Take(MaximumEntries), JsonOptions));
                File.Move(temporary, HistoryFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.Error("Could not write cleanup history.", ex);
            }
        }
    }

    private List<CleanupHistoryEntry> Read()
    {
        try
        {
            if (!File.Exists(HistoryFilePath))
            {
                return new List<CleanupHistoryEntry>();
            }

            string json = File.ReadAllText(HistoryFilePath);
            return JsonSerializer.Deserialize<List<CleanupHistoryEntry>>(json, JsonOptions) ??
                   new List<CleanupHistoryEntry>();
        }
        catch (Exception ex)
        {
            _logger.Error("Could not read cleanup history; starting a new list.", ex);
            return new List<CleanupHistoryEntry>();
        }
    }
}
