using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

/// <summary>A set of files that are byte-for-byte identical.</summary>
public sealed class DuplicateGroup
{
    public long FileSize { get; init; }
    public List<string> Paths { get; init; } = new();

    /// <summary>The copy the UI keeps when the others are recycled (the oldest by write time).</summary>
    public string KeepPath { get; init; } = string.Empty;

    /// <summary>Bytes that would be released if all but one copy were removed.</summary>
    public long WasteBytes => FileSize * Math.Max(0, Paths.Count - 1);

    public string SizeText => ByteFormatter.Format(FileSize);
    public string WasteText => ByteFormatter.Format(WasteBytes);
    public string CountText => $"{Paths.Count} copies";
    public string DisplayName => Path.GetFileName(Paths.Count > 0 ? Paths[0] : string.Empty);
    public string KeepText => string.IsNullOrWhiteSpace(KeepPath) ? string.Empty : "Keep " + KeepPath;

    /// <summary>Every path except <see cref="KeepPath"/>.</summary>
    public IReadOnlyList<string> ExtraPaths => Paths
        .Where(path => !path.Equals(KeepPath, StringComparison.OrdinalIgnoreCase))
        .ToList();
}

/// <summary>Outcome of recycling the extra copies of one duplicate group.</summary>
public sealed record DuplicateRemovalResult(
    int MovedFiles,
    long MovedBytes,
    int SkippedFiles,
    IReadOnlyList<string> Messages)
{
    public bool AllMoved => MovedFiles > 0 && SkippedFiles == 0;

    public string Summary => MovedFiles == 0 && SkippedFiles == 0
        ? "Nothing was recycled."
        : $"Recycled {MovedFiles:N0} file(s) ({ByteFormatter.Format(MovedBytes)})." +
          (SkippedFiles > 0 ? $" {SkippedFiles:N0} file(s) skipped; the list below explains why." : string.Empty);
}

/// <summary>An installed Win32 (registry) or packaged (Store) application.</summary>
public sealed class InstalledApp
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string InstallLocation { get; init; } = string.Empty;

    /// <summary>Command line stored by the installer; empty for Store apps.</summary>
    public string UninstallCommand { get; init; } = string.Empty;

    /// <summary>True for MSIX/Store packages, which are removed through the Settings app.</summary>
    public bool IsStoreApp { get; init; }

    public long SizeBytes { get; init; }

    public string SizeText => SizeBytes > 0 ? ByteFormatter.Format(SizeBytes) : "-";

    public string Subtitle => string.IsNullOrWhiteSpace(Publisher)
        ? (IsStoreApp ? "Store app" : "Desktop app")
        : Publisher;
}
