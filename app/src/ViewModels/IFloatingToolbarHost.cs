namespace PCManager.ViewModels;

/// <summary>
/// Shows or hides the floating toolbar pill. Implemented by the WPF layer because it owns
/// windows; the view models only see this interface.
/// </summary>
public interface IFloatingToolbarHost
{
    bool IsVisible { get; }

    void Show();

    void Hide();
}
