using System.Windows;
using PCManager.Core.Models;
using PCManager.Services;

namespace PCManager.Views;

/// <summary>
/// Three-way exit question (W3): exit everything, keep the toolbar, or cancel. The remembered
/// choice is written to settings by the caller.
/// </summary>
public partial class ExitDialog : FpmWindow
{
    public ExitDialog()
    {
        InitializeComponent();
        AppIdentity.ApplyWindowIcon(this);
    }

    public ExitAction? Choice { get; private set; }

    public bool Remember { get; private set; }

    private void ExitAll_Click(object sender, RoutedEventArgs e) => Finish(ExitAction.ExitAll);

    private void KeepToolbar_Click(object sender, RoutedEventArgs e) => Finish(ExitAction.ExitKeepToolbar);

    private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(null);

    private void Finish(ExitAction? choice)
    {
        Choice = choice;
        Remember = RememberChoice.IsChecked == true;
        Close();
    }
}
