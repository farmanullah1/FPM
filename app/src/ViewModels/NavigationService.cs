namespace PCManager.ViewModels;

/// <summary>
/// Concrete navigation broker. It is registered as a singleton and the shell assigns
/// <see cref="Handler"/>; pages depend only on <see cref="INavigationService"/>, which
/// avoids a circular dependency between the shell and its pages.
/// </summary>
public sealed class NavigationService : INavigationService
{
    public Action<string>? Handler { get; set; }

    public void Navigate(string pageKey) => Handler?.Invoke(pageKey);
}
