using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Android;

namespace PCManager.ViewModels;

public sealed partial class AndroidPackageRow(AndroidPackage package) : ObservableObject
{
    public event Action? SelectionChanged;

    public AndroidPackage Package { get; } = package;
    public string Id => Package.Id;
    public string ListText => Package.ListText;
    public string RemovalText => Package.RemovalText;
    public string StateText => Package.StateText;
    public string Description => Package.Description;
    public string AutomationName =>
        $"Package {Id}, {ListText}, {RemovalText}, {StateText}";

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) =>
        SelectionChanged?.Invoke();
}
