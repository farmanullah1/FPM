namespace PCManager.Core.Models;

/// <summary>
/// Type of leftover remnant detected after an application uninstallation.
/// </summary>
public enum AppLeftoverKind
{
    Directory,
    RegistryKey,
    File
}

/// <summary>
/// One leftover file, folder, or registry remnant discovered for an uninstalled or targeted application.
/// </summary>
public sealed class AppLeftoverItem
{
    public AppLeftoverItem(
        string name,
        string targetPath,
        AppLeftoverKind kind,
        long sizeBytes,
        string formattedSize,
        bool isSafeToRemove,
        string safetyReason)
    {
        Name = name;
        TargetPath = targetPath;
        Kind = kind;
        SizeBytes = sizeBytes;
        FormattedSize = formattedSize;
        IsSafeToRemove = isSafeToRemove;
        SafetyReason = safetyReason;
        IsSelected = isSafeToRemove;
    }

    public string Name { get; }
    public string TargetPath { get; }
    public AppLeftoverKind Kind { get; }
    public long SizeBytes { get; }
    public string FormattedSize { get; }
    public bool IsSafeToRemove { get; }
    public string SafetyReason { get; }
    public bool IsSelected { get; set; }

    public string KindText => Kind switch
    {
        AppLeftoverKind.Directory => "Folder",
        AppLeftoverKind.RegistryKey => "Registry",
        _ => "File"
    };
}

/// <summary>
/// Result of scanning application leftovers across filesystem and registry.
/// </summary>
public sealed record AppLeftoverScanResult(
    string AppName,
    IReadOnlyList<AppLeftoverItem> Items,
    long TotalBytes,
    int RegKeyCount,
    bool FoundAny);
