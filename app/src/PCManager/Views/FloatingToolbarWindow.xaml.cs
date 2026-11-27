using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PCManager.Core.Models;
using PCManager.Services;
using PCManager.Services.Abstractions;
using PCManager.Services.Desktop;
using PCManager.ViewModels;

namespace PCManager.Views;

/// <summary>
/// Always-on-top floating toolbar (W4): drag to snap to any screen edge, 32 px docked tab that
/// expands into the full drawer on hover or click, auto-collapse, pin, opacity/always-on-top menu,
/// and automatic hiding while a fullscreen app or presentation is running.
/// </summary>
public partial class FloatingToolbarWindow : Window
{
    private static readonly TimeSpan SnapDuration = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(1500);

    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly FloatingToolbarViewModel _viewModel;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _fullscreenTimer;
    private DesktopFollowLoop? _desktopFollower;
    private DockEdge _edge;
    private bool _expanded;
    private bool _pinned;
    private bool _autoHidden;
    private bool _dockedThisDrag;

    public FloatingToolbarWindow(
        FloatingToolbarViewModel viewModel,
        ISettingsService settings,
        IAppLogger logger,
        BatteryStatusViewModel battery)
    {
        InitializeComponent();

        _settings = settings;
        _logger = logger;
        _viewModel = viewModel;
        DataContext = viewModel;
        AppIdentity.ApplyWindowIcon(this);

        RestoreFloatingPosition();

        _edge = settings.Current.ToolbarDock;
        _pinned = settings.Current.ToolbarPinned;
        Topmost = settings.Current.ToolbarTopmost;
        Opacity = Math.Clamp(settings.Current.ToolbarOpacity, 0.6, 1.0);
        TopmostMenuItem.IsChecked = Topmost;
        OpacitySlider.Value = Opacity;
        UpdatePinVisual();

        // T1: the toolbar owns a battery demand slot, so toolbar-only mode samples while visible.
        IsVisibleChanged += (_, _) => battery.SetVisible("toolbar", IsVisible);

        _collapseTimer = new DispatcherTimer { Interval = CollapseDelay };
        _collapseTimer.Tick += (_, _) => Collapse();

        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _fullscreenTimer.Tick += (_, _) => CheckFullscreen();

        SourceInitialized += (_, _) =>
        {
            StartDesktopFollower();
            ApplyDockOnStartup();
            _fullscreenTimer.Start();
        };

        Loaded += (_, _) => RefreshDockTab();
        Closed += (_, _) =>
        {
            _fullscreenTimer.Stop();
            _collapseTimer.Stop();
            _desktopFollower?.Dispose();
        };

        LocationChanged += (_, _) =>
        {
            if (WindowState == WindowState.Normal && _edge == DockEdge.None)
            {
                PersistFloatingPosition();
            }
        };
    }

    private void RestoreFloatingPosition()
    {
        Rect work = SystemParameters.WorkArea;
        double defaultLeft = work.Right - 430;
        double defaultTop = work.Top + 24;

        if (_settings.Current.FloatingToolbarLeft.HasValue && _settings.Current.FloatingToolbarTop.HasValue)
        {
            Left = Math.Clamp(_settings.Current.FloatingToolbarLeft.Value, work.Left, Math.Max(work.Left, work.Right - Width));
            Top = Math.Clamp(_settings.Current.FloatingToolbarTop.Value, work.Top, Math.Max(work.Top, work.Bottom - Height));
        }
        else
        {
            Left = defaultLeft;
            Top = defaultTop;
        }
    }

    private void ApplyDockOnStartup()
    {
        if (_edge == DockEdge.None)
        {
            return;
        }

        (Rect work, _, _) = WorkArea();
        WindowBounds strip = DockMath.Strip(_edge, work.Left, work.Top, work.Width, work.Height);
        SizeToContent = SizeToContent.Manual;
        Width = strip.Width;
        Height = strip.Height;
        Left = strip.Left;
        Top = strip.Top;
        ShowDockTab();
        _logger.Info($"Toolbar restored docked to the {_edge.ToString().ToLowerInvariant()} edge.");
    }

    private (Rect Work, string Device, uint Dpi) WorkArea()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;

        if (handle == IntPtr.Zero)
        {
            Rect fallback = SystemParameters.WorkArea;
            return (fallback, "primary", 96);
        }

