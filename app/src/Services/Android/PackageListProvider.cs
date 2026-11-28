using System.Net.Http.Headers;
using System.Text;
using PCManager.Core;
using PCManager.Core.Android;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Android;

public interface IUadListSource
{
    Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches the upstream list only after an explicit user command. The response is bounded so a
/// malformed or hostile endpoint cannot exhaust memory; the provider parses it before writing.
/// </summary>
public sealed class HttpUadListSource : IUadListSource
{
    public const string UpstreamUrl =
        "https://raw.githubusercontent.com/0x192/universal-android-debloater/main/resources/assets/uad_lists.json";

    public const int MaxDownloadBytes = 4_000_000;

    private static readonly HttpClient Client = CreateClient();

    public async Task<byte[]> DownloadAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > MaxDownloadBytes)
        {
            throw new InvalidDataException("The UAD list response is too large.");
        }

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var memory = new MemoryStream();
        byte[] buffer = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (memory.Length + read > MaxDownloadBytes)
            {
                throw new InvalidDataException("The UAD list response is too large.");
            }

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FPM", "1.0"));
        return client;
    }
}

public interface IPackageListProvider
{
    UadListCatalog? Current { get; }
    string? LoadWarning { get; }
    string CacheFilePath { get; }

    Task<UadListCatalog> LoadAsync(CancellationToken cancellationToken = default);

    Task<UadListUpdateResult> UpdateAsync(
        bool userInitiated,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads the embedded list as the guaranteed fallback, prefers a valid cached update, and writes
/// a new cache + SHA-256 sidecar only after the remote array passes the same schema validator.
/// No request is made unless the caller passes the user's explicit update action.
/// </summary>
public sealed class UadListProvider : IPackageListProvider
{
    private readonly IUadListSource _source;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _cacheDirectory;
    private UadListCatalog? _current;

    public UadListProvider(
        IUadListSource source,
        IAppLogger logger,
        string? cacheDirectory = null)
    {
        _source = source;
        _logger = logger;
        _cacheDirectory = cacheDirectory ??
            Path.Combine(AppInfo.DataFolder, "android", "lists");
    }

    public UadListCatalog? Current => _current;
    public string? LoadWarning { get; private set; }
    public string CacheFilePath => Path.Combine(_cacheDirectory, "uad_lists.json");

    public async Task<UadListCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_current is not null)
            {
                return _current;
            }

            if (File.Exists(CacheFilePath))
            {
                try
                {
                    string cachedJson = await File
                        .ReadAllTextAsync(CacheFilePath, Encoding.UTF8, cancellationToken)
                        .ConfigureAwait(false);

                    if (UadListParser.TryParse(
                            cachedJson,
                            UadListOrigin.Cached,
                            out UadListCatalog? cached,
                            out var cachedErrors))
                    {
                        _current = cached!;
                        LoadWarning = null;
                        _logger.Info($"UAD list loaded from cache ({_current.Packages.Count} packages).");
                        return _current;
                    }

                    LoadWarning = "Cached UAD list was invalid; the embedded list is in use: " +
                                  string.Join("; ", cachedErrors);
                    _logger.Error(LoadWarning);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LoadWarning = "Cached UAD list could not be read; the embedded list is in use.";
                    _logger.Error(LoadWarning, ex);
                }
            }

            _current = UadListCatalogLoader.LoadEmbedded();
            _logger.Info($"UAD embedded list loaded ({_current.Packages.Count} packages).");
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UadListUpdateResult> UpdateAsync(
        bool userInitiated,
        CancellationToken cancellationToken = default)
    {
        if (!userInitiated)
        {
            return new UadListUpdateResult(
                UadListUpdateStatus.Unavailable,
                "The UAD list update requires an explicit user action.",
                _current,
                null,
                _current?.Sha256);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            UadListCatalog current = _current ?? UadListCatalogLoader.LoadEmbedded();
            _current = current;

            byte[] bytes;

            try
            {
                bytes = await _source
                    .DownloadAsync(HttpUadListSource.UpstreamUrl, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Result(UadListUpdateStatus.Failed, "The UAD list update was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.Error("UAD list update failed.", ex);
                return Result(UadListUpdateStatus.Failed, "The UAD list update failed: " + ex.Message);
            }

            string json = Encoding.UTF8.GetString(bytes);

            if (!UadListParser.TryParse(json, UadListOrigin.Remote, out UadListCatalog? remote, out var errors))
            {
                return Result(
                    UadListUpdateStatus.Invalid,
                    "The upstream UAD list did not match the schema: " + string.Join("; ", errors));
            }

            UadListCatalog next = remote!;

            if (next.Sha256.Equals(current.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return new UadListUpdateResult(
                    UadListUpdateStatus.AlreadyCurrent,
                    "The embedded list already matches the upstream SHA-256.",
                    current,
                    UadListDiff.Between(current, next),
                    current.Sha256);
            }

            UadListDiff diff = UadListDiff.Between(current, next);

            try
            {
                await WriteCacheAsync(json, next.Sha256, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Error("UAD list cache write failed.", ex);
                return Result(UadListUpdateStatus.Failed, "The UAD list could not be cached: " + ex.Message);
            }

            _current = next;
            LoadWarning = null;
            _logger.Info(
                $"UAD list updated ({diff.Added} added, {diff.Removed} removed, {diff.Changed} changed).");

            return new UadListUpdateResult(
                UadListUpdateStatus.Updated,
                "The UAD list was updated from the upstream source.",
                next,
                diff,
                next.Sha256);
        }
        finally
        {
            _gate.Release();
        }
    }

    private UadListUpdateResult Result(UadListUpdateStatus status, string message) =>
        new(status, message, _current, null, _current?.Sha256);

    private async Task WriteCacheAsync(
        string json,
        string sha256,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_cacheDirectory);
        string temporary = CacheFilePath + ".tmp";
        await File.WriteAllTextAsync(temporary, json, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            File.Move(temporary, CacheFilePath, overwrite: true);
            await File.WriteAllTextAsync(
                    CacheFilePath + ".sha256",
                    sha256 + Environment.NewLine,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
