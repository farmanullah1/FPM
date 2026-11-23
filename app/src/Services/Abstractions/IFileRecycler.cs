using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Moves files to the Windows Recycle Bin through the shell, so a duplicate removal can be
/// undone by the user. Nothing is deleted permanently by this service.
/// </summary>
public interface IFileRecycler
{
    OperationResult Recycle(string path);
}
