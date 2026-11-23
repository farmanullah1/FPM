using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// The always-on-top pill: live memory load, live network download/upload throughput,
/// one-click Boost, screenshot, notepad, calculator, task manager and collapsible tool drawer.
/// </summary>
public sealed partial class FloatingToolbarViewModel : ObservableObject, IDisposable
{
    private readonly IPcBoostService _boost;
    private readonly IToolboxService _toolbox;
    private readonly INetworkThroughputService _network;
    private readonly DispatcherTimer _timer;

    public FloatingToolbarViewModel(
        IPcBoostService boost,
        IToolboxService toolbox,
        INetworkThroughputService network)
    {
        _boost = boost;
        _toolbox = toolbox;
        _network = network;

        BoostCommand = new AsyncRelayCommand(BoostAsync);
        ScreenshotCommand = new AsyncRelayCommand(() => RunToolAsync("screenshot"));
        NotepadCommand = new AsyncRelayCommand(() => RunToolAsync("notepad"));
        CalculatorCommand = new AsyncRelayCommand(() => RunToolAsync("calculator"));
        TaskManagerCommand = new AsyncRelayCommand(() => RunToolAsync("taskmgr"));
        ToggleDrawerCommand = new RelayCommand(() => IsDrawerOpen = !IsDrawerOpen);
        OpenFpmCommand = new RelayCommand(OpenFpm);

        Refresh();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    public AsyncRelayCommand BoostCommand { get; }
    public AsyncRelayCommand ScreenshotCommand { get; }
    public AsyncRelayCommand NotepadCommand { get; }
    public AsyncRelayCommand CalculatorCommand { get; }
    public AsyncRelayCommand TaskManagerCommand { get; }
    public RelayCommand ToggleDrawerCommand { get; }
    public RelayCommand OpenFpmCommand { get; }

    [ObservableProperty]
    private string _loadText = "-";

    [ObservableProperty]
    private string _freeText = string.Empty;

    [ObservableProperty]
    private string _downloadSpeedText = "↓ 0 KB/s";

    [ObservableProperty]
    private string _uploadSpeedText = "↑ 0 KB/s";

    [ObservableProperty]
    private string _networkTooltip = "Network throughput";

    [ObservableProperty]
    private bool _isDrawerOpen;

    private void Refresh()
    {
        try
        {
            ApplySnapshot(_boost.GetMemorySnapshot());
        }
        catch (Exception)
        {
            LoadText = "-";
        }

        try
        {
            _network.Refresh();
            NetworkThroughputSnapshot net = _network.GetCurrentThroughput();
            DownloadSpeedText = $"↓ {net.FormattedDownloadSpeed}";
            UploadSpeedText = $"↑ {net.FormattedUploadSpeed}";
            NetworkTooltip = $"Adapter: {net.ActiveAdapterName}\nDownload: {net.FormattedDownloadSpeed}\nUpload: {net.FormattedUploadSpeed}";
        }
        catch (Exception)
        {
            DownloadSpeedText = "↓ 0 KB/s";
            UploadSpeedText = "↑ 0 KB/s";
        }
    }

    private async Task BoostAsync()
    {
        try
        {
            ApplySnapshot((await _boost.OptimizeAsync()).After);
        }
        catch (Exception)
        {
            LoadText = "-";
        }
    }

    private Task RunToolAsync(string toolId) => _toolbox.LaunchAsync(toolId);

    private void OpenFpm()
    {
        Window? mainWindow = Application.Current?.MainWindow;
        if (mainWindow is null) return;

        mainWindow.Show();
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }
        mainWindow.Activate();
    }

    private void ApplySnapshot(MemorySnapshot snapshot)
    {
        LoadText = $"{snapshot.LoadPercent:0}%";
        FreeText = $"{ByteFormatter.Format((long)snapshot.AvailableBytes)} free";
    }

    public void Dispose() => _timer.Stop();
}
