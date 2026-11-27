namespace PCManager.Core.Models;

/// <summary>
/// Size and position of one window on one monitor at one DPI (W1). Values are WPF device-independent
/// units, keyed by monitor device name and DPI so a docked laptop and an external screen each keep
/// their own placement.
/// </summary>
public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

/// <summary>
/// Pure rules for picking and clamping a saved placement. Kept out of the window class so the
/// behaviour can be tested without a desktop.
/// </summary>
public static class WindowBoundsTable
{
    public static string Key(string monitorDevice, uint dpi) => $"{monitorDevice}@{dpi}";

    /// <summary>
    /// Exact monitor + DPI first; then any DPI on the same monitor (a DPI change must not lose the
    /// position); otherwise nothing, and the caller falls back to its default placement.
    /// </summary>
    public static WindowBounds? Resolve(
        IReadOnlyDictionary<string, WindowBounds> table,
        string monitorDevice,
        uint dpi)
    {
        if (table.Count == 0)
        {
            return null;
        }

        if (table.TryGetValue(Key(monitorDevice, dpi), out WindowBounds? exact))
        {
            return exact;
        }

        string prefix = monitorDevice + "@";

        foreach ((string key, WindowBounds value) in table)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Fits a saved placement into the monitor's work area without moving it off screen and without
    /// shrinking it below the window's minimum size.
    /// </summary>
    public static WindowBounds Clamp(
        WindowBounds desired,
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight,
        double minWidth,
        double minHeight)
    {
        double width = Math.Clamp(desired.Width > 0 ? desired.Width : minWidth, minWidth, Math.Max(minWidth, workWidth));
        double height = Math.Clamp(desired.Height > 0 ? desired.Height : minHeight, minHeight, Math.Max(minHeight, workHeight));
        double maxLeft = Math.Max(workLeft, workLeft + workWidth - width);
        double maxTop = Math.Max(workTop, workTop + workHeight - height);

        return new WindowBounds
        {
            Left = Math.Clamp(desired.Left, workLeft, maxLeft),
            Top = Math.Clamp(desired.Top, workTop, maxTop),
            Width = width,
            Height = height,
            Maximized = desired.Maximized,
        };
    }
}
