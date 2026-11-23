namespace PCManager.Core.Models;

/// <summary>
/// Measured network throughput snapshot across active network interfaces.
/// </summary>
public sealed record NetworkThroughputSnapshot(
    long BytesReceivedPerSecond,
    long BytesSentPerSecond,
    string FormattedDownloadSpeed,
    string FormattedUploadSpeed,
    string ActiveAdapterName,
    DateTime TimestampUtc)
{
    public static NetworkThroughputSnapshot Empty => new(
        0, 0, "0 KB/s", "0 KB/s", "None", DateTime.UtcNow);
}
