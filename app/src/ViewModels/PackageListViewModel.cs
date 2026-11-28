using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Android;
using PCManager.Services.Android;

namespace PCManager.ViewModels;

/// <summary>
/// A2's small Debloat → Android surface: it loads the embedded/cached list without a request and
/// contacts GitHub only when the user presses Check for list updates.
/// </summary>
public sealed partial class PackageListViewModel : ObservableObject
{
    private readonly IPackageListProvider _provider;
    private bool _loading;

    public PackageListViewModel(IPackageListProvider provider)
    {
        _provider = provider;
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsUpdating);
    }

    public string ChineseOemNote =>
        "Chinese OEM note: Xiaomi, Huawei and some other manufacturers can reuse AOSP package " +
        "names for modified apps. Treat an AOSP recommendation as a hint to verify on the device, " +
        "not as permission to remove it.";

    public AsyncRelayCommand CheckForUpdatesCommand { get; }

    [ObservableProperty]
    private string _statusText = "UAD package list: not loaded.";

    [ObservableProperty]
    private string _infoText = "The embedded list is the fallback; no update is fetched automatically.";

    [ObservableProperty]
    private string _diffText = "No update checked. FPM only contacts GitHub when you press Check for list updates.";

    [ObservableProperty]
    private bool _isUpdating;

    partial void OnIsUpdatingChanged(bool value) => CheckForUpdatesCommand.NotifyCanExecuteChanged();

    public async Task LoadAsync()
    {
        if (_loading || _provider.Current is not null)
        {
            if (_provider.Current is not null)
            {
                Apply(_provider.Current);
            }

            return;
        }

        _loading = true;
        StatusText = "UAD package list: loading…";

        try
        {
            UadListCatalog catalog = await _provider.LoadAsync();
            Apply(catalog);

            if (_provider.LoadWarning is { Length: > 0 } warning)
            {
                DiffText = warning;
            }
        }
        catch (Exception ex)
        {
            StatusText = "UAD package list: unavailable.";
            InfoText = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (IsUpdating)
        {
            return;
        }

        IsUpdating = true;
        StatusText = "UAD package list: checking upstream…";

        try
        {
            UadListUpdateResult result = await _provider.UpdateAsync(userInitiated: true);

            if (result.Catalog is not null)
            {
                Apply(result.Catalog);
            }

            StatusText = result.Status switch
            {
                UadListUpdateStatus.Updated => "UAD package list: updated from upstream.",
                UadListUpdateStatus.AlreadyCurrent => "UAD package list: already current.",
                UadListUpdateStatus.Invalid => "UAD package list: upstream rejected by the schema.",
                UadListUpdateStatus.Failed => "UAD package list: update failed.",
                _ => "UAD package list: update unavailable.",
            };

            DiffText = result.Diff?.Summary ?? result.Message;

            if (result.Status == UadListUpdateStatus.Invalid ||
                result.Status == UadListUpdateStatus.Failed)
            {
                DiffText = result.Message;
            }
        }
        catch (Exception ex)
        {
            StatusText = "UAD package list: update failed.";
            DiffText = ex.Message;
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private void Apply(UadListCatalog catalog)
    {
        StatusText = $"UAD package list: {catalog.Packages.Count:N0} package ids ready.";
        InfoText =
            $"{catalog.SourceLabel} · {catalog.SourceRowCount:N0} source rows · " +
            $"{catalog.DuplicateIdCount} duplicate id(s) normalized · SHA-256 {catalog.ShortHash}";
    }
}
