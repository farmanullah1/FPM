using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PCManager.Services;
using PCManager.ViewModels;

namespace PCManager.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        // Shared shield icon from the identity helper (no external .ico asset needed).
        AppIdentity.ApplyWindowIcon(this);

        PositionNearTray();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.LoadAsync();
    }

    /// <summary>Compact utility placement: the bottom-right corner of the work area, near the tray.</summary>
    private void PositionNearTray()
    {
        Rect area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, area.Right - Width - 12);
        Top = Math.Max(area.Top, area.Bottom - Height - 12);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // DragMove must only run for the primary button, otherwise the window throws.
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>Pins the utility above other windows; the icon and pill show the state.</summary>
    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Background = Topmost
            ? (Brush)FindResource("App.AccentSoft")
            : Brushes.Transparent;
        PinIcon.Fill = Topmost
            ? (Brush)FindResource("App.Accent")
            : (Brush)FindResource("App.Subtle");
        PinButton.ToolTip = Topmost ? "Unpin from top" : "Pin on top";
    }

    private void Minimise_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();
}