        return MonitorIdentity.WorkAreaForWindow(handle);
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (_edge == DockEdge.None)
            {
                DockTo(DockEdge.Right);
            }
            else
            {
                Undock();
            }

            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            OnDragFinished();
        }
    }

    private void OnDragFinished()
    {
        if (_dockedThisDrag)
        {
            _dockedThisDrag = false;
            return;
        }

        (Rect work, _, _) = WorkArea();
        var placement = new WindowBounds { Left = Left, Top = Top, Width = ActualWidth, Height = ActualHeight };
        DockEdge edge = DockMath.Resolve(placement, work.Left, work.Top, work.Width, work.Height);

        if (edge == DockEdge.None)
        {
            if (_edge != DockEdge.None)
            {
                // Dragged away from the edge: undock and keep the new floating position.
                Undock();
                PersistFloatingPosition();
            }
            else
            {
                PersistFloatingPosition();
            }

            return;
        }

        DockTo(edge);
    }

    private void DockTo(DockEdge edge)
    {
        _edge = edge;
        _dockedThisDrag = true;
        _settings.Current.ToolbarDock = edge;
        _settings.Save();

        (Rect work, _, _) = WorkArea();
        WindowBounds strip = DockMath.Strip(edge, work.Left, work.Top, work.Width, work.Height);

        SizeToContent = SizeToContent.Manual;
        Width = strip.Width;
        Height = strip.Height;
        AnimateTo(strip, SnapDuration);
        ShowDockTab();

        _logger.Info($"Toolbar docked to the {edge.ToString().ToLowerInvariant()} edge.");
    }

    private void Undock()
    {
        _edge = DockEdge.None;
        _settings.Current.ToolbarDock = DockEdge.None;
        _settings.Save();

        SizeToContent = SizeToContent.WidthAndHeight;
        Width = double.NaN;
        Height = double.NaN;
        DockTab.Visibility = Visibility.Collapsed;
        CompactShell.Visibility = Visibility.Visible;
        _expanded = false;

        (Rect work, _, _) = WorkArea();
        Left = work.Left + ((work.Width - ActualWidth) / 2);
        Top = work.Top + ((work.Height - ActualHeight) / 2);
        _logger.Info("Toolbar undocked.");
    }

    private void ShowDockTab()
    {
        _collapseTimer.Stop();
        _expanded = false;
        _viewModel.IsDrawerOpen = false;
        CompactShell.Visibility = Visibility.Collapsed;
        DockTab.Visibility = Visibility.Visible;
    }

    private void Expand()
    {
        if (_edge == DockEdge.None || _expanded)
        {
            return;
        }

        _expanded = true;
        _viewModel.IsDrawerOpen = true;
        DockTab.Visibility = Visibility.Collapsed;
        CompactShell.Visibility = Visibility.Visible;
        SizeToContent = SizeToContent.WidthAndHeight;
        Width = double.NaN;
        Height = double.NaN;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            (Rect work, _, _) = WorkArea();
            WindowBounds expanded = DockMath.Expanded(
                _edge, ActualWidth, ActualHeight, work.Left, work.Top, work.Width, work.Height);
            AnimateTo(expanded, ExpandDuration);
            ArmCollapse();
        }), DispatcherPriority.Loaded);
    }

    private void Collapse()
    {
        _collapseTimer.Stop();

        if (_edge == DockEdge.None || _pinned || !_expanded)
        {
            return;
        }

        (Rect work, _, _) = WorkArea();
        WindowBounds strip = DockMath.Strip(_edge, work.Left, work.Top, work.Width, work.Height);

        SizeToContent = SizeToContent.Manual;
        Width = strip.Width;
        Height = strip.Height;
        AnimateTo(strip, ExpandDuration);
        ShowDockTab();
    }

    private void ArmCollapse()
    {
        if (_pinned)
        {
            return;
        }

        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void AnimateTo(WindowBounds target, TimeSpan duration)
    {
        // SizeToContent would override explicit Width/Height, so docking owns the size from here on.
        SizeToContent = SizeToContent.Manual;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation { Duration = duration, EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        double fromWidth = double.IsNaN(Width) ? ActualWidth : Width;
        double fromHeight = double.IsNaN(Height) ? ActualHeight : Height;

        BeginAnimation(LeftProperty, CreateAnimation(Left, target.Left, animation));
        BeginAnimation(TopProperty, CreateAnimation(Top, target.Top, animation));
        BeginAnimation(WidthProperty, CreateAnimation(fromWidth, target.Width, animation));
        BeginAnimation(HeightProperty, CreateAnimation(fromHeight, target.Height, animation));

        Left = target.Left;
        Top = target.Top;
        Width = target.Width;
        Height = target.Height;
    }

    private static DoubleAnimation CreateAnimation(double from, double to, DoubleAnimation template) =>
        new(from, to, template.Duration) { EasingFunction = template.EasingFunction, FillBehavior = FillBehavior.Stop };

    private void Root_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();

        if (_edge != DockEdge.None && !_expanded)
        {
            Expand();
        }
    }

    private void Root_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_edge != DockEdge.None && _expanded)
        {
            ArmCollapse();
        }
    }

    private void DockTab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Expand();

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        _settings.Current.ToolbarPinned = _pinned;
        _settings.Save();
        UpdatePinVisual();
        _logger.Info(_pinned ? "Toolbar pinned expanded." : "Toolbar auto-collapse enabled.");

        if (!_pinned)
        {
            ArmCollapse();
        }
    }

    private void UpdatePinVisual()
    {
        PinIcon.Fill = _pinned
            ? (Brush)FindResource("App.Accent")
            : (Brush)FindResource("App.Subtle");
        PinButton.ToolTip = _pinned ? "Unpin (auto-collapse)" : "Pin expanded";
    }

    private void DockLeft_Click(object sender, RoutedEventArgs e) => DockTo(DockEdge.Left);

    private void DockTop_Click(object sender, RoutedEventArgs e) => DockTo(DockEdge.Top);

    private void DockRight_Click(object sender, RoutedEventArgs e) => DockTo(DockEdge.Right);

    private void DockBottom_Click(object sender, RoutedEventArgs e) => DockTo(DockEdge.Bottom);

    private void Undock_Click(object sender, RoutedEventArgs e) => Undock();

    private void Opacity100_Click(object sender, RoutedEventArgs e) => SetOpacity(1.0);

    private void Opacity90_Click(object sender, RoutedEventArgs e) => SetOpacity(0.9);

    private void Opacity80_Click(object sender, RoutedEventArgs e) => SetOpacity(0.8);

    private void SetOpacity(double value)
    {
        if (_settings is null)
        {
            return;
        }

        value = Math.Clamp(value, 0.6, 1.0);
        Opacity = value;
        OpacitySlider.Value = value;
        _settings.Current.ToolbarOpacity = value;
        _settings.Save();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        SetOpacity(e.NewValue);

    private void Topmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostMenuItem.IsChecked;
        _settings.Current.ToolbarTopmost = Topmost;
        _settings.Save();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _settings.Current.FloatingToolbarLeft = null;
        _settings.Current.FloatingToolbarTop = null;
        _settings.Current.ToolbarDock = DockEdge.None;
        _settings.Save();

        _edge = DockEdge.None;
        SizeToContent = SizeToContent.WidthAndHeight;
        Width = double.NaN;
        Height = double.NaN;
        DockTab.Visibility = Visibility.Collapsed;
        CompactShell.Visibility = Visibility.Visible;

        Rect work = SystemParameters.WorkArea;
        Left = work.Right - 430;
        Top = work.Top + 24;
        _logger.Info("Toolbar position reset.");
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideToolbar();

    private void CheckFullscreen()
    {
        int state = QueryNotificationState();
        bool blocked = state is 2 or 3 or 4; // busy, D3D fullscreen, presentation mode

        if (blocked && IsVisible)
        {
            Hide();
            _autoHidden = true;
        }
        else if (!blocked && _autoHidden && _settings.Current.ShowFloatingToolbar)
        {
            Show();
            _autoHidden = false;
        }
    }

    private static int QueryNotificationState()
    {
        try
        {
            return SHQueryUserNotificationState(out int state) == 0 ? state : 0;
        }
        catch (DllNotFoundException)
        {
            return 0;
        }
    }

    private void PersistFloatingPosition()
    {
        _settings.Current.FloatingToolbarLeft = Left;
        _settings.Current.FloatingToolbarTop = Top;

        IntPtr handle = new WindowInteropHelper(this).Handle;

        if (handle != IntPtr.Zero)
        {
            (_, string device, uint dpi) = MonitorIdentity.WorkAreaForWindow(handle);
            _settings.Current.ToolbarBoundsByMonitor[WindowBoundsTable.Key(device, dpi)] = new WindowBounds
            {
                Left = Left,
                Top = Top,
                Width = ActualWidth,
                Height = ActualHeight,
            };
        }

        _settings.Save();
    }

    private void StartDesktopFollower()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        _desktopFollower = DesktopFollowLoop.Start(
            () => handle,
            () => _settings.Current.ToolbarFollowDesktops && IsVisible,
            _logger);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => HideToolbar();

    private void HideToolbar()
    {
        _settings.Current.ShowFloatingToolbar = false;
        _settings.Save();
        _logger.Info("Toolbar closed by the user; floating toolbar disabled.");
        Hide();
    }

    private void RefreshDockTab()
    {
        DockTab.ToolTip = "FPM — hover to expand";
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
