using System.IO.Compression;
using System.Security.Cryptography;
using PCManager.Core;
using PCManager.Core.Android;

namespace PCManager.Services.Android;

public interface IAdbPackageSource
{
    Task DownloadAsync(string url, string destination, CancellationToken cancellationToken = default);

    Task ExtractPlatformToolsAsync(
        string archivePath,
        string destination,
        CancellationToken cancellationToken = default);
}

public interface IAdbProvisioner
{
    Task<AdbProvisionResult> InstallAsync(
        bool consented,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The only download path for platform-tools. Consent is checked before the source is touched;
/// SHA-256 is checked before extraction; Google's Authenticode signature is checked before the
/// staged files replace the target directory.
/// </summary>
public sealed class AdbProvisioner(
    IAdbPackageSource source,
    IAdbSignatureVerifier signatureVerifier,
    string toolsDirectory,
    string? expectedSha256 = null)
    : IAdbProvisioner
{
    private readonly string _toolsDirectory = toolsDirectory;
    private readonly string _expectedSha256 = expectedSha256 ?? AdbPackage.Sha256;

    public AdbProvisioner(
        IAdbPackageSource source,
        IAdbSignatureVerifier signatureVerifier)
        : this(
            source,
            signatureVerifier,
            Path.Combine(AppInfo.DataFolder, "tools"))
    {
    }

    public async Task<AdbProvisionResult> InstallAsync(
        bool consented,
        CancellationToken cancellationToken = default)
    {
        if (!consented)
        {
            return new AdbProvisionResult(
                AdbStatusKind.Declined,
                null,
                "No consent was given; no download was attempted.");
        }

        string work = Path.Combine(
            Path.GetTempPath(),
            "fpm-adb-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(work, AdbPackage.ArchiveName);
        string staging = Path.Combine(work, "platform-tools");

        try
        {
            Directory.CreateDirectory(work);
            await source.DownloadAsync(AdbPackage.DownloadUrl, archive, cancellationToken)
                .ConfigureAwait(false);

            string actual = await HashAsync(archive, cancellationToken).ConfigureAwait(false);

            if (!actual.Equals(_expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new AdbProvisionResult(
                    AdbStatusKind.ChecksumMismatch,
                    null,
                    $"Platform-tools SHA-256 was {actual}; expected {_expectedSha256}.");
            }

            Directory.CreateDirectory(staging);
            await source.ExtractPlatformToolsAsync(archive, staging, cancellationToken)
                .ConfigureAwait(false);

            string stagedAdb = Path.Combine(staging, "adb.exe");
            if (!File.Exists(stagedAdb) || !signatureVerifier.IsGoogleSigned(stagedAdb))
            {
                return new AdbProvisionResult(
                    AdbStatusKind.Unverified,
                    null,
                    "The extracted adb.exe is not Google-signed; nothing was installed.");
            }

            string target = Path.Combine(_toolsDirectory, "platform-tools");
            Directory.CreateDirectory(_toolsDirectory);
            ReplaceDirectory(staging, target);

            string installed = Path.Combine(target, "adb.exe");
            return new AdbProvisionResult(
                AdbStatusKind.Ready,
                installed,
                $"Installed Google platform-tools {AdbPackage.Version} at {target}.");
        }
        catch (OperationCanceledException)
        {
            return new AdbProvisionResult(
                AdbStatusKind.DownloadFailed,
                null,
                "The platform-tools download was cancelled.");
        }
        catch (Exception ex)
        {
            return new AdbProvisionResult(
                AdbStatusKind.DownloadFailed,
                null,
                "Platform-tools could not be installed: " + ex.Message);
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ReplaceDirectory(string staging, string target)
    {
        string old = target + ".old-" + Guid.NewGuid().ToString("N")[..8];

        if (Directory.Exists(target))
        {
            Directory.Move(target, old);
        }

        try
        {
            Directory.Move(staging, target);
            TryDeleteDirectory(old);
        }
        catch
        {
            if (!Directory.Exists(target) && Directory.Exists(old))
            {
                Directory.Move(old, target);
            }

            throw;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Temporary staging cleanup must not hide the install result.
        }
    }
}

/// <summary>HTTP download plus a zip extractor that only accepts platform-tools/ entries.</summary>
public sealed class HttpAdbPackageSource : IAdbPackageSource
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(2) };

    public async Task DownloadAsync(
        string url,
        string destination,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using FileStream stream = File.Create(destination);
        await response.Content.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public Task ExtractPlatformToolsAsync(
        string archivePath,
        string destination,
        CancellationToken cancellationToken = default)
        => Task.Run(() => Extract(archivePath, destination, cancellationToken), cancellationToken);

    private static void Extract(string archivePath, string destination, CancellationToken cancellationToken)
    {
        string root = Path.GetFullPath(destination);
        using ZipArchive archive = ZipFile.OpenRead(archivePath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = entry.FullName.Replace('\\', '/');

            if (!relative.StartsWith("platform-tools/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            relative = relative["platform-tools/".Length..].TrimStart('/');
            if (relative.Length == 0)
            {
                continue;
            }

            string target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The archive contains a path outside platform-tools.");
            }

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }
}
