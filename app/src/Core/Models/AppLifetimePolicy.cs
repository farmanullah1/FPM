namespace PCManager.Core.Models;

/// <summary>What the tray Exit command should do (W3).</summary>
public enum ExitAction
{
    ExitAll,
    ExitKeepToolbar,
    Prompt,
}

/// <summary>The user's remembered answer to the exit question.</summary>
public enum ExitPreference
{
    Ask,
    ExitAll,
    ExitKeepToolbar,
}

/// <summary>What closing the main window should do (W3).</summary>
public enum CloseAction
{
    HideToTray,
    Exit,
}

/// <summary>
/// Lifetime rules, kept free of WPF so the tray and window behaviour can be tested headlessly.
/// </summary>
public static class AppLifetimePolicy
{
    /// <summary>
    /// Closing the main window keeps FPM alive only while the floating toolbar is enabled and the
    /// user has not turned "close to tray" off. Otherwise the process exits as usual.
    /// </summary>
    public static CloseAction DecideClose(bool closeToTray, bool toolbarEnabled, bool exiting) =>
        !exiting && closeToTray && toolbarEnabled ? CloseAction.HideToTray : CloseAction.Exit;

    /// <summary>
    /// Exit always offers the toolbar choice when the toolbar is running; with it off there is
    /// nothing to keep, so the question is pointless.
    /// </summary>
    public static ExitAction DecideExit(ExitPreference preference, bool toolbarEnabled, bool toolbarOnly)
    {
        if (!toolbarEnabled && !toolbarOnly)
        {
            return ExitAction.ExitAll;
        }

        return preference switch
        {
            ExitPreference.ExitAll => ExitAction.ExitAll,
            ExitPreference.ExitKeepToolbar => ExitAction.ExitKeepToolbar,
            _ => ExitAction.Prompt,
        };
    }
}
