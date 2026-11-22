using System.Diagnostics;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using PCManager.Core.Elevation;
using PCManager.Core.Formatting;

namespace PCManager.Helper;

/// <summary>
/// One-shot elevated worker. It accepts one typed request over a current-user-only pipe, performs
/// a fixed operation, answers, and exits. It never accepts a path, command line, executable or
/// service name from the client.
/// </summary>
internal static class Program
{
    private const int MaxRequestBytes = 8192;
    private const int ConnectTimeoutSeconds = 60;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] UpdateServices = { "wuauserv", "bits" };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            return RunAsync(args).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log($"FATAL {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        string pipeName = ResolvePipeName(args);

        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(ConnectTimeoutSeconds));

        try
        {
            await server.WaitForConnectionAsync(connectTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log("No client connected before the timeout.");
            return 0;
        }

        using var reader = new StreamReader(
            server,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        using var writer = new StreamWriter(
            server,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 4096,
            leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        HelperRequest? request = null;

        try
        {
            string? line = await ReadLimitedLineAsync(server, MaxRequestBytes, connectTimeout.Token)
                .ConfigureAwait(false);

            if (line is null)
            {
                Log("Request was empty or larger than the limit.");
                return 2;
            }

            request = JsonSerializer.Deserialize<HelperRequest>(line, JsonOptions);

            if (request is null)
            {
                Log("Request could not be parsed.");
                return 2;
            }

            HelperResponse response = await DispatchAsync(request).ConfigureAwait(false);
            await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            Log($"Request failed: {ex.GetType().Name}: {ex.Message}");

            if (request is not null)
            {
                try
                {
                    HelperResponse response = Failure(request, $"The helper failed: {ex.Message}");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions))
                        .ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    // The client is already gone; the log has the details.
                }
            }

            return 1;
        }
    }

    private static async Task<HelperResponse> DispatchAsync(HelperRequest request)
        => request.Command switch
        {
            HelperCommand.CleanRule => CleanRule(request),
            HelperCommand.CreateRestorePoint => await CreateRestorePointAsync(request).ConfigureAwait(false),
            _ => Failure(request, "Unsupported helper command.")
        };

    private static HelperResponse CleanRule(HelperRequest request)
    {
        if (!ElevatedCleanTargets.TryGet(request.RuleId, out ElevatedCleanTarget target))
        {
            return Failure(request, $"Unknown cleanup rule '{request.RuleId}'.");
        }

        if (target.Roots.All(root => !Directory.Exists(root)))
        {
            return Success(request, 0, 0, 0, "Nothing to remove; the folder does not exist.");
        }

        var stopped = new List<ServiceController>();

        try
        {
            if (target.StopUpdateServices)
            {
                foreach (string name in UpdateServices)
                {
                    TryStopService(name, stopped);
                }
            }

            long bytes = 0;
            int deleted = 0;
            int skipped = 0;
            DateTime cutoff = DateTime.UtcNow - target.MinimumAge;

            foreach (string root in target.Roots.Where(Directory.Exists))
            {
                CountAndDelete(root, cutoff, ref bytes, ref deleted, ref skipped);
                DeleteEmptySubfolders(root);
            }

            string message = deleted == 0
                ? skipped == 0
                    ? "Nothing matched the rule; the folder was already clean."
                    : $"Nothing was removed; {skipped:N0} file(s) were in use or protected."
                : $"{ByteFormatter.Format(bytes)} removed from {deleted:N0} file(s)" +
                  (skipped > 0 ? $"; {skipped:N0} skipped." : ".");

            Log($"CleanRule {target.RuleId}: {deleted} deleted, {skipped} skipped, {bytes} bytes.");

            return Success(request, bytes, deleted, skipped, message);
        }
        catch (Exception ex)
        {
            Log($"CleanRule {target.RuleId} failed: {ex}");
            return Failure(request, $"The cleanup failed: {ex.Message}");
        }
        finally
        {
            foreach (ServiceController service in stopped)
            {
                try
                {
                    service.Start();
                }
                catch (Exception ex)
                {
                    Log($"Could not restart {service.ServiceName}: {ex.Message}");
                }
                finally
                {
                    service.Dispose();
                }
            }
        }
    }

    private static void CountAndDelete(
        string root,
        DateTime cutoff,
        ref long bytes,
        ref int deleted,
        ref int skipped)
    {
        string rootPrefix = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            // Reparse points are neither followed nor deleted.
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (string path in Directory.EnumerateFiles(root, "*", options))
        {
            try
            {
                string fullPath = Path.GetFullPath(path);

                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var file = new FileInfo(fullPath);

                if (file.LastWriteTimeUtc > cutoff)
                {
                    continue;
                }

                if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    skipped++;
                    continue;
                }

                long length = file.Length;

                if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    file.Attributes = FileAttributes.Normal;
                }

                file.Delete();
                bytes += length;
                deleted++;
            }
            catch (UnauthorizedAccessException)
            {
                skipped++;
            }
            catch (IOException)
            {
                skipped++;
            }
            catch (Exception ex)
            {
                skipped++;
                Log($"Delete failed for {path}: {ex.Message}");
            }
        }
    }

    private static void DeleteEmptySubfolders(string root)
    {
        try
        {
            var directories = new DirectoryInfo(root)
                .EnumerateDirectories("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                })
                .OrderByDescending(directory => directory.FullName.Length)
                .ToList();

            foreach (DirectoryInfo directory in directories)
            {
                try
                {
                    if (!directory.EnumerateFileSystemInfos().Any())
                    {
                        directory.Delete();
                    }
                }
                catch
                {
                    // Still in use; it stays for the next run.
                }
            }
        }
        catch
        {
            // Best-effort; the root is never removed.
        }
    }

    private static async Task<HelperResponse> CreateRestorePointAsync(HelperRequest request)
    {
        try
        {
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            // Fixed command; no user input is interpolated.
            startInfo.ArgumentList.Add(
                "Checkpoint-Computer -RestorePointType 'MODIFY_SETTINGS' -Description 'Farmanullah PC Manager cleanup'");

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return Failure(request, "Windows did not start the restore-point command.");
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            string stdout = await process.StandardOutput.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
            string stderr = await process.StandardError.ReadToEndAsync(timeout.Token).ConfigureAwait(false);

            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                Log("Restore point created.");
                return Success(request, 0, 0, 0, "A System Restore point was created.");
            }

            string detail = FirstLine(stderr) ?? FirstLine(stdout) ?? $"exit code {process.ExitCode}";
            Log($"Restore point failed: {detail}");
            return Failure(request, $"System Restore did not create a point: {detail}");
        }
        catch (OperationCanceledException)
        {
            return Failure(request, "System Restore did not finish within 3 minutes.");
        }
        catch (Exception ex)
        {
            Log($"Restore point failed: {ex}");
            return Failure(request, $"System Restore failed: {ex.Message}");
        }
    }

    private static void TryStopService(string name, List<ServiceController> stopped)
    {
        ServiceController? service = null;

        try
        {
            service = new ServiceController(name);

            if (service.Status != ServiceControllerStatus.Running)
            {
                service.Dispose();
                return;
            }

            service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
            stopped.Add(service);
            Log($"Stopped service {name}.");
        }
        catch (Exception ex)
        {
            service?.Dispose();
            Log($"Could not stop {name}: {ex.Message}");
        }
    }

    private static async Task<string?> ReadLimitedLineAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[maxBytes + 1];
        int total = 0;

        while (total < maxBytes)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, 1), cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            if (buffer[total] == (byte)'\n')
            {
                break;
            }

            total++;
        }

        // A full buffer means the client sent more than one request line.

        if (total >= maxBytes || (total > 0 && buffer[total - 1] != (byte)'\n' && total == maxBytes))
        {
            return null;
        }

        string line = Encoding.UTF8.GetString(buffer, 0, total).TrimEnd('\r');
        return string.IsNullOrWhiteSpace(line) ? null : line;
    }

    private static string ResolvePipeName(string[] args)
    {
        const string prefix = "FarmanullahPCManager.Helper.";

        if (args.Length > 0 &&
            args[0].StartsWith(prefix, StringComparison.Ordinal) &&
            args[0].IndexOfAny(new[] { '\\', '/', ':', '\0' }) < 0)
        {
            return args[0];
        }

        // Fallback for a manual launch; the normal client always supplies the name.
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return prefix + (identity.User?.Value ?? Environment.UserName);
        }
        catch
        {
            return prefix + Environment.UserName;
        }
    }

    private static HelperResponse Success(
        HelperRequest request,
        long bytes,
        int deleted,
        int skipped,
        string message)
        => new()
        {
            Success = true,
            BytesFreed = bytes,
            FilesDeleted = deleted,
            FilesSkipped = skipped,
            Message = message,
            RequestId = request.RequestId
        };

    private static HelperResponse Failure(HelperRequest request, string message)
        => new()
        {
            Success = false,
            Message = message,
            RequestId = request.RequestId
        };

    private static string? FirstLine(string? text)
    {
        string? line = text?
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(candidate => candidate.Length > 0);

        if (line is null)
        {
            return null;
        }

        return line.Length > 300 ? line[..300] : line;
    }

    private static void Log(string message)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FarmanullahPCManager",
                "logs");

            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, $"helper-{DateTime.Now:yyyyMMdd}.log"),
                $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // The helper must not fail because its own log is unavailable.
        }
    }
}
