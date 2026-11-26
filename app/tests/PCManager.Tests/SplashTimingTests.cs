using PCManager.Core;
using Xunit;

namespace PCManager.Tests;

public sealed class SplashTimingTests
{
    [Fact]
    public void A_fast_start_still_keeps_the_splash_for_the_minimum()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(600), SplashTiming.DelayFor(TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromMilliseconds(350), SplashTiming.DelayFor(TimeSpan.FromMilliseconds(250)));
        Assert.Equal(600, SplashTiming.MinimumVisible.TotalMilliseconds);
    }

    [Fact]
    public void A_slow_start_does_not_wait_for_the_minimum()
    {
        Assert.Equal(TimeSpan.Zero, SplashTiming.DelayFor(TimeSpan.FromMilliseconds(600)));
        Assert.Equal(TimeSpan.Zero, SplashTiming.DelayFor(TimeSpan.FromMilliseconds(1200)));
    }

    [Fact]
    public void The_three_second_limit_is_enforced()
    {
        Assert.False(SplashTiming.HasReachedLimit(TimeSpan.FromMilliseconds(2999)));
        Assert.True(SplashTiming.HasReachedLimit(TimeSpan.FromSeconds(3)));
        Assert.Equal(3000, SplashTiming.MaximumVisible.TotalMilliseconds);
    }
}
