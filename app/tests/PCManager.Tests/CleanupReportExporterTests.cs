using PCManager.Core.Models;
using PCManager.Services.Cleanup;
using Xunit;

namespace PCManager.Tests;

public sealed class CleanupReportExporterTests
{
    [Fact]
    public void Exports_markdown_with_measured_totals_and_items()
    {
        string folder = TemporaryFolder();

        try
        {
            var exporter = new CleanupReportExporter(new TestLogger(), folder);
            string path = exporter.ExportMarkdown(Report());

            Assert.True(File.Exists(path));
            Assert.EndsWith(".md", path, StringComparison.OrdinalIgnoreCase);

            string text = File.ReadAllText(path);
            Assert.Contains("Farmanullah PC Manager cleanup report", text, StringComparison.Ordinal);
            Assert.Contains("plan-1234", text, StringComparison.Ordinal);
            Assert.Contains("PARTIAL", text, StringComparison.Ordinal);
            Assert.Contains("Space released: 1.5 KB", text, StringComparison.Ordinal);
            Assert.Contains("C: 10 GB → 12 GB free", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Exports_csv_and_escapes_quotes()
    {
        string folder = TemporaryFolder();

        try
        {
            var exporter = new CleanupReportExporter(new TestLogger(), folder);
            string path = exporter.ExportCsv(Report());

            Assert.True(File.Exists(path));
            Assert.EndsWith(".csv", path, StringComparison.OrdinalIgnoreCase);

            string text = File.ReadAllText(path);
            string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.StartsWith("Number,Item,Outcome", lines[0], StringComparison.Ordinal);
            Assert.Contains("\"a \"\"quoted\"\" item\"", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static CleanupReport Report()
        => new()
        {
            PlanId = "plan-1234567890",
            CompletedUtc = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc),
            EstimatedBytes = 2048,
            BytesDeleted = 1536,
            FilesDeleted = 2,
            FilesSkipped = 1,
            Duration = TimeSpan.FromSeconds(2.5),
            DriveChanges = new[] { "C: 10 GB → 12 GB free (+2 GB)" },
            Summary = "Deleted 1 of 1 item(s). Space released: 1.5 KB.",
            Items = new[]
            {
                new CleanupItemResult
                {
                    Number = 1,
                    Name = "a \"quoted\" item",
                    PathText = @"C:\Temp",
                    BytesDeleted = 1536,
                    FilesDeleted = 2,
                    FilesSkipped = 1,
                    Outcome = CleanupItemOutcome.PartiallyDeleted,
                    Message = "1.5 KB removed; 1 skipped."
                }
            }
        };

    private static string TemporaryFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "PCManagerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
