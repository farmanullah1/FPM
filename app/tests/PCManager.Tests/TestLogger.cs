using PCManager.Services.Abstractions;

namespace PCManager.Tests;

internal sealed class TestLogger : IAppLogger
{
    public string LogFilePath => "test.log";

    public List<string> Errors { get; } = new();

    public void Info(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
        => Errors.Add(message);
}
