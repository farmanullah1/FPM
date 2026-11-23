namespace PCManager.Services.Abstractions;

/// <summary>UI confirmation for destructive actions. The implementation lives in the WPF shell.</summary>
public interface IUserConfirmationService
{
    bool Confirm(string title, string message);
}
