using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PCManager.Core.Models;
using PCManager.Services;
using PCManager.Services.Abstractions;
using PCManager.ViewModels;

namespace PCManager.Views;

/// <summary>
/// Main Shell Window for FPM (Card D2, §8.4).
/// Provides responsive breakpoints (360px - 1440+ px), custom WindowChrome,
/// snap layout support, and window position persistence.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;

        RestoreWindowBounds();

        Loaded += OnLoaded;
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        Closing += OnWindowClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        UpdateLayoutForWidth(ActualWidth);
        await _viewModel.LoadAsync();
    }

    private void RestoreWindowBounds()
    {
        Rect work = SystemParameters.WorkArea;
        AppSettings cfg = _settings.Current;

        double w = cfg.MainWindowWidth > 0 ? cfg.MainWindowWidth : 560;
        double h = cfg.MainWindowHeight > 0 ? cfg.MainWindowHeight : 780;

        Width = Math.Clamp(w, MinWidth, work.Width);
        Height = Math.Clamp(h, MinHeight, work.Height);

        if (cfg.MainWindowLeft.HasValue && cfg.MainWindowTop.HasValue)
        {
            Left = Math.Clamp(cfg.MainWindowLeft.Value, work.Left, Math.Max(work.Left, work.Right - Width));
            Top = Math.Clamp(cfg.MainWindowTop.Value, work.Top, Math.Max(work.Top, work.Bottom - Height));
        }
        else
        {
            // Default placement: bottom right corner near tray
            Left = Math.Max(work.Left, work.Right - Width - 16);
            Top = Math.Max(work.Top, work.Bottom - Height - 16);
        }

        if (cfg.MainWindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutForWidth(e.NewSize.Width);
    }

    private void UpdateLayoutForWidth(double width)
    {
        if (width < 480)
        {
            RailColumn.Width = new GridLength(52);
        }
        else if (width < 960)
        {
            RailColumn.Width = new GridLength(68);
        }
        else
        {
            RailColumn.Width = new GridLength(210);
        }

        _viewModel.UpdateResponsiveLayout(width);
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        MaximizeIconText.Text = WindowState == WindowState.Maximized ? "\u274F" : "\u25A2";
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.Current.MainWindowWidth = Width;
            _settings.Current.MainWindowHeight = Height;
            _settings.Current.MainWindowLeft = Left;
            _settings.Current.MainWindowTop = Top;
            _settings.Current.MainWindowMaximized = false;
        }
        else if (WindowState == WindowState.Maximized)
        {
            _settings.Current.MainWindowMaximized = true;
        }

        _settings.Save();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

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

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();
}
