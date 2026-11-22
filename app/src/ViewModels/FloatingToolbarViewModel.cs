using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// The always-on-top pill: live memory load, one-click Boost, screenshot, notepad and
/// calculator. It reuses the same services as the main window, so nothing is duplicated.
/// </summary>
public sealed class FloatingToolbarViewModel : ObservableObject, IDisposable
{
    private readonly IPcBoostService _boost;
    private readonly IToolboxService _toolbox;
    private readonly DispatcherTimer _timer;

    public FloatingToolbarViewModel(IPcBoostService boost, IToolboxService toolbox)
    {
        _boost = boost;
        _toolbox = toolbox;

        BoostCommand = new AsyncRelayCommand(BoostAsync);
        ScreenshotCommand = new AsyncRelayCommand(() => RunToolAsync("screenshot"));
        NotepadCommand = new AsyncRelayCommand(() => RunToolAsync("notepad"));
        CalculatorCommand = new AsyncRelayCommand(() => RunToolAsync("calculator"));

        Refresh();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    public AsyncRelayCommand BoostCommand { get; }
    public AsyncRelayCommand ScreenshotCommand { get; }
    public AsyncRelayCommand NotepadCommand { get; }
    public AsyncRelayCommand CalculatorCommand { get; }

    private string _loadText = "-";
    public string LoadText
    {
        get => _loadText;
        private set => SetProperty(ref _loadText, value);
    }

    private string _freeText = string.Empty;
    public string FreeText
    {
        get => _freeText;
        private set => SetProperty(ref _freeText, value);
    }

    private string _resultText = "Ready";
    public string ResultText
    {
        get => _resultText;
        private set => SetProperty(ref _resultText, value);
    }

    private void Refresh()
    {
        try
        {
            MemorySnapshot snapshot = _boost.GetMemorySnapshot();
            LoadText = $"{snapshot.LoadPercent:0}%";
            FreeText = $"{ByteFormatter.Format((long)snapshot.AvailableBytes)} free";
        }
        catch (Exception)
        {
            LoadText = "-";
        }
    }

    private async Task BoostAsync()
    {
        try
        {
            MemorySnapshot snapshot = await _boost.OptimizeAsync();
            LoadText = $"{snapshot.LoadPercent:0}%";
            ResultText = $"{ByteFormatter.Format(snapshot.ReclaimedBytes)} reclaimed";
            Refresh();
        }
        catch (Exception ex)
        {
            ResultText = ex.Message;
        }
    }

    private async Task RunToolAsync(string toolId)
    {
        OperationResult result = await _toolbox.LaunchAsync(toolId);
        ResultText = result.Message;
    }

    public void Dispose() => _timer.Stop();
}
