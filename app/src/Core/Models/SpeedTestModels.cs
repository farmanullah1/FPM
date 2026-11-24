namespace PCManager.Core.Models;

/// <summary>
/// Result of a user-started download speed test. The endpoint is always shown with the number
/// because throughput depends on the server, route and time of day.
/// </summary>
public sealed record SpeedTestResult(
    bool Success,
    double MegabitsPerSecond,
    long BytesDownloaded,
    double Seconds,
    string Endpoint,
    string Message)
{
    public static SpeedTestResult Failed(string message, string endpoint = "")
        => new(false, 0, 0, 0, endpoint, message);
}
