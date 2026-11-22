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
public sealed partial class FloatingToolbarViewModel : ObservableObject, IDisposable
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

    [ObservableProperty]
    private string _loadText = "-";

    [ObservableProperty]
    private string _freeText = string.Empty;

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

    private void ApplySnapshot(MemorySnapshot snapshot)
    {
        LoadText = $"{snapshot.LoadPercent:0}%";
        FreeText = $"{ByteFormatter.Format((long)snapshot.AvailableBytes)} free";
    }

    public void Dispose() => _timer.Stop();
}
