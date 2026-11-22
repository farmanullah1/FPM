using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Toolbox: the floating-toolbar switch and the tool grid (screenshot, recorder,
/// calculator, notepad, DNS flush, quick links).
/// </summary>
public sealed class ToolboxViewModel : ViewModelBase
{
    private readonly IToolboxService _toolbox;
    private readonly ISettingsService _settings;
    private readonly IFloatingToolbarHost _toolbarHost;
    private readonly IAppLogger _logger;

    public ToolboxViewModel(
        IToolboxService toolbox,
        ISettingsService settings,
        IFloatingToolbarHost toolbarHost,
        IAppLogger logger)
    {
        _toolbox = toolbox;
        _settings = settings;
        _toolbarHost = toolbarHost;
        _logger = logger;

        Tools = new ObservableCollection<ToolboxTool>(_toolbox.GetTools());
        LaunchToolCommand = new AsyncRelayCommand<string>(LaunchAsync, _ => !IsBusy);
        StatusMessage = "Pick a tool to run it.";
    }

    public ObservableCollection<ToolboxTool> Tools { get; }

    public AsyncRelayCommand<string> LaunchToolCommand { get; }

    /// <summary>Shows/hides the always-on-top pill and remembers the choice.</summary>
    public bool ShowFloatingToolbar
    {
        get => _settings.Current.ShowFloatingToolbar;
        set
        {
            if (_settings.Current.ShowFloatingToolbar == value)
            {
                return;
            }

            _settings.Current.ShowFloatingToolbar = value;
            _settings.Save();

            try
            {
                if (value)
                {
                    _toolbarHost.Show();
                }
                else
                {
                    _toolbarHost.Hide();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                _logger.Error("Floating toolbar toggle failed.", ex);
            }

            OnPropertyChanged();
            StatusMessage = value ? "Floating toolbar shown." : "Floating toolbar hidden.";
        }
    }

    public override Task LoadAsync()
    {
        // Reflect the saved state without re-triggering the setter.
        if (_settings.Current.ShowFloatingToolbar && !_toolbarHost.IsVisible)
        {
            _toolbarHost.Show();
        }

        return Task.CompletedTask;
    }

    protected override void OnBusyChanged() => LaunchToolCommand.NotifyCanExecuteChanged();

    private async Task LaunchAsync(string? toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return;
        }

        IsBusy = true;

        try
        {
            OperationResult result = await _toolbox.LaunchAsync(toolId);
            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _logger.Error($"Tool '{toolId}' failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
