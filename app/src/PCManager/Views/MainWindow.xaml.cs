using System.Windows;
using System.Windows.Input;
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

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.LoadAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximise();
            return;
        }

        // DragMove must only run for the primary button, otherwise the window throws.
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimise_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximise_Click(object sender, RoutedEventArgs e)
        => ToggleMaximise();

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximise()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
