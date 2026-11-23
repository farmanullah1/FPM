using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Toolbox;

/// <summary>
/// Store/MSIX inventory via the fixed Get-AppxPackage query. Removal is current-user only,
/// confirmation-gated by the UI, and refused unless the exact package is still in the
/// freshly queried list and is not a framework, resource, system or Microsoft PC Manager package.
/// </summary>
public sealed class AppxPackageService : IAppxPackageService
{
    private const string ListScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = [Text.Encoding]::UTF8
        Get-AppxPackage |
            Where-Object {
                -not $_.IsFramework -and
                -not $_.IsResourcePackage -and
                $_.SignatureKind -ne 'System'
            } |
            ForEach-Object {
                [pscustomobject]@{
                    Name = $_.Name
                    PackageFullName = $_.PackageFullName
                    Version = $_.Version.ToString()
                    Publisher = ($_.Publisher -replace '^CN=([^,]+).*$', '$1')
                    InstallLocation = $_.InstallLocation
                    SignatureKind = $_.SignatureKind.ToString()
                    IsFramework = [bool]$_.IsFramework
                    IsResourcePackage = [bool]$_.IsResourcePackage
                }
            } |
            ConvertTo-Json -Compress -Depth 3
        """;

    private const string UninstallScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = [Text.Encoding]::UTF8
        $package = $args[0]
        Remove-AppxPackage -Package $package
        Write-Output 'OK'
        """;

    public Task<IReadOnlyList<AppxPackage>> GetPackagesAsync(CancellationToken cancellationToken = default)
        => Task.Run(GetPackages, cancellationToken);

    public Task<OperationResult> UninstallAsync(
        AppxPackage package,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            try
            {
                if (!package.CanUninstall || string.IsNullOrWhiteSpace(package.PackageFullName))
                {
                    return OperationResult.Fail(
                        "That package cannot be removed by this app. Use Windows Settings.");
                }

                // Re-query immediately before removal. A stale list or a caller-supplied object
                // must never become an uninstall target on its own.
                IReadOnlyList<AppxPackage> current = GetPackages();
                AppxPackage? target = current.FirstOrDefault(candidate =>
                    candidate.PackageFullName.Equals(
                        package.PackageFullName,
                        StringComparison.OrdinalIgnoreCase));

                if (target is null || !target.CanUninstall)
                {
                    return OperationResult.Fail(
                        "The package is no longer installed, is protected, or the list changed. Refresh the list.");
                }

                ProcessResult result = ProcessRunner.RunPowerShell(
                    UninstallScript,
                    new[] { target.PackageFullName },
                    TimeSpan.FromMinutes(3));

                return result.Success
                    ? OperationResult.Ok($"Removed {target.Name} for the current user.")
                    : OperationResult.Fail("Windows did not remove the package: " + result.Message);
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }, cancellationToken);

    private static IReadOnlyList<AppxPackage> GetPackages()
    {
        ProcessResult result = ProcessRunner.RunPowerShell(
            ListScript,
            Array.Empty<string>(),
            TimeSpan.FromSeconds(90));

        if (!result.Success)
        {
            throw new InvalidOperationException(
                "Could not read Store/MSIX packages: " + result.Message);
        }

        return AppxPackageParser.Parse(result.StandardOutput);
    }
}
