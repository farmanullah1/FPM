using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Cleanup;

namespace PCManager.Services.Cleanup;

public sealed class UnifiedCleanupCoordinator : IUnifiedCleanupCoordinator
{
    private readonly CleanupScanner _scanner;
    private readonly CleanupExecutor _executor;
    private readonly IAppLogger _logger;
    private UnifiedScanReport? _lastReport;
    private CleanupScanResult? _lastScanResult;

    public UnifiedCleanupCoordinator(
        CleanupScanner scanner,
        CleanupExecutor executor,
        IAppLogger logger)
    {
        _scanner = scanner;
        _executor = executor;
        _logger = logger;
    }

    public async Task<UnifiedScanReport> RunFullScanAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info("Starting Unified Cleanup full scan across all categories.");

        CleanupScanResult scanResult = await _scanner.ScanAsync(cancellationToken);
        _lastScanResult = scanResult;

        var items = new List<UnifiedCleanupItem>();
        int number = 1;
        long total = 0;
        long safe = 0;
        long caution = 0;
        long optIn = 0;

        foreach (var candidate in scanResult.Items.OrderBy(c => c.Tier))
        {
            var item = new UnifiedCleanupItem(
                number++,
                candidate.Category,
                candidate.PathText,
                candidate.Bytes,
                candidate.DetailText,
                candidate.Tier,
                candidate.Rule.Id,
                candidate.Rule.RequiresElevation,
                candidate.Rule.RequiresAppClosed,
                candidate.Tier == CleanupTier.Safe);

            items.Add(item);
            total += candidate.Bytes;

            switch (candidate.Tier)
            {
                case CleanupTier.Safe:
                    safe += candidate.Bytes;
                    break;
                case CleanupTier.Caution:
                    caution += candidate.Bytes;
                    break;
                case CleanupTier.OptIn:
                    optIn += candidate.Bytes;
                    break;
            }
        }

        string scanId = Guid.NewGuid().ToString("N");
        _lastReport = new UnifiedScanReport(scanId, DateTime.UtcNow, items, total, safe, caution, optIn);
        _logger.Info($"Unified Cleanup scan finished with {items.Count} items ({total} bytes).");
        return _lastReport;
    }

    public async Task<UnifiedExecutionResult> ExecuteApprovedAsync(
        string scanId,
        IReadOnlyList<int> approvedItemNumbers,
        CancellationToken cancellationToken = default)
    {
        if (_lastReport is null || _lastReport.ScanId != scanId || _lastScanResult is null)
        {
            return new UnifiedExecutionResult(
                scanId, 0, 0, 0, 0,
                new[] { "Scan manifest expired or not found. Please rescan." },
                "Execution aborted: scan manifest expired.");
        }

        var approvedItems = _lastReport.Items
            .Where(i => approvedItemNumbers.Contains(i.ItemNumber))
            .ToList();

        if (approvedItems.Count == 0)
        {
            return new UnifiedExecutionResult(scanId, 0, 0, 0, 0, Array.Empty<string>(), "No items approved.");
        }

        _logger.Info($"Executing unified cleanup for {approvedItems.Count} approved items.");

        var planItems = new List<CleanupPlanItem>();
        foreach (var item in approvedItems)
        {
            var candidate = _lastScanResult.Items.FirstOrDefault(c => c.Rule.Id == item.RuleId);
            if (candidate is not null)
            {
                planItems.Add(new CleanupPlanItem(
                    item.ItemNumber,
                    candidate.Rule.Id,
                    candidate.Rule.Name,
                    candidate.Roots,
                    candidate.Bytes,
                    candidate.FileCount,
                    candidate.Rule.Tier,
                    candidate.Rule.Action,
                    candidate.Rule.MinimumAge,
                    candidate.Rule.RequiresElevation,
                    candidate.Rule.RequiresAppClosed,
                    candidate.Rule.ProcessName));
            }
        }

        var plan = new CleanupPlan
        {
            PlanId = _lastReport.ScanId,
            Items = planItems,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(15)
        };

        CleanupReport report = await _executor.ExecuteAsync(plan, dryRun: false, quarantine: false, cancellationToken: cancellationToken);

        var skippedReasons = report.Items
            .Where(i => i.Outcome != CleanupItemOutcome.Deleted)
            .Select(i => $"{i.Name}: {i.OutcomeText} - {i.Message}")
            .ToList();

        string summaryText = $"Successfully cleaned {report.DeletedItems} items, released {report.BytesDeleted / (1024 * 1024)} MB.";
        return new UnifiedExecutionResult(
            scanId,
            report.DeletedItems,
            report.SkippedItems,
            report.BytesDeleted,
            report.BytesDeleted,
            skippedReasons,
            summaryText);
    }
}
