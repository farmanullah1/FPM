namespace PCManager.Services.Infrastructure;

/// <summary>
/// Measures and empties well-known system cache folders.
///
/// Every file operation is individually guarded: a locked cache file skips, it never
/// aborts the whole cleanup. Reparse points and system files are excluded by
/// <see cref="SafeFileSystem"/>, so a junction cannot lead the walk outside the root.
/// </summary>
internal static class FolderCleaner
{
    internal static long Measure(params string[] roots)
    {
        long total = 0;

        foreach (string root in roots)
        {
            foreach (FileInfo file in SafeFileSystem.EnumerateFiles(root, recurse: true))
            {
                try { total += file.Length; } catch { /* vanished mid-scan */ }
            }
        }

        return total;
    }

    internal static (long Freed, int Deleted, int Skipped) DeleteContents(params string[] roots)
    {
        long freed = 0;
        int deleted = 0;
        int skipped = 0;

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (FileInfo file in SafeFileSystem.EnumerateFiles(root, recurse: true))
            {
                try
                {
                    long size = file.Length;

                    if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        file.Attributes = FileAttributes.Normal;
                    }

                    file.Delete();
                    freed += size;
                    deleted++;
                }
                catch (Exception)
                {
                    // Locked by a running process, or protected by ACLs (needs elevation).
                    skipped++;
                }
            }

            // Remove the now-empty sub-folders but keep the root the OS expects.
            try
            {
                foreach (string directory in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(directory).Any())
                        {
                            Directory.Delete(directory);
                        }
                    }
                    catch { /* in use */ }
                }
            }
            catch { /* root disappeared */ }
        }

        return (freed, deleted, skipped);
    }
}
