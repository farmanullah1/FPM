using PCManager.Core.Formatting;

namespace PCManager.Core.Models;

/// <summary>One selectable row of the Storage page's deep cleanup.</summary>
public sealed class CleanupCategory
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>Bytes that were found by the scan.</summary>
    public long Bytes { get; set; }

    /// <summary>False for categories Windows will not let an unelevated process touch.</summary>
    public bool CanClean { get; set; } = true;

    public bool IsSelected { get; set; } = true;

    public bool IsCleaned { get; set; }

    public string SizeText => ByteFormatter.Format(Bytes);
}

/// <summary>Result of a deep cleanup run.</summary>
public sealed record CleanupOutcome(long FreedBytes, int Cleaned, int Skipped, string Message);

/// <summary>A set of files that are byte-for-byte identical.</summary>
public sealed class DuplicateGroup
{
    public long FileSize { get; init; }
    public List<string> Paths { get; init; } = new();

    /// <summary>Bytes that would be released if all but one copy were removed.</summary>
    public long WasteBytes => FileSize * Math.Max(0, Paths.Count - 1);

    public string SizeText => ByteFormatter.Format(FileSize);
    public string WasteText => ByteFormatter.Format(WasteBytes);
    public string CountText => $"{Paths.Count} copies";
    public string DisplayName => Path.GetFileName(Paths.Count > 0 ? Paths[0] : string.Empty);
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
