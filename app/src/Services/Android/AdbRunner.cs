using System.Diagnostics;
using System.Text;
using PCManager.Core.Android;

namespace PCManager.Services.Android;

public sealed record AdbProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false,
    bool Cancelled = false)
{
    public bool Success => !TimedOut && !Cancelled && ExitCode == 0;

    public string Combined => string.Join(
        Environment.NewLine,
        new[] { StandardOutput.Trim(), StandardError.Trim() }.Where(text => text.Length > 0));
}

public interface IAdbRunner
{
    Task<AdbProcessResult> RunAsync(
        string executable,
        AdbCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs only the typed A1 commands with ProcessStartInfo.ArgumentList. No shell, no free text,
/// no window, a bounded timeout, and cancellation kills the child process tree.
/// </summary>
public sealed class ProcessAdbRunner(TimeSpan? timeout = null) : IAdbRunner
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(20);

    public async Task<AdbProcessResult> RunAsync(
        string executable,
        AdbCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executable))
        {
            return new AdbProcessResult(-1, string.Empty, "adb.exe was not found.");
        }

        IReadOnlyList<string> arguments;

        try
        {
            arguments = AdbCommandLine.Build(command);
        }
        catch (ArgumentException ex)
        {
            return new AdbProcessResult(-1, string.Empty, ex.Message);
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows did not start adb.exe.");

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            });

            bool exited = process.WaitForExit((int)_timeout.TotalMilliseconds);

            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return new AdbProcessResult(-1, string.Empty, "adb timed out.", TimedOut: true);
            }

            string output = await stdout.ConfigureAwait(false);
            string error = await stderr.ConfigureAwait(false);

            return cancellationToken.IsCancellationRequested
                ? new AdbProcessResult(-1, output, error, Cancelled: true)
                : new AdbProcessResult(process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return new AdbProcessResult(-1, string.Empty, ex.Message);
        }
    }
}

public static class AdbFailures
{
    private static readonly string[] DisconnectMarkers =
    {
        "device offline",
        "no devices/emulators found",
        "device unauthorized",
        "device not found",
        "device still connecting",
        "failed to connect",
        "connection reset",
        "error: closed",
        "protocol fault",
        "no devices found",
    };

    public static bool IsDeviceDisconnect(AdbProcessResult result) =>
        result.Cancelled || (!result.Success && DisconnectMarkers.Any(marker =>
            result.Combined.Contains(marker, StringComparison.OrdinalIgnoreCase)));
}
