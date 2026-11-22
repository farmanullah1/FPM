using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Append-only cleanup audit log at %LOCALAPPDATA%\FarmanullahPCManager\logs\cleanup_log.txt,
/// rotated at 5 MB. One line per item: timestamp, number, path, size and result.
/// </summary>
public interface ICleanupLog
{
    string FilePath { get; }

    void Write(CleanupPlanItem item, CleanupItemOutcome outcome, long bytes, string detail);

    void WriteLine(string message);
}
