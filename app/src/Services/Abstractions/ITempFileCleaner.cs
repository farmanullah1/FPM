namespace PCManager.Services.Abstractions;

/// <summary>
/// Isolated cleaner: temporary files older than 48 hours in the user and Windows
/// temp folders. Used by both the Health Check module and PC Boost.
/// </summary>
public interface ITempFileCleaner
{
    /// <summary>Bytes that <see cref="Clean"/> could release right now.</summary>
    long GetReclaimableBytes();

    /// <summary>Deletes the files and returns the bytes actually freed.</summary>
    long Clean();
}
