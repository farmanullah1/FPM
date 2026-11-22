using Microsoft.Extensions.DependencyInjection;
using PCManager.ViewModels;
using PCManager.Views;

namespace PCManager.Services;

/// <summary>
/// Owns the singleton floating-toolbar window. The window is created lazily on first use
/// and hidden (not destroyed) afterwards so reopening it is instant.
/// </summary>
public sealed class FloatingToolbarHost : IFloatingToolbarHost
{
    private readonly IServiceProvider _services;
    private FloatingToolbarWindow? _window;

    public FloatingToolbarHost(IServiceProvider services) => _services = services;

    public bool IsVisible => _window?.IsVisible == true;

    public void Show()
    {
        if (_window is null)
        {
            _window = _services.GetRequiredService<FloatingToolbarWindow>();
            _window.Closed += (_, _) => _window = null;
        }

        _window.Show();
        _window.Activate();
    }

    public void Hide()
    {
        if (_window is { IsVisible: true })
        {
            _window.Hide();
        }
    }
}
