using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Cleaners;

/// <summary>
/// Isolated temporary-file cleaner.
///
/// Rules (matching the original app): only files whose last-write time is older than
/// 48 hours are considered, so programs that are actively using a temp file do not get
/// their working data deleted. Files currently open by another process throw
/// IOException on delete and are simply skipped.
/// </summary>
public sealed class TempFileCleaner : ITempFileCleaner
{
    internal static readonly TimeSpan MinimumAge = TimeSpan.FromHours(48);

    public long GetReclaimableBytes() => Collect(delete: false);

    public long Clean() => Collect(delete: true);

    private static long Collect(bool delete)
    {
        long total = 0;
        DateTime cutoff = DateTime.Now - MinimumAge;

        foreach (string root in Roots())
        {
            foreach (FileInfo file in SafeFileSystem.EnumerateFiles(root, recurse: true))
            {
                try
                {
                    if (file.Length == 0 || file.LastWriteTime > cutoff)
                    {
                        continue;
                    }

                    if (!delete)
                    {
                        total += file.Length;
                        continue;
                    }

                    // Read-only files cannot be deleted; clearing the attribute first is
                    // what Storage Sense and the original cleaner do too.
                    if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        file.Attributes = FileAttributes.Normal;
                    }

                    // Read the size before deleting: once the file is gone FileInfo can no
                    // longer report it, and the resulting exception used to swallow the
                    // freed bytes so every successful clean reported 0.
                    long size = file.Length;
                    file.Delete();
                    total += size;
                }
                catch (IOException) { /* file in use - skip */ }
                catch (UnauthorizedAccessException) { /* protected folder - skip */ }
                catch (Exception) { /* never let one file abort the clean */ }
            }
        }

        return total;
    }

    private static IEnumerable<string> Roots()
    {
        // Per-user temp (%LOCALAPPDATA%\Temp, what Path.GetTempPath returns).
        yield return Path.GetTempPath();

        // Machine-wide temp (%SystemRoot%\Temp). Deletion usually needs elevation;
        // the enumerator silently skips what it cannot open.
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

        // Legacy per-user temp location used by some installers.
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localAppData, "Temp");
    }
}
