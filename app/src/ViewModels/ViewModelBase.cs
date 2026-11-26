using CommunityToolkit.Mvvm.ComponentModel;

namespace PCManager.ViewModels;

/// <summary>
/// Base class for page view models. Provides the busy flag and the status line shown
/// at the bottom of every page.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;

    /// <summary>True while an operation is running; disables the page buttons.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        protected set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnBusyChanged();
            }
        }
    }

    private string _statusMessage = string.Empty;

    /// <summary>Short message shown under the page header.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        protected set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Called once when the page is first shown.</summary>
    public virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>True after the first successful load, so revisits can refresh later.</summary>
    public bool HasLoadedOnce { get; private set; }

    private Task? _loadTask;

    /// <summary>Loads data the first time the page is shown; later calls are no-ops.</summary>
    public Task EnsureLoadedAsync() => _loadTask ??= LoadOnceAsync();

    private async Task LoadOnceAsync()
    {
        await LoadAsync();
        HasLoadedOnce = true;
    }

    /// <summary>Override to refresh command CanExecute states when <see cref="IsBusy"/> changes.</summary>
    protected virtual void OnBusyChanged() { }
}
