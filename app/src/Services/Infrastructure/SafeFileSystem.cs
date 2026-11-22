namespace PCManager.Services.Infrastructure;

/// <summary>
/// File enumeration that never throws: inaccessible folders are skipped instead of
/// aborting a whole scan (System Volume Information, other users' profiles, locked
/// browser cache files, ...).
/// </summary>
internal static class SafeFileSystem
{
    internal static IEnumerable<FileInfo> EnumerateFiles(string root, bool recurse, int maxFiles = 250_000)
    {
        FileInfo[]? files = TryGetFiles(root, recurse);
        return files is null ? Array.Empty<FileInfo>() : files.Take(maxFiles);
    }

    internal static IEnumerable<DirectoryInfo> EnumerateDirectories(string root)
    {
        try
        {
            return new DirectoryInfo(root).GetDirectories("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            });
        }
        catch (Exception)
        {
            return Array.Empty<DirectoryInfo>();
        }
    }

    private static FileInfo[]? TryGetFiles(string root, bool recurse)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return null;
            }

            return new DirectoryInfo(root).GetFiles("*", new EnumerationOptions
            {
                RecurseSubdirectories = recurse,
                IgnoreInaccessible = true,
                // Reparse points would make the walk loop forever (junction cycles);
                // system files are never cleanup candidates.
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
                ReturnSpecialDirectories = false
            });
        }
        catch (Exception)
        {
            return null;
        }
    }
}
