using PCManager.Core;
using Xunit;

namespace PCManager.Tests;

public sealed class MotionPolicyTests
{
    [Fact]
    public void Motion_runs_when_windows_animations_are_enabled()
    {
        Assert.True(MotionPolicy.ShouldAnimate(true));
        Assert.Equal(MotionPolicy.PageDuration, MotionPolicy.Duration(true, MotionPolicy.PageDuration));
        Assert.Equal(MotionPolicy.PaletteDuration, MotionPolicy.Duration(true, MotionPolicy.PaletteDuration));
    }

    [Fact]
    public void Motion_is_skipped_when_reduced_motion_is_requested()
    {
        Assert.False(MotionPolicy.ShouldAnimate(false));
        Assert.Equal(TimeSpan.Zero, MotionPolicy.Duration(false, MotionPolicy.PageDuration));
        Assert.Equal(TimeSpan.Zero, MotionPolicy.Duration(false, MotionPolicy.PaletteDuration));
    }
}
