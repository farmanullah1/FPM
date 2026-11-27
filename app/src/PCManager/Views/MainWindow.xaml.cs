using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Motion;
using PCManager.Services;
using PCManager.Services.Desktop;
using MotionKit = PCManager.Motion.Motion;
using PCManager.Services.Abstractions;
using PCManager.ViewModels;

namespace PCManager.Views;

/// <summary>
/// Main Shell Window for FPM (Card D2, §8.4).
/// Provides responsive breakpoints (360px - 1440+ px), custom WindowChrome,
/// snap layout support, and window position persistence.
/// </summary>
public partial class MainWindow : FpmWindow
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly FrameTimingProbe _frameProbe = new();
    private readonly Dictionary<object, FrameworkElement> _pageViews = new();
    private FrameworkElement? _visiblePage;
    private DesktopFollowLoop? _desktopFollower;

    public MainWindow(MainViewModel viewModel, ISettingsService settings, IAppLogger logger)
    {
        InitializeComponent();

        // Lets Windows show Snap Layouts when the pointer rests on our custom maximize button.
        MaximizeHitTarget = MaximizeButton;

        logger.Info(
            $"Render tier {RenderCapability.Tier >> 16}; DWM composition " +
            $"{(FpmWindow.IsCompositionEnabled() ? "on" : "off")}.");

        _viewModel = viewModel;
        _settings = settings;
        _logger = logger;
        DataContext = viewModel;

        RestoreWindowBounds();

        SourceInitialized += (_, _) => StartDesktopFollower();
        Closed += (_, _) => _desktopFollower?.Dispose();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += OnLoaded;
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        Closing += OnWindowClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        UpdateLayoutForWidth(ActualWidth);

        FrameworkElement initial = GetPageView(_viewModel.CurrentPage);
        initial.Visibility = Visibility.Visible;
        initial.Opacity = 1;
        _visiblePage = initial;

        await Dispatcher.Yield(DispatcherPriority.Loaded);
        UpdateNavPill(animate: false);
        await _viewModel.LoadAsync();
    }

    /// <summary>Puts the caret in the palette search box whenever the palette opens.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsCommandPaletteOpen))
        {
            if (_viewModel.IsCommandPaletteOpen)
            {
                AnimatePaletteOpen();
                FocusPaletteQuery();
            }

            return;
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            _frameProbe.Measure(MotionPolicy.PageDuration + TimeSpan.FromMilliseconds(80), _logger.Info);
            AnimatePageChange();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.IsExpandedSidebar))
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateNavPill(animate: false)), DispatcherPriority.Loaded);
            return;
        }

        // A new query means a new result list; keep the highlight on the first match.
        if (e.PropertyName == nameof(MainViewModel.CommandPaletteQuery) && _viewModel.IsCommandPaletteOpen)
        {
            Dispatcher.BeginInvoke(new Action(SelectFirstMatch), DispatcherPriority.Input);
        }
    }

    /// <summary>
    /// Shared-axis page change (U6): the outgoing page fades out in 120 ms while the cached
    /// incoming page fades in and slides 16 px from the direction of travel. Reduced motion
    /// keeps the cross-fade; motion off swaps instantly. Cached views keep their scroll offset.
    /// </summary>
    private void AnimatePageChange()
    {
        FrameworkElement next = GetPageView(_viewModel.CurrentPage);
        FrameworkElement? previous = _visiblePage;

        if (ReferenceEquals(previous, next))
        {
            return;
        }

        _visiblePage = next;

        bool movingDown = previous is null
            || IndexOfPage(next.DataContext) >= IndexOfPage(previous.DataContext);

        Panel.SetZIndex(next, 1);
        next.Visibility = Visibility.Visible;

        var nextShift = (TranslateTransform)next.RenderTransform;
        double distance = MotionKit.Policy.AllowsTransforms ? 16d : 0d;
        var enterMotion = TimeSpan.FromMilliseconds(MotionPolicy.PageDuration.TotalMilliseconds);
        var exitMotion = TimeSpan.FromMilliseconds(120);
        var enterEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        var exitEase = new CubicEase { EasingMode = EasingMode.EaseIn };

        if (!MotionKit.Policy.AllowsFades)
        {
            next.BeginAnimation(UIElement.OpacityProperty, null);
            next.Opacity = 1;
            nextShift.BeginAnimation(TranslateTransform.YProperty, null);
            nextShift.Y = 0;
            Collapse(previous);
            Dispatcher.BeginInvoke(new Action(() => UpdateNavPill(animate: false)), DispatcherPriority.Loaded);
            return;
        }

        next.Opacity = 0;
        nextShift.Y = movingDown ? distance : -distance;
        next.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0d, 1d, enterMotion) { EasingFunction = enterEase });

        if (distance > 0)
        {
            nextShift.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(nextShift.Y, 0d, enterMotion) { EasingFunction = enterEase });
        }
        else
        {
            nextShift.Y = 0;
        }

        if (previous is not null)
        {
            Panel.SetZIndex(previous, 0);
            var previousShift = (TranslateTransform)previous.RenderTransform;
            var exit = new DoubleAnimation(1d, 0d, exitMotion) { EasingFunction = exitEase };
            exit.Completed += (_, _) => Collapse(previous);
            previous.BeginAnimation(UIElement.OpacityProperty, exit);

            if (distance > 0)
            {
                previousShift.BeginAnimation(
                    TranslateTransform.YProperty,
                    new DoubleAnimation(0d, movingDown ? -8d : 8d, exitMotion) { EasingFunction = exitEase });
            }
        }

        Dispatcher.BeginInvoke(new Action(() => UpdateNavPill(animate: true)), DispatcherPriority.Loaded);
    }

    private static void Collapse(FrameworkElement? page)
    {
        if (page is null)
        {
            return;
        }

        page.BeginAnimation(UIElement.OpacityProperty, null);
        page.Visibility = Visibility.Collapsed;
        page.Opacity = 1;

        if (page.RenderTransform is TranslateTransform shift)
        {
            shift.BeginAnimation(TranslateTransform.YProperty, null);
            shift.Y = 0;
        }
    }

    /// <summary>
        /// Builds every page view at idle time, one per dispatcher turn, so the first visit to a
        /// page does not pay the whole construction cost inside its transition.
        /// </summary>
    public void PrewarmPages()
    {
        List<ViewModelBase> pages = _viewModel.Pages
            .Concat(_viewModel.SettingsItems)
            .Select(item => item.Page)
            .ToList();

        Dispatcher.BeginInvoke(new Action(() => PrewarmNext(pages, 0)), DispatcherPriority.ApplicationIdle);
    }

    private void PrewarmNext(List<ViewModelBase> pages, int index)
    {
        if (index >= pages.Count)
        {
            return;
        }

        GetPageView(pages[index]);
        Dispatcher.BeginInvoke(new Action(() => PrewarmNext(pages, index + 1)), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>One visual per page, created once and reused for the life of the window.</summary>
    private FrameworkElement GetPageView(object page)
    {
        if (_pageViews.TryGetValue(page, out FrameworkElement? cached))
        {
            return cached;
        }

        UserControl view = page switch
        {
            HomeViewModel => new HomeView(),
            MemoryViewModel => new MemoryView(),
            ProtectionViewModel => new ProtectionView(),
            StorageViewModel => new StorageView(),
            AppsViewModel => new AppsView(),
            ToolboxViewModel => new ToolboxView(),
            SettingsViewModel => new SettingsView(),
            _ => throw new InvalidOperationException($"No view is registered for {page.GetType().Name}.")
        };

        view.DataContext = page;
        view.Visibility = Visibility.Collapsed;
        view.Opacity = 0;
        view.RenderTransform = new TranslateTransform();

        PageHost.Children.Add(view);
        _pageViews[page] = view;
        return view;
    }

    private int IndexOfPage(object? page)
    {
        if (page is null)
        {
            return 0;
        }

        int index = 0;

        foreach (NavigationItem item in _viewModel.Pages.Concat(_viewModel.SettingsItems))
        {
            if (ReferenceEquals(item.Page, page))
            {
                return index;
            }

            index++;
        }

        return index;
    }

    /// <summary>Places the rail's selection pill behind the selected item and slides it there.</summary>
    private void UpdateNavPill(bool animate)
    {
        ListBoxItem? container =
            PrimaryNav.ItemContainerGenerator.ContainerFromItem(_viewModel.SelectedPage) as ListBoxItem
            ?? SettingsNav.ItemContainerGenerator.ContainerFromItem(_viewModel.SelectedPage) as ListBoxItem;

        if (container is null || container.ActualHeight <= 0)
        {
            return;
        }

        Point origin = container.TranslatePoint(new Point(0, 0), NavPillLayer);
        NavPill.Width = Math.Max(0, container.ActualWidth);
        NavPill.Height = container.ActualHeight;
        NavPill.Opacity = 1;

        if (animate)
        {
            MotionKit.MoveTo(NavPillShift, toX: 0, toY: origin.Y, TimeSpan.FromMilliseconds(180));
            return;
        }

        NavPillShift.BeginAnimation(TranslateTransform.XProperty, null);
        NavPillShift.BeginAnimation(TranslateTransform.YProperty, null);
        NavPillShift.Y = origin.Y;
    }

    /// <summary>Fades and zooms the palette panel in through the motion kit.</summary>
    private void AnimatePaletteOpen()
    {
        MotionKit.FadeIn(PaletteOverlay, MotionPolicy.PaletteDuration);
        MotionKit.ScaleIn(PaletteScale, from: 0.98, MotionPolicy.PaletteDuration);
    }

    private void FocusPaletteQuery()
    {
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                PaletteQuery.Focus();
                Keyboard.Focus(PaletteQuery);
                PaletteQuery.SelectAll();
                SelectFirstMatch();
            }),
            DispatcherPriority.Input);
    }

    private void SelectFirstMatch()
    {
        if (PaletteList.Items.Count == 0)
        {
            PaletteList.SelectedIndex = -1;
            return;
        }

        PaletteList.SelectedIndex = 0;
        PaletteList.ScrollIntoView(PaletteList.Items[0]);
    }

    /// <summary>Escape closes the palette; arrows move the highlight; Enter runs the entry.</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsCommandPaletteOpen)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                _viewModel.CloseCommandPaletteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                RunSelectedEntry();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        int count = PaletteList.Items.Count;

        if (count == 0)
        {
            return;
        }

        int next = PaletteList.SelectedIndex < 0
            ? (delta > 0 ? 0 : count - 1)
            : Math.Clamp(PaletteList.SelectedIndex + delta, 0, count - 1);

        PaletteList.SelectedIndex = next;
        PaletteList.ScrollIntoView(PaletteList.Items[next]);
    }

    private void RunSelectedEntry()
    {
        object? selection = PaletteList.SelectedItem ??
                            (PaletteList.Items.Count > 0 ? PaletteList.Items[0] : null);

        if (selection is CommandPaletteEntry entry)
        {
            _viewModel.ExecutePaletteCommand.Execute(entry);
        }
    }

    private void StartDesktopFollower()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        _desktopFollower = DesktopFollowLoop.Start(
            () => handle,
            () => _settings.Current.MainWindowFollowDesktops && IsVisible && WindowState != WindowState.Minimized,
            _logger);
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
        Dispatcher.BeginInvoke(new Action(() => UpdateNavPill(animate: false)), DispatcherPriority.Loaded);
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
