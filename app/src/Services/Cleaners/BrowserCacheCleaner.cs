using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Cleaners;

/// <summary>
/// Isolated browser/system cache cleaner. Removes cache files only; cookies, history
/// and logins are stored elsewhere and stay intact.
/// </summary>
public sealed class BrowserCacheCleaner : IBrowserCacheCleaner
{
    public long GetReclaimableBytes() => Collect(delete: false);

    public long Clean() => Collect(delete: true);

    public IReadOnlyList<string> GetCacheRoots() => Roots().Where(Directory.Exists).ToList();

    private static long Collect(bool delete)
    {
        long total = 0;

        foreach (string root in Roots())
        {
            foreach (FileInfo file in SafeFileSystem.EnumerateFiles(root, recurse: true, maxFiles: 120_000))
            {
                try
                {
                    if (!delete)
                    {
                        total += file.Length;
                        continue;
                    }

                    if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        file.Attributes = FileAttributes.Normal;
                    }

                    // Capture the size first; after Delete() FileInfo throws, which used to
                    // discard the bytes actually released.
                    long size = file.Length;
                    file.Delete();
                    total += size;
                }
                catch (IOException) { /* cache file locked by the browser */ }
                catch (UnauthorizedAccessException) { }
                catch (Exception) { }
            }
        }

        return total;
    }

    /// <summary>
    /// Chromium-based browsers keep their HTTP cache under
    /// %LOCALAPPDATA%&lt;Vendor&gt;\&lt;Product&gt;\User Data\&lt;Profile&gt;\Cache.
    /// </summary>
    private static IEnumerable<string> Roots()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache");
        yield return Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Code Cache");
        yield return Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache");
        yield return Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Code Cache");
        yield return Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache");
        yield return Path.Combine(local, "Microsoft", "Windows", "INetCache");
        yield return Path.Combine(local, "Microsoft", "Windows", "WebCache");
    }
}
