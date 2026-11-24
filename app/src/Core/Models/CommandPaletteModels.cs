namespace PCManager.Core.Models;

/// <summary>
/// A searchable action or destination in the Ctrl+K Command Palette.
/// </summary>
public sealed class CommandPaletteEntry
{
    public CommandPaletteEntry(
        string title,
        string category,
        string shortcut,
        Action action)
    {
        Title = title;
        Category = category;
        Shortcut = shortcut;
        Action = action;
    }

    public string Title { get; }
    public string Category { get; }
    public string Shortcut { get; }
    public Action Action { get; }
}
