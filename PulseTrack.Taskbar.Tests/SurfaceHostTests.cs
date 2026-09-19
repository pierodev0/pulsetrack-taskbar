using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class FakeSurface : ITimerSurface
{
    public FakeSurface(AppMode mode) => Mode = mode;

    public AppMode Mode { get; }
    public bool Visible { get; set; }
    public TimerViewState? LastRendered { get; private set; }
    public int RenderCount { get; private set; }
    public int SetVisibleCount { get; private set; }
    public bool Disposed { get; private set; }

    public void Render(TimerViewState state)
    {
        LastRendered = state;
        RenderCount++;
    }

    public void SetVisible(bool visible)
    {
        Visible = visible;
        SetVisibleCount++;
    }

    public void Dispose() => Disposed = true;
}

public sealed class SurfaceHostTests
{
    private static readonly TimerViewState _running =
        new("Code", "00:00:05", true, "⏸", "Lap 1 · 00:00:05", true, true, true, Array.Empty<LapInfo>());

    private static (SurfaceHost Host, FakeSurface Normal, FakeSurface Taskbar, FakeSurface Pip) Create(AppMode initial)
    {
        var normal = new FakeSurface(AppMode.Normal);
        var taskbar = new FakeSurface(AppMode.Taskbar);
        var pip = new FakeSurface(AppMode.Pip);
        var host = new SurfaceHost(new ITimerSurface[] { normal, taskbar, pip }, initial);
        return (host, normal, taskbar, pip);
    }

    [Theory]
    [InlineData(AppMode.Normal)]
    [InlineData(AppMode.Taskbar)]
    [InlineData(AppMode.Pip)]
    public void InitialMode_ShowsOnlyThatSurface(AppMode initial)
    {
        var (host, normal, taskbar, pip) = Create(initial);
        using (host)
        {
            Assert.True(normal.Visible == (initial == AppMode.Normal));
            Assert.True(taskbar.Visible == (initial == AppMode.Taskbar));
            Assert.True(pip.Visible == (initial == AppMode.Pip));
            Assert.Equal(initial, host.Mode);
        }
    }

    [Fact]
    public void InitialMode_RendersActiveSurfaceOnce()
    {
        var (host, normal, taskbar, _) = Create(AppMode.Normal);
        using (host)
        {
            Assert.Equal(1, normal.RenderCount);
            Assert.Equal(TimerViewState.Empty, normal.LastRendered);
            Assert.Equal(0, taskbar.RenderCount);
        }
    }

    [Fact]
    public void SetMode_HidesPreviousAndShowsNew()
    {
        var (host, normal, taskbar, _) = Create(AppMode.Normal);
        using (host)
        {
            host.SetMode(AppMode.Taskbar);

            Assert.Equal(AppMode.Taskbar, host.Mode);
            Assert.False(normal.Visible);
            Assert.True(taskbar.Visible);
        }
    }

    [Fact]
    public void SetMode_RendersNewSurfaceWithCachedState()
    {
        var (host, _, taskbar, _) = Create(AppMode.Normal);
        using (host)
        {
            host.Render(_running);

            host.SetMode(AppMode.Taskbar);

            Assert.Equal(_running, taskbar.LastRendered);
        }
    }

    [Fact]
    public void SetMode_RoundTrip_KeepsCachedState()
    {
        var (host, normal, _, pip) = Create(AppMode.Normal);
        using (host)
        {
            host.Render(_running);

            host.SetMode(AppMode.Pip);
            host.SetMode(AppMode.Normal);

            Assert.Equal(_running, pip.LastRendered);
            Assert.Equal(_running, normal.LastRendered);
        }
    }

    [Fact]
    public void Render_OnlyReachesActiveSurface()
    {
        var (host, normal, taskbar, pip) = Create(AppMode.Taskbar);
        using (host)
        {
            var taskbarBefore = taskbar.RenderCount;
            var normalBefore = normal.RenderCount;
            var pipBefore = pip.RenderCount;

            host.Render(_running);

            Assert.Equal(taskbarBefore + 1, taskbar.RenderCount);
            Assert.Equal(_running, taskbar.LastRendered);
            Assert.Equal(normalBefore, normal.RenderCount);
            Assert.Equal(pipBefore, pip.RenderCount);
        }
    }

    [Fact]
    public void Render_ReachesSurfaceShownLaterWithLatestState()
    {
        var (host, normal, taskbar, _) = Create(AppMode.Normal);
        using (host)
        {
            host.Render(_running);
            var older = normal.LastRendered;

            host.SetMode(AppMode.Taskbar);
            host.Render(TimerViewState.Empty);
            host.SetMode(AppMode.Normal);

            Assert.Equal(_running, older);
            Assert.Equal(TimerViewState.Empty, normal.LastRendered);
            Assert.Equal(TimerViewState.Empty, taskbar.LastRendered);
        }
    }

    [Fact]
    public void SetMode_FiresModeChangedOnlyOnRealChange()
    {
        var (host, _, _, _) = Create(AppMode.Normal);
        using (host)
        {
            var changes = new List<AppMode>();
            host.ModeChanged += mode => changes.Add(mode);

            host.SetMode(AppMode.Normal);
            host.SetMode(AppMode.Pip);
            host.SetMode(AppMode.Pip);
            host.SetMode(AppMode.Taskbar);

            Assert.Equal(new[] { AppMode.Pip, AppMode.Taskbar }, changes);
        }
    }

    [Fact]
    public void SetMode_AfterDispose_DoesNothing()
    {
        var (host, _, _, pip) = Create(AppMode.Normal);
        host.Dispose();

        host.SetMode(AppMode.Pip);

        Assert.Equal(AppMode.Normal, host.Mode);
        Assert.False(pip.Visible);
    }

    [Fact]
    public void ShowActive_TouchesOnlyActiveSurface()
    {
        var (host, normal, taskbar, _) = Create(AppMode.Normal);
        using (host)
        {
            var normalBefore = normal.SetVisibleCount;
            var taskbarBefore = taskbar.SetVisibleCount;

            host.ShowActive();

            Assert.Equal(normalBefore + 1, normal.SetVisibleCount);
            Assert.Equal(taskbarBefore, taskbar.SetVisibleCount);
        }
    }

    [Fact]
    public void Dispose_DisposesEverySurface()
    {
        var (host, normal, taskbar, pip) = Create(AppMode.Normal);

        host.Dispose();

        Assert.True(normal.Disposed);
        Assert.True(taskbar.Disposed);
        Assert.True(pip.Disposed);
    }

    [Fact]
    public void Render_SurfaceThrows_LogsAndKeepsWorking()
    {
        var logger = new FakeLogger();
        var host = new SurfaceHost(new ITimerSurface[] { new ThrowingSurface() }, AppMode.Normal, logger);
        var before = logger.Entries.Count;

        using (host)
        {
            var ex = Record.Exception(() => host.Render(_running));

            Assert.Null(ex);
            Assert.Equal(before + 1, logger.Entries.Count);
            Assert.All(logger.Entries, e => Assert.Equal("Host", e.Scope));
        }
    }

    private sealed class ThrowingSurface : ITimerSurface
    {
        public AppMode Mode => AppMode.Normal;
        public void Render(TimerViewState state) => throw new InvalidOperationException("render boom");
        public void SetVisible(bool visible) { }
        public void Dispose() { }
    }
}
