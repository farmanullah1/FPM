using System.Text;
using PCManager.Core.Android;
using PCManager.Services.Android;
using Xunit;

namespace PCManager.Tests;

public sealed class UadListProviderTests
{
    private const string CacheJson = """
        [
          {"id":"com.example.one","list":"Oem","description":"First","removal":"Recommended"},
          {"id":"com.example.two","list":"Google","description":"Second","removal":"Advanced"}
        ]
        """;

    private const string RemoteJson = """
        [
          {"id":"com.example.one","list":"Oem","description":"First","removal":"Recommended"},
          {"id":"com.example.two","list":"Google","description":"Second changed","removal":"Advanced"},
          {"id":"com.example.three","list":"Misc","description":"Third","removal":"Expert"}
        ]
        """;

    [Fact]
    public async Task Embedded_is_the_fallback_and_cache_is_preferred_when_valid()
    {
        using var directory = new UadTestDirectory();
        var source = new FakeUadSource(Encoding.UTF8.GetBytes(RemoteJson));
        var provider = new UadListProvider(source, new FakeLogger(), directory.Path);

        UadListCatalog embedded = await provider.LoadAsync();
        Assert.Equal(UadListOrigin.Embedded, embedded.Origin);
        Assert.Equal(2394, embedded.Packages.Count);
        Assert.Equal(0, source.Calls);

        directory.WriteCache(CacheJson);
        var cachedProvider = new UadListProvider(source, new FakeLogger(), directory.Path);
        UadListCatalog cached = await cachedProvider.LoadAsync();

        Assert.Equal(UadListOrigin.Cached, cached.Origin);
        Assert.Equal(2, cached.Packages.Count);
    }

    [Fact]
    public async Task Invalid_cache_falls_back_to_embedded_with_a_warning()
    {
        using var directory = new UadTestDirectory();
        directory.WriteCache("{not valid");
        var provider = new UadListProvider(new FakeUadSource(Array.Empty<byte>()), new FakeLogger(), directory.Path);

        UadListCatalog catalog = await provider.LoadAsync();

        Assert.Equal(UadListOrigin.Embedded, catalog.Origin);
        Assert.Equal(2394, catalog.Packages.Count);
        Assert.NotNull(provider.LoadWarning);
        Assert.Contains("embedded", provider.LoadWarning);
    }

    [Fact]
    public async Task Update_requires_an_explicit_user_action()
    {
        using var directory = new UadTestDirectory();
        directory.WriteCache(CacheJson);
        var source = new FakeUadSource(Encoding.UTF8.GetBytes(RemoteJson));
        var provider = new UadListProvider(source, new FakeLogger(), directory.Path);
        await provider.LoadAsync();

        UadListUpdateResult result = await provider.UpdateAsync(userInitiated: false);

        Assert.Equal(UadListUpdateStatus.Unavailable, result.Status);
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Invalid_upstream_is_rejected_without_changing_current_or_cache()
    {
        using var directory = new UadTestDirectory();
        directory.WriteCache(CacheJson);
        var source = new FakeUadSource(Encoding.UTF8.GetBytes("[{\"id\":\"bad\"}]"));
        var provider = new UadListProvider(source, new FakeLogger(), directory.Path);
        await provider.LoadAsync();

        UadListUpdateResult result = await provider.UpdateAsync(userInitiated: true);

        Assert.Equal(UadListUpdateStatus.Invalid, result.Status);
        Assert.Equal(2, provider.Current!.Packages.Count);
        Assert.Equal(CacheJson, directory.ReadCache().TrimEnd());
        Assert.DoesNotContain("bad", directory.ReadCache());
    }

    [Fact]
    public async Task Identical_upstream_is_already_current_and_is_not_rewritten()
    {
        using var directory = new UadTestDirectory();
        directory.WriteCache(CacheJson);
        var source = new FakeUadSource(Encoding.UTF8.GetBytes(CacheJson));
        var provider = new UadListProvider(source, new FakeLogger(), directory.Path);
        await provider.LoadAsync();

        UadListUpdateResult result = await provider.UpdateAsync(userInitiated: true);

        Assert.Equal(UadListUpdateStatus.AlreadyCurrent, result.Status);
        Assert.Equal(1, source.Calls);
        Assert.NotNull(result.Diff);
        Assert.False(result.Diff!.HasChanges);
    }

    [Fact]
    public async Task Updated_list_reports_diff_writes_cache_and_sha_sidecar()
    {
        using var directory = new UadTestDirectory();
        directory.WriteCache(CacheJson);
        var source = new FakeUadSource(Encoding.UTF8.GetBytes(RemoteJson));
        var provider = new UadListProvider(source, new FakeLogger(), directory.Path);
        await provider.LoadAsync();

        UadListUpdateResult result = await provider.UpdateAsync(userInitiated: true);

        Assert.Equal(UadListUpdateStatus.Updated, result.Status);
        Assert.Equal(3, provider.Current!.Packages.Count);
        Assert.Equal(1, result.Diff!.Added);
        Assert.Equal(0, result.Diff.Removed);
        Assert.Equal(1, result.Diff.Changed);
        Assert.Equal(provider.Current.Sha256, result.Sha256);
        Assert.Equal(RemoteJson, directory.ReadCache().TrimEnd());
        Assert.True(File.Exists(directory.Path + "/uad_lists.json.sha256"));
        Assert.Equal(provider.Current.Sha256 + Environment.NewLine, directory.ReadHash());

        var reloaded = new UadListProvider(source, new FakeLogger(), directory.Path);
        UadListCatalog cached = await reloaded.LoadAsync();
        Assert.Equal(UadListOrigin.Cached, cached.Origin);
        Assert.Equal(3, cached.Packages.Count);
    }

    [Fact]
    public async Task Network_failure_keeps_the_previous_catalog()
    {
        using var directory = new UadTestDirectory();
        directory.WriteCache(CacheJson);
        var source = new FakeUadSource(new HttpRequestException("offline"));
        var provider = new UadListProvider(source, new FakeLogger(), directory.Path);
        await provider.LoadAsync();

        UadListUpdateResult result = await provider.UpdateAsync(userInitiated: true);

        Assert.Equal(UadListUpdateStatus.Failed, result.Status);
        Assert.Equal(2, provider.Current!.Packages.Count);
        Assert.Contains("offline", result.Message);
    }

    private sealed class FakeUadSource : IUadListSource
    {
        private readonly byte[]? _bytes;
        private readonly Exception? _error;

        public FakeUadSource(byte[] bytes) => _bytes = bytes;

        public FakeUadSource(Exception error) => _error = error;

        public int Calls { get; private set; }

        public Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default)
        {
            Calls++;
            return _error is not null
                ? Task.FromException<byte[]>(_error)
                : Task.FromResult(_bytes!);
        }
    }

    private sealed class UadTestDirectory : IDisposable
    {
        public UadTestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "fpm-uad-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void WriteCache(string json) =>
            File.WriteAllText(System.IO.Path.Combine(Path, "uad_lists.json"), json);

        public string ReadCache() =>
            File.ReadAllText(System.IO.Path.Combine(Path, "uad_lists.json"));

        public string ReadHash() =>
            File.ReadAllText(System.IO.Path.Combine(Path, "uad_lists.json.sha256"));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test cleanup is best effort.
            }
        }
    }
}
