using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// One startup entry. Toggling the checkbox immediately writes the StartupApproved
/// registry value (or renames the shortcut / enables the scheduled task) and reverts
/// the checkbox when Windows refuses the change.
/// </summary>
public sealed class StartupAppViewModel : ObservableObject
{
    private readonly IProcessService _processService;
    private readonly Action<string> _report;
    private bool _suppressWrite;
    private bool _isEnabled;

    public StartupAppViewModel(StartupApp model, IProcessService processService, Action<string> report)
    {
        Model = model;
        _processService = processService;
        _report = report;
        _isEnabled = model.IsEnabled;
    }

    public StartupApp Model { get; }

    public string Name => Model.Name;

    public string Command => Model.Command;

    public bool IsSystem => Model.IsSystem;

    /// <summary>Built-in Windows tasks are shown but cannot be toggled.</summary>
    public bool CanToggle => !Model.IsSystem;

    public string SourceText => Model.Source switch
    {
        StartupSource.RegistryCurrentUser => "User registry (Run)",
        StartupSource.RegistryLocalMachine => "System registry (Run)",
        StartupSource.TaskScheduler => "Task Scheduler",
        StartupSource.StartupFolder => "Startup folder",
        _ => "Unknown"
    };

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value) || _suppressWrite)
            {
                return;
            }

            _ = ApplyAsync(value);
        }
    }

    private async Task ApplyAsync(bool enabled)
    {
        try
        {
            OperationResult result = await _processService.SetStartupAppEnabledAsync(Model, enabled);

            if (!result.Success)
            {
                Revert(enabled);
            }
            else
            {
                Model.IsEnabled = enabled;
            }

            _report(result.Message);
        }
        catch (Exception ex)
        {
            Revert(enabled);
            _report(ex.Message);
        }
    }

    private void Revert(bool attempted)
    {
        _suppressWrite = true;
        IsEnabled = !attempted;
        _suppressWrite = false;
    }
}
