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

        CleanupScanManifest manifest = await _scanner.ScanAsync(cancellationToken);

        var items = new List<UnifiedCleanupItem>();
        int number = 1;
        long total = 0;
        long safe = 0;
        long caution = 0;
        long optIn = 0;

        foreach (var candidate in manifest.Candidates.OrderBy(c => c.SafetyTier))
        {
            var item = new UnifiedCleanupItem(
                number++,
                candidate.Category,
                candidate.Path,
                candidate.SizeBytes,
                candidate.Description,
                candidate.SafetyTier,
                candidate.RuleId,
                candidate.RequiresElevation,
                candidate.RequiresProcessClosed,
                candidate.SafetyTier == CleanupSafetyTier.Safe);

            items.Add(item);
            total += candidate.SizeBytes;

            switch (candidate.SafetyTier)
            {
                case CleanupSafetyTier.Safe:
                    safe += candidate.SizeBytes;
                    break;
                case CleanupSafetyTier.Caution:
                    caution += candidate.SizeBytes;
                    break;
                case CleanupSafetyTier.OptIn:
                    optIn += candidate.SizeBytes;
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
        if (_lastReport is null || _lastReport.ScanId != scanId)
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

        // Construct plan for existing CleanupExecutor
        var approvedRuleIds = approvedItems.Select(i => i.RuleId).Distinct().ToList();
        var plan = new CleanupApprovedPlan(
            _lastReport.ScanId,
            approvedRuleIds,
            approvedItems.Sum(i => i.SizeBytes),
            DateTime.UtcNow.AddMinutes(15));

        CleanupExecutionSummary summary = await _executor.ExecuteAsync(plan, cancellationToken: cancellationToken);

        string summaryText = $"Successfully cleaned {summary.TargetsCleaned} items, released {summary.BytesFreed / (1024 * 1024)} MB.";
        return new UnifiedExecutionResult(
            scanId,
            summary.TargetsCleaned,
            summary.TargetsSkipped,
            summary.BytesFreed,
            summary.BytesFreed,
            summary.SkippedReasons,
            summaryText);
    }
}
