using PCManager.Services.Desktop;
using Xunit;

namespace PCManager.Tests;

public sealed class VirtualDesktopFollowerTests
{
    private static readonly IntPtr Window = new(42);

    private sealed class FakeDesktopApi : IVirtualDesktopManagerApi
    {
        public bool OnCurrentDesktop { get; set; }
        public Guid ForegroundDesktop { get; set; } = Guid.NewGuid();
        public bool HasForegroundDesktop { get; set; } = true;
        public bool MoveSucceeds { get; set; } = true;
        public int MoveCalls { get; private set; }
        public Guid LastMovedTo { get; private set; }
        public IntPtr ForegroundWindow { get; set; } = new(7);

        public bool IsOnCurrentDesktop(IntPtr window) => OnCurrentDesktop;

        public bool TryGetDesktopId(IntPtr window, out Guid desktopId)
        {
            desktopId = ForegroundDesktop;
            return HasForegroundDesktop;
        }

        public bool MoveToDesktop(IntPtr window, Guid desktopId)
        {
            MoveCalls++;
            LastMovedTo = desktopId;
            return MoveSucceeds;
        }
    }

    private static VirtualDesktopFollower Create(
        FakeDesktopApi api,
        bool follow = true,
        IntPtr? handle = null) =>
        new(api, () => handle ?? Window, () => follow, new TestLogger());

    [Fact]
    public void MovesWindowToTheForegroundDesktopWhenItIsElsewhere()
    {
        var api = new FakeDesktopApi { OnCurrentDesktop = false };
        using VirtualDesktopFollower follower = Create(api);

        Assert.True(follower.TickOnce());
        Assert.Equal(1, api.MoveCalls);
        Assert.Equal(api.ForegroundDesktop, api.LastMovedTo);
    }

    [Fact]
    public void DoesNothingWhenTheWindowIsAlreadyCurrent()
    {
        var api = new FakeDesktopApi { OnCurrentDesktop = true };
        using VirtualDesktopFollower follower = Create(api);

        Assert.False(follower.TickOnce());
        Assert.Equal(0, api.MoveCalls);
    }

    [Fact]
    public void DoesNothingWhenFollowingIsDisabled()
    {
        var api = new FakeDesktopApi { OnCurrentDesktop = false };
        using VirtualDesktopFollower follower = Create(api, follow: false);

        Assert.False(follower.TickOnce());
        Assert.Equal(0, api.MoveCalls);
    }

    [Fact]
    public void DoesNothingWithoutAWindowHandle()
    {
        var api = new FakeDesktopApi { OnCurrentDesktop = false };
        using VirtualDesktopFollower follower = Create(api, handle: IntPtr.Zero);

        Assert.False(follower.TickOnce());
        Assert.Equal(0, api.MoveCalls);
    }

    [Fact]
    public void LeavesTheWindowAloneWhenTheDesktopIdIsUnknown()
    {
        var api = new FakeDesktopApi { OnCurrentDesktop = false, HasForegroundDesktop = false };
        using VirtualDesktopFollower follower = Create(api);

        Assert.False(follower.TickOnce());
        Assert.Equal(0, api.MoveCalls);
    }

    [Fact]
    public void ReportsAFailedMoveWithoutThrowing()
    {
        var api = new FakeDesktopApi { OnCurrentDesktop = false, MoveSucceeds = false };
        using VirtualDesktopFollower follower = Create(api);

        Assert.False(follower.TickOnce());
        Assert.Equal(1, api.MoveCalls);
    }

    [Fact]
    public void DisposeIsSafeWhenTheApiIsNotDisposable()
    {
        var api = new FakeDesktopApi { OnCurrentDesktop = true };
        VirtualDesktopFollower follower = Create(api);

        follower.Dispose();
        follower.Dispose();
    }
}
