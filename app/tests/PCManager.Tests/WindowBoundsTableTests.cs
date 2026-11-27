using PCManager.Core.Models;
using Xunit;

namespace PCManager.Tests;

public sealed class WindowBoundsTableTests
{
    [Fact]
    public void KeyCombinesMonitorAndDpi()
    {
        Assert.Equal(@"\\.\DISPLAY1@120", WindowBoundsTable.Key(@"\\.\DISPLAY1", 120));
    }

    [Fact]
    public void ResolvePrefersTheExactMonitorAndDpi()
    {
        var exact = new WindowBounds { Left = 10, Top = 20, Width = 560, Height = 780 };
        var other = new WindowBounds { Left = 900, Top = 20, Width = 560, Height = 780 };
        var table = new Dictionary<string, WindowBounds>
        {
            [WindowBoundsTable.Key(@"\\.\DISPLAY1", 96)] = other,
            [WindowBoundsTable.Key(@"\\.\DISPLAY1", 120)] = exact,
        };

        WindowBounds? resolved = WindowBoundsTable.Resolve(table, @"\\.\DISPLAY1", 120);

        Assert.Same(exact, resolved);
    }

    [Fact]
    public void ResolveFallsBackToTheSameMonitorAtAnotherDpi()
    {
        var saved = new WindowBounds { Left = 40, Top = 30, Width = 700, Height = 800 };
        var table = new Dictionary<string, WindowBounds>
        {
            [WindowBoundsTable.Key(@"\\.\DISPLAY2", 96)] = saved,
        };

        Assert.Same(saved, WindowBoundsTable.Resolve(table, @"\\.\DISPLAY2", 144));
    }

    [Fact]
    public void ResolveReturnsNullForAnUnknownMonitor()
    {
        var table = new Dictionary<string, WindowBounds>
        {
            [WindowBoundsTable.Key(@"\\.\DISPLAY1", 96)] = new WindowBounds { Width = 560, Height = 780 },
        };

        Assert.Null(WindowBoundsTable.Resolve(table, @"\\.\DISPLAY9", 96));
        Assert.Null(WindowBoundsTable.Resolve(new Dictionary<string, WindowBounds>(), @"\\.\DISPLAY1", 96));
    }

    [Fact]
    public void ClampKeepsAWindowInsideTheWorkArea()
    {
        var offScreen = new WindowBounds { Left = 5000, Top = -400, Width = 560, Height = 780 };

        WindowBounds clamped = WindowBoundsTable.Clamp(offScreen, 0, 0, 1536, 864, 360, 480);

        Assert.Equal(976, clamped.Left);
        Assert.Equal(0, clamped.Top);
        Assert.Equal(560, clamped.Width);
        Assert.Equal(780, clamped.Height);
    }

    [Fact]
    public void ClampShrinksBelowTheMinimumOnlyWhenTheWorkAreaIsSmaller()
    {
        var wide = new WindowBounds { Left = 0, Top = 0, Width = 1200, Height = 900 };

        WindowBounds clamped = WindowBoundsTable.Clamp(wide, 0, 0, 800, 600, 360, 480);

        Assert.Equal(800, clamped.Width);
        Assert.Equal(600, clamped.Height);
    }

    [Fact]
    public void ClampPreservesTheMaximizedFlag()
    {
        var maximized = new WindowBounds { Width = 560, Height = 780, Maximized = true };

        Assert.True(WindowBoundsTable.Clamp(maximized, 0, 0, 1536, 864, 360, 480).Maximized);
    }
}
