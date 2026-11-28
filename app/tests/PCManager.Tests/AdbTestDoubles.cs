using System.IO.Compression;
using PCManager.Core.Android;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Android;

namespace PCManager.Tests;

internal sealed class FakeRunner(params AdbProcessResult[] results) : IAdbRunner
{
    private readonly Queue<AdbProcessResult> _results = new(results);

    public List<(string Executable, AdbCommand Command)> Calls { get; } = new();

    public IReadOnlyList<AdbCommandKind> Kinds => Calls.Select(call => call.Command.Kind).ToList();

    public Task<AdbProcessResult> RunAsync(
        string executable,
        AdbCommand command,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((executable, command));
        return Task.FromResult(_results.Count > 0
            ? _results.Dequeue()
            : new AdbProcessResult(0, string.Empty, string.Empty));
    }
}

internal sealed class FakeSignature : IAdbSignatureVerifier
{
    private readonly Dictionary<string, bool> _values;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public FakeSignature(bool result = true, bool? configured = null, bool? local = null)
    {
        _values = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (configured is not null) _values["configured"] = configured.Value;
        if (local is not null) _values["platform-tools"] = local.Value;
        _values["*"] = result;
    }

    public bool Called { get; private set; }

    public bool IsGoogleSigned(string executablePath)
    {
        Called = true;
        _seen.Add(executablePath);
        string name = Path.GetFileName(Path.GetDirectoryName(executablePath) ?? executablePath);
        return _values.TryGetValue(name, out bool value) ? value : _values["*"];
    }

    public bool Checked(string path) => _seen.Contains(path);
}

internal sealed class FakeProvisioner : IAdbProvisioner
{
    public bool Called { get; private set; }
    public bool? Consent { get; private set; }

    public Task<AdbProvisionResult> InstallAsync(bool consented, CancellationToken cancellationToken = default)
    {
        Called = true;
        Consent = consented;
        return Task.FromResult(new AdbProvisionResult(
            AdbStatusKind.Declined, null, "test"));
    }
}

internal sealed class FakePackageSource(byte[] archive) : IAdbPackageSource
{
    public int DownloadCount { get; private set; }
    public int ExtractCount { get; private set; }

    public Task DownloadAsync(string url, string destination, CancellationToken cancellationToken = default)
    {
        DownloadCount++;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllBytes(destination, archive);
        return Task.CompletedTask;
    }

    public Task ExtractPlatformToolsAsync(
        string archivePath,
        string destination,
        CancellationToken cancellationToken = default)
    {
        ExtractCount++;
        ZipFile.ExtractToDirectory(archivePath, destination);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSettings : ISettingsService
{
    public AppSettings Current { get; set; } = new();
    public bool IsFirstRun => false;
    public void Save() { }
}

internal sealed class FakeLogger : IAppLogger
{
    public string LogFilePath => "test";
    public void Info(string message) { }
    public void Error(string message, Exception? exception = null) { }
}

internal sealed class TempFiles : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "fpm-adb-test-" + Guid.NewGuid().ToString("N"));

    public TempFiles(bool createLocalAdb = true)
    {
        if (createLocalAdb)
        {
            CreateAdb("local");
        }
    }

    public string ToolsRoot => Path.Combine(_root, "tools");
    public string LocalPlatformTools => Path.Combine(ToolsRoot, "platform-tools");

    public string CreateAdb(string name)
    {
        string directory = name == "local" ? LocalPlatformTools : Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "adb.exe");
        File.WriteAllText(path, "fake");
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test cleanup is best effort.
        }
    }
}
