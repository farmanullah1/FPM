using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Scans for and safely cleans leftover files, folders, and registry keys after application uninstall.
/// All file/directory cleanups send items to the Windows Recycle Bin. Registry cleanups create a .reg backup first.
/// </summary>
public interface IAppLeftoverScanner
{
    Task<AppLeftoverScanResult> ScanLeftoversAsync(string appName, string? publisher = null, CancellationToken cancellationToken = default);
    Task<OperationResult> ExportRegBackupAsync(IEnumerable<string> registryKeys, string backupFilePath, CancellationToken cancellationToken = default);
    Task<OperationResult> CleanLeftoversAsync(IEnumerable<AppLeftoverItem> items, bool createRegBackup = true, CancellationToken cancellationToken = default);
}
