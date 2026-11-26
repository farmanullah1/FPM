using PCManager.Core;
using Xunit;

namespace PCManager.Tests;

public sealed class MotionPolicyTests
{
    private static MotionEnvironment Enabled() =>
        new(SystemAnimationsEnabled: true, ReduceMotion: false, BatterySaver: false,
            RemoteSession: false, LowResourceMode: false);

    [Fact]
    public void A_quiet_desktop_gets_full_motion()
    {
        Assert.Equal(MotionLevel.Full, MotionPolicy.Resolve(Enabled()));
        Assert.True(MotionPolicy.AllowsFades(MotionLevel.Full));
        Assert.True(MotionPolicy.AllowsTransforms(MotionLevel.Full));
        Assert.Equal(MotionPolicy.PageDuration, MotionPolicy.Duration(MotionLevel.Full, MotionPolicy.PageDuration));
    }

    [Fact]
    public void Windows_animations_off_means_no_motion()
    {
        var environment = Enabled() with { SystemAnimationsEnabled = false };

        Assert.Equal(MotionLevel.None, MotionPolicy.Resolve(environment));
        Assert.False(MotionPolicy.AllowsFades(MotionLevel.None));
        Assert.False(MotionPolicy.AllowsTransforms(MotionLevel.None));
        Assert.Equal(TimeSpan.Zero, MotionPolicy.Duration(MotionLevel.None, MotionPolicy.PageDuration));
    }

    [Fact]
    public void A_hidden_window_means_no_motion()
    {
        var environment = Enabled() with { WindowVisible = false };

        Assert.Equal(MotionLevel.None, MotionPolicy.Resolve(environment));
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Reduce_motion_battery_saver_rdp_and_low_resource_mode_downgrade_to_fades(
        bool reduceMotion, bool batterySaver, bool remoteSession, bool lowResource)
    {
        var environment = Enabled() with
        {
            ReduceMotion = reduceMotion,
            BatterySaver = batterySaver,
            RemoteSession = remoteSession,
            LowResourceMode = lowResource
        };

        Assert.Equal(MotionLevel.Reduced, MotionPolicy.Resolve(environment));
        Assert.True(MotionPolicy.AllowsFades(MotionLevel.Reduced));
        Assert.False(MotionPolicy.AllowsTransforms(MotionLevel.Reduced));
        Assert.Equal(MotionPolicy.StandardDuration,
            MotionPolicy.Duration(MotionLevel.Reduced, MotionPolicy.StandardDuration));
    }

    [Fact]
    public void Durations_follow_the_plan()
    {
        Assert.Equal(120, MotionPolicy.MicroDuration.TotalMilliseconds);
        Assert.Equal(200, MotionPolicy.StandardDuration.TotalMilliseconds);
        Assert.InRange(MotionPolicy.PageDuration.TotalMilliseconds, 180, 260);
        Assert.Equal(2200, MotionPolicy.HeroDuration.TotalMilliseconds);
    }

    [Fact]
    public void Stagger_starts_at_zero_and_caps_at_twelve_slots()
    {
        Assert.Equal(TimeSpan.Zero, MotionPolicy.StaggerDelay(0));
        Assert.Equal(TimeSpan.FromMilliseconds(30), MotionPolicy.StaggerDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(330), MotionPolicy.StaggerDelay(11));
        Assert.Equal(TimeSpan.FromMilliseconds(330), MotionPolicy.StaggerDelay(40));
    }
}
