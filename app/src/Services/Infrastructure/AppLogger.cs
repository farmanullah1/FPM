using PCManager.Core;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Infrastructure;

/// <summary>
/// Appends timestamped lines to %LOCALAPPDATA%\FarmanullahPCManager\logs\app-yyyyMMdd.log.
/// Writes are serialised and capped at 2 MB per day so a chatty error loop cannot fill the
/// disk. Logging failures are swallowed: diagnostics must never break a cleanup.
/// </summary>
public sealed class AppLogger : IAppLogger
{
    private const long MaxBytes = 2 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly string _path;

    public AppLogger()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.LogFolder);
        }
        catch
        {
            // A locked-down profile still gets a working app.
        }

        _path = Path.Combine(AppInfo.LogFolder, $"app-{DateTime.Now:yyyyMMdd}.log");
    }

    public string LogFilePath => _path;

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (_gate)
            {
                var file = new FileInfo(_path);
                if (file.Exists && file.Length > MaxBytes)
                {
                    return;
                }

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";

                if (exception is not null)
                {
                    line += Environment.NewLine + exception;
                }

                File.AppendAllText(_path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Never let logging surface to the user.
        }
    }
}
