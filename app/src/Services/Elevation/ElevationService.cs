using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using PCManager.Core.Elevation;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Elevation;

/// <summary>
/// Starts the packaged requireAdministrator helper only when a command needs it, then sends one
/// fixed request over a current-user-only pipe. The helper is never left running after a reply.
/// </summary>
public sealed class ElevationService : IElevationService
{
    private const int ConnectTimeoutSeconds = 45;
    private const int ResponseTimeoutSeconds = 180;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppLogger _logger;
    private readonly string _helperPath;
    private readonly string _pipePrefix;

    public ElevationService(IAppLogger logger, string? helperPath = null)
    {
        _logger = logger;
        _helperPath = helperPath ??
                      (File.Exists(Path.Combine(AppContext.BaseDirectory, "FPM.Helper.exe"))
                          ? Path.Combine(AppContext.BaseDirectory, "FPM.Helper.exe")
                          : Path.Combine(AppContext.BaseDirectory, "FarmanullahPCManager.Helper.exe"));
        _pipePrefix = BuildPipePrefix();
    }

    public bool IsHelperAvailable => File.Exists(_helperPath);

    public async Task<ElevationOutcome> RunAsync(
        HelperCommand command,
        string? ruleId = null,
        IReadOnlyList<string>? domains = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsHelperAvailable)
        {
            return ElevationOutcome.Failed(
                "The elevated helper is missing from the application folder. Rebuild or reinstall the app.");
        }

        // A fresh random pipe name per launch prevents another same-user process from
        // squatting on a predictable name and answering for the elevated helper.
        string pipeName = $"{_pipePrefix}.{Guid.NewGuid():N}";

        var request = new HelperRequest
        {
            Command = command,
            RuleId = ruleId ?? string.Empty,
            RequestId = Guid.NewGuid().ToString("N"),
            Domains = domains?.ToList() ?? new List<string>()
        };

        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _helperPath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };

            // The pipe name is derived from the signed-in user's SID, never from user input.
            // Shell-execute (runas) needs Arguments; the value is validated again by the helper.
            startInfo.Arguments = $"\"{pipeName}\"";
            process = Process.Start(startInfo);

            if (process is null)
            {
                return ElevationOutcome.Failed("Windows did not start the elevated helper.");
            }

            using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(ConnectTimeoutSeconds));

            try
            {
                await client.ConnectAsync(connectTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ElevationOutcome.Failed(
                    $"The elevated helper did not connect within {ConnectTimeoutSeconds} seconds.");
            }

            using var writer = new StreamWriter(
                client,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096,
                leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n"
            };

            using var reader = new StreamReader(
                client,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions))
                .ConfigureAwait(false);

            using var responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            responseTimeout.CancelAfter(TimeSpan.FromSeconds(ResponseTimeoutSeconds));

            string? line = await reader.ReadLineAsync(responseTimeout.Token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(line))
            {
                return ElevationOutcome.Failed("The elevated helper closed the connection without a reply.");
            }

            HelperResponse? response = JsonSerializer.Deserialize<HelperResponse>(line, JsonOptions);

            if (response is null || !string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
            {
                return ElevationOutcome.Failed("The elevated helper returned an unexpected reply.");
            }

            _logger.Info(
                $"Elevation {command}{(string.IsNullOrWhiteSpace(ruleId) ? string.Empty : $" ({ruleId})")}: " +
                $"success={response.Success}, bytes={response.BytesFreed}, files={response.FilesDeleted}, " +
                $"skipped={response.FilesSkipped}.");

            return new ElevationOutcome(
                response.Success,
                response.Cancelled,
                response.BytesFreed,
                response.FilesDeleted,
                response.FilesSkipped,
                response.Message);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return ElevationOutcome.Declined();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ElevationOutcome.Failed(
                $"The elevated helper did not answer within {ResponseTimeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            _logger.Error("The elevated helper call failed.", ex);
            return ElevationOutcome.Failed($"The elevated helper call failed: {ex.Message}");
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    if (!process.WaitForExit(3000))
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // The helper may already have exited; nothing else to clean up.
                }

                process.Dispose();
            }
        }
    }

    private static string BuildPipePrefix()
    {
        string sid;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            sid = identity.User?.Value ?? Environment.UserName;
        }
        catch
        {
            sid = Environment.UserName;
        }

        return $"FPM.Helper.{sid}";
    }
}

/// <summary>Uses the helper's fixed, non-parameterised Checkpoint-Computer command.</summary>
public sealed class RestorePointService : IRestorePointService
{
    private readonly IElevationService _elevation;

    public RestorePointService(IElevationService elevation) => _elevation = elevation;

    public Task<ElevationOutcome> CreateAsync(CancellationToken cancellationToken = default)
        => _elevation.RunAsync(HelperCommand.CreateRestorePoint, cancellationToken: cancellationToken);
}
