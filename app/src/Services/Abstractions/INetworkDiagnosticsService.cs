using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Network connectivity diagnostics, adapter details, DNS flush, and security scanning trigger.
/// </summary>
public interface INetworkDiagnosticsService
{
    Task<NetworkDiagnosticsSummary> RunDiagnosticsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> FlushDnsCacheAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> StartQuickVirusScanAsync(CancellationToken cancellationToken = default);
}
