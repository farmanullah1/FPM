namespace PCManager.Core.Models;

/// <summary>
/// One installed MSIX/Store package reported by Get-AppxPackage. Framework, resource and
/// system packages are filtered before they reach the UI; Microsoft PC Manager is never
/// uninstalled by this app.
/// </summary>
public sealed class AppxPackage
{
    public string Name { get; init; } = string.Empty;
    public string PackageFullName { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string InstallLocation { get; init; } = string.Empty;
    public string SignatureKind { get; init; } = string.Empty;
    public bool IsFramework { get; init; }
    public bool IsResourcePackage { get; init; }

    public bool IsSystem => SignatureKind.Equals("System", StringComparison.OrdinalIgnoreCase);

    public bool IsMicrosoftPcManager =>
        Name.Contains("MicrosoftPCManager", StringComparison.OrdinalIgnoreCase) ||
        PackageFullName.Contains("MicrosoftPCManager", StringComparison.OrdinalIgnoreCase);

    public bool CanUninstall =>
        !IsFramework &&
        !IsResourcePackage &&
        !IsSystem &&
        !IsMicrosoftPcManager &&
        !string.IsNullOrWhiteSpace(PackageFullName);

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Publisher))
            {
                parts.Add(Publisher);
            }

            if (!string.IsNullOrWhiteSpace(Version))
            {
                parts.Add("v" + Version);
            }

            return parts.Count == 0 ? "MSIX package" : string.Join(" · ", parts);
        }
    }
}
