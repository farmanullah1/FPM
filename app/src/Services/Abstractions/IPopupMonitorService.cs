using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Lists visible top-level windows owned by other apps and remembers which apps the user chose
/// to ignore. It never closes, hides or blocks a window; Windows offers no supported generic way
/// to do that, so the UI states the limit and links to Windows notification settings.
/// </summary>
public interface IPopupMonitorService
{
    Task<IReadOnlyList<PopupSourceInfo>> ScanAsync(CancellationToken cancellationToken = default);

    Task<bool> IsAllowedAsync(string processName);

    Task SetAllowedAsync(string processName, bool allowed);
}
