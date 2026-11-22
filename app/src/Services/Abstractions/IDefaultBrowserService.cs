using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>
/// Reads the default browser. Windows 10/11 deliberately blocks programmatic
/// changes, so the only supported "change" is opening the Default apps settings page.
/// </summary>
public interface IDefaultBrowserService
{
    DefaultBrowserSetting GetDefaultBrowser();

    /// <summary>Opens <c>ms-settings:defaultapps</c>.</summary>
    OperationResult OpenDefaultAppsSettings();
}
