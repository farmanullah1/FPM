using PCManager.Core.Models;
using Xunit;

namespace PCManager.Tests;

public sealed class AppLifetimePolicyTests
{
    [Fact]
    public void ClosingHidesToTrayWhileTheToolbarRuns()
    {
        Assert.Equal(CloseAction.HideToTray, AppLifetimePolicy.DecideClose(true, true, false));
    }

    [Fact]
    public void ClosingExitsWhenTheToolbarIsOff()
    {
        Assert.Equal(CloseAction.Exit, AppLifetimePolicy.DecideClose(true, false, false));
    }

    [Fact]
    public void ClosingExitsWhenCloseToTrayIsOff()
    {
        Assert.Equal(CloseAction.Exit, AppLifetimePolicy.DecideClose(false, true, false));
    }

    [Fact]
    public void ClosingExitsWhenTheAppIsAlreadyExiting()
    {
        Assert.Equal(CloseAction.Exit, AppLifetimePolicy.DecideClose(true, true, true));
    }

    [Fact]
    public void ExitSkipsTheQuestionWhenTheToolbarIsNotRunning()
    {
        Assert.Equal(ExitAction.ExitAll, AppLifetimePolicy.DecideExit(ExitPreference.Ask, false, false));
    }

    [Fact]
    public void ExitPromptsByDefaultWhenThereIsAToolbarToKeep()
    {
        Assert.Equal(ExitAction.Prompt, AppLifetimePolicy.DecideExit(ExitPreference.Ask, true, false));
    }

    [Fact]
    public void RememberedChoicesSkipTheQuestion()
    {
        Assert.Equal(
            ExitAction.ExitAll,
            AppLifetimePolicy.DecideExit(ExitPreference.ExitAll, true, false));
        Assert.Equal(
            ExitAction.ExitKeepToolbar,
            AppLifetimePolicy.DecideExit(ExitPreference.ExitKeepToolbar, true, false));
    }

    [Fact]
    public void ToolbarOnlyAlwaysOffersTheChoice()
    {
        Assert.Equal(ExitAction.Prompt, AppLifetimePolicy.DecideExit(ExitPreference.Ask, false, true));
    }
}
