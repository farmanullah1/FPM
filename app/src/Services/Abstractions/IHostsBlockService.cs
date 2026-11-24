using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Reads the FPM section of the Windows hosts file and asks the elevated helper to replace or
/// remove it. The helper writes a timestamped backup before touching the file.
/// </summary>
public interface IHostsBlockService
{
    HostsBlockState GetState();

    Task<OperationResult> ApplyAsync(string domainsText, CancellationToken cancellationToken = default);

    Task<OperationResult> RemoveAsync(CancellationToken cancellationToken = default);
}
