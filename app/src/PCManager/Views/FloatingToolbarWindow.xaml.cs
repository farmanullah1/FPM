using System.Windows;
using System.Windows.Input;
using PCManager.Services;
using PCManager.Services.Abstractions;
using PCManager.ViewModels;

namespace PCManager.Views;

/// <summary>
/// Small always-on-top pill. Closing it hides the window (the host recreates nothing), and
/// the position is kept and persisted to settings.
/// </summary>
public partial class FloatingToolbarWindow : Window
{
    private readonly ISettingsService _settings;

    public FloatingToolbarWindow(FloatingToolbarViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();

        _settings = settings;
        DataContext = viewModel;
        AppIdentity.ApplyWindowIcon(this);

        Rect work = SystemParameters.WorkArea;
        double defaultLeft = work.Right - 430;
        double defaultTop = work.Top + 24;

        if (_settings.Current.FloatingToolbarLeft.HasValue && _settings.Current.FloatingToolbarTop.HasValue)
        {
            Left = Math.Clamp(_settings.Current.FloatingToolbarLeft.Value, work.Left, work.Right - Width);
            Top = Math.Clamp(_settings.Current.FloatingToolbarTop.Value, work.Top, work.Bottom - Height);
        }
        else
        {
            Left = defaultLeft;
            Top = defaultTop;
        }

        LocationChanged += (_, _) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _settings.Current.FloatingToolbarLeft = Left;
                _settings.Current.FloatingToolbarTop = Top;
                _settings.Save();
            }
        };
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
