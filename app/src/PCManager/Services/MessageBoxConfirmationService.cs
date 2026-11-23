using System.Windows;
using PCManager.Services.Abstractions;

namespace PCManager.Services;

/// <summary>The only place that asks the user to confirm a destructive action.</summary>
public sealed class MessageBoxConfirmationService : IUserConfirmationService
{
    public bool Confirm(string title, string message)
        => MessageBox.Show(
               message,
               title,
               MessageBoxButton.YesNo,
               MessageBoxImage.Warning,
               MessageBoxResult.No) == MessageBoxResult.Yes;
}
