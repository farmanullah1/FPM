using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PCManager.Services;
using PCManager.Services.Abstractions;
using PCManager.Services.Desktop;
using PCManager.ViewModels;

namespace PCManager.Views;

/// <summary>
/// Small always-on-top pill. Closing it hides the window (the host recreates nothing), and
/// the position is kept and persisted to settings.
/// </summary>
public partial class FloatingToolbarWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;
    private DesktopFollowLoop? _desktopFollower;

    public FloatingToolbarWindow(FloatingToolbarViewModel viewModel, ISettingsService settings, IAppLogger logger)
    {
        InitializeComponent();

        _settings = settings;
        _logger = logger;
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

        SourceInitialized += (_, _) => StartDesktopFollower();
        Closed += (_, _) => _desktopFollower?.Dispose();

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

    private void StartDesktopFollower()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        _desktopFollower = DesktopFollowLoop.Start(
            () => handle,
            () => _settings.Current.ToolbarFollowDesktops && IsVisible,
            _logger);
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // W3: the toolbar's own × is the one place that turns the setting off, so it does not
        // silently come back on the next launch.
        _settings.Current.ShowFloatingToolbar = false;
        _settings.Save();
        _logger.Info("Toolbar closed by the user; floating toolbar disabled.");
        Hide();
    }
}
