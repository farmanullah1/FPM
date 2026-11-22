namespace PCManager.Services.Abstractions;

/// <summary>
/// Minimal file logger. Everything goes to
/// %LOCALAPPDATA%\FarmanullahPCManager\logs — never to a Microsoft folder.
/// </summary>
public interface IAppLogger
{
    string LogFilePath { get; }
    void Info(string message);
    void Error(string message, Exception? exception = null);
}
