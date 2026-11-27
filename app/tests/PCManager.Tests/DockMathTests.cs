using PCManager.Core.Models;
using Xunit;

namespace PCManager.Tests;

public sealed class DockMathTests
{
    private const double WorkLeft = 0;
    private const double WorkTop = 0;
    private const double WorkWidth = 1536;
    private const double WorkHeight = 864;

    private static WindowBounds Window(double left, double top) =>
        new() { Left = left, Top = top, Width = 448, Height = 96 };

    [Fact]
    public void ReleaseNearTheLeftEdgeSnapsLeft()
    {
        Assert.Equal(DockEdge.Left, DockMath.Resolve(Window(10, 400), WorkLeft, WorkTop, WorkWidth, WorkHeight));
    }

    [Fact]
    public void ReleaseNearTheRightEdgeSnapsRight()
    {
        Assert.Equal(
            DockEdge.Right,
            DockMath.Resolve(Window(WorkWidth - 448 - 5, 400), WorkLeft, WorkTop, WorkWidth, WorkHeight));
    }

    [Fact]
    public void ReleaseNearTopAndBottomSnapToThoseEdges()
    {
        Assert.Equal(DockEdge.Top, DockMath.Resolve(Window(600, 8), WorkLeft, WorkTop, WorkWidth, WorkHeight));
        Assert.Equal(
            DockEdge.Bottom,
            DockMath.Resolve(Window(600, WorkHeight - 96 - 6), WorkLeft, WorkTop, WorkWidth, WorkHeight));
    }

    [Fact]
    public void ReleaseInTheMiddleDoesNotDock()
    {
        Assert.Equal(DockEdge.None, DockMath.Resolve(Window(500, 300), WorkLeft, WorkTop, WorkWidth, WorkHeight));
    }

    [Fact]
    public void StripIsAThinTabCentredOnTheEdge()
    {
        WindowBounds left = DockMath.Strip(DockEdge.Left, WorkLeft, WorkTop, WorkWidth, WorkHeight);
        WindowBounds right = DockMath.Strip(DockEdge.Right, WorkLeft, WorkTop, WorkWidth, WorkHeight);
        WindowBounds top = DockMath.Strip(DockEdge.Top, WorkLeft, WorkTop, WorkWidth, WorkHeight);

        Assert.Equal(0, left.Left);
        Assert.Equal(32, left.Width);
        Assert.Equal((WorkHeight - 168) / 2, left.Top);
        Assert.Equal(WorkWidth - 32, right.Left);
        Assert.Equal(32, top.Height);
        Assert.Equal((WorkWidth - 168) / 2, top.Left);
    }

    [Fact]
    public void ExpandedSitsBesideTheStripAndStaysOnScreen()
    {
        WindowBounds expanded = DockMath.Expanded(
            DockEdge.Right, 448, 96, WorkLeft, WorkTop, WorkWidth, WorkHeight);

        Assert.Equal(WorkWidth - 32 - 448, expanded.Left);
        Assert.True(expanded.Top >= 0);
        Assert.Equal(448, expanded.Width);
    }
}
