namespace PCManager.Services.Abstractions;

/// <summary>
/// Isolated cleaner for Edge/Chrome/Brave disk caches and the WinINet cache folder.
/// Registry "Cache\Content" index values are left untouched: only the on-disk cache
/// files are removed, which is what the original app effectively does.
/// </summary>
public interface IBrowserCacheCleaner
{
    long GetReclaimableBytes();
    long Clean();
    IReadOnlyList<string> GetCacheRoots();
}
