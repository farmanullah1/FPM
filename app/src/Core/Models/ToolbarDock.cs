using PCManager.Core.Models;

namespace PCManager.Core.Models;

/// <summary>Screen edge the floating toolbar is docked to (W4).</summary>
public enum DockEdge
{
    None,
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>
/// Docking rules, kept free of WPF so they can be tested without a desktop: where a release snaps,
/// the slim strip rectangle, and where the expanded drawer sits.
/// </summary>
public static class DockMath
{
    public const double SnapThreshold = 24;
    public const double StripThickness = 32;
    public const double StripLength = 168;

    /// <summary>The edge within the snap threshold of the released window, or None.</summary>
    public static DockEdge Resolve(
        WindowBounds window,
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight)
    {
        if (window.Width <= 0 || window.Height <= 0 || workWidth <= 0 || workHeight <= 0)
        {
            return DockEdge.None;
        }

        if (window.Left <= workLeft + SnapThreshold)
        {
            return DockEdge.Left;
        }

        if (window.Top <= workTop + SnapThreshold)
        {
            return DockEdge.Top;
        }

        if (window.Left + window.Width >= workLeft + workWidth - SnapThreshold)
        {
            return DockEdge.Right;
        }

        if (window.Top + window.Height >= workTop + workHeight - SnapThreshold)
        {
            return DockEdge.Bottom;
        }

        return DockEdge.None;
    }

    /// <summary>The slim strip: a centred tab on the chosen edge, 32 px thick.</summary>
    public static WindowBounds Strip(
        DockEdge edge,
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight)
    {
        double left = workLeft;
        double top = workTop;
        double width = StripThickness;
        double height = StripLength;

        switch (edge)
        {
            case DockEdge.Left:
                top = workTop + ((workHeight - StripLength) / 2);
                break;
            case DockEdge.Right:
                left = workLeft + workWidth - StripThickness;
                top = workTop + ((workHeight - StripLength) / 2);
                break;
            case DockEdge.Top:
                width = StripLength;
                height = StripThickness;
                left = workLeft + ((workWidth - StripLength) / 2);
                break;
            case DockEdge.Bottom:
                width = StripLength;
                height = StripThickness;
                left = workLeft + ((workWidth - StripLength) / 2);
                top = workTop + workHeight - StripThickness;
                break;
        }

        return new WindowBounds { Left = left, Top = top, Width = width, Height = height };
    }

    /// <summary>The full drawer next to the strip, clamped to stay on screen.</summary>
    public static WindowBounds Expanded(
        DockEdge edge,
        double width,
        double height,
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight)
    {
        double left = workLeft + ((workWidth - width) / 2);
        double top = workTop + ((workHeight - height) / 2);

        switch (edge)
        {
            case DockEdge.Left:
                left = workLeft + StripThickness;
                break;
            case DockEdge.Right:
                left = workLeft + workWidth - StripThickness - width;
                break;
            case DockEdge.Top:
                top = workTop + StripThickness;
                break;
            case DockEdge.Bottom:
                top = workTop + workHeight - StripThickness - height;
                break;
        }

        return WindowBoundsTable.Clamp(
            new WindowBounds { Left = left, Top = top, Width = width, Height = height },
            workLeft,
            workTop,
            workWidth,
            workHeight,
            minWidth: Math.Min(width, workWidth),
            minHeight: Math.Min(height, workHeight));
    }
}
