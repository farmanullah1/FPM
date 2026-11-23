using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Recycles the extra copies of a duplicate group. Every candidate is re-hashed with full
/// SHA-256 immediately before the shell move, whitelisted or protected paths are refused, and
/// the kept copy is never touched.
/// </summary>
public interface IDuplicateRemovalService
{
    Task<DuplicateRemovalResult> MoveExtrasToRecycleBinAsync(
        DuplicateGroup group,
        string keepPath,
        CancellationToken cancellationToken = default);
}
