using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class PipViewModelTests
{
    private static PipViewModel Create(out FakeForegroundSource fg, out ManualTickScheduler sched, out ForegroundTimer timer)
    {
        fg = new FakeForegroundSource { Current = "Code" };
        sched = new ManualTickScheduler();
        timer = new ForegroundTimer(fg, sched);
        return new PipViewModel(timer);
    }

    [Fact]
    public void NoApp_ShowsIdleState()
    {
        var vm = Create(out _, out var sched, out var timer);
        using (timer)
        using (sched)
        {
            vm.Refresh(new TimerTick(0, false, 0, Array.Empty<LapInfo>()));

            Assert.Equal("Choose app", vm.AppName);
            Assert.Equal("00:00:00", vm.ElapsedText);
            Assert.Equal("", vm.LapText);
            Assert.False(vm.CanPause);
            Assert.False(vm.CanLap);
            Assert.False(vm.CanStop);
        }
    }

    [Fact]
    public void Running_ShowsPauseGlyphAndLap()
    {
        var vm = Create(out var fg, out var sched, out var timer);
        using (timer)
        using (sched)
        {
            timer.Start("Code");
            fg.Current = "Code";
            sched.Fire(4);
            vm.Refresh(new TimerTick(2.0, true, 0, timer.Laps));

            Assert.Equal("Code", vm.AppName);
            Assert.Equal("00:00:02", vm.ElapsedText);
            Assert.Equal("⏸", vm.PauseGlyph);
            Assert.Equal("Lap 1 · 00:00:02", vm.LapText);
            Assert.True(vm.CanPause);
            Assert.True(vm.CanLap);
            Assert.True(vm.CanStop);
        }
    }

    [Fact]
    public void Paused_ShowsPlayGlyph()
    {
        var vm = Create(out var fg, out var sched, out var timer);
        using (timer)
        using (sched)
        {
            timer.Start("Code");
            fg.Current = "Code";
            sched.Fire(2);
            timer.Pause();
            vm.Refresh(new TimerTick(1.0, false, 0, timer.Laps));

            Assert.Equal("▶", vm.PauseGlyph);
            Assert.False(vm.CanPause);
            Assert.True(vm.CanStop);
        }
    }

    [Fact]
    public void SecondLap_ShowsCurrentLapNumber()
    {
        var vm = Create(out var fg, out var sched, out var timer);
        using (timer)
        using (sched)
        {
            timer.Start("Code");
            fg.Current = "Code";
            sched.Fire(4);
            timer.Lap();
            sched.Fire(2);
            vm.Refresh(new TimerTick(3.0, true, 1, timer.Laps));

            Assert.Equal("Lap 2 · 00:00:01", vm.LapText);
        }
    }

    [Fact]
    public void ClockFormat_PadsCorrectly()
    {
        Assert.Equal("00:00:00", ClockFormat.Hms(0));
        Assert.Equal("00:00:02", ClockFormat.Hms(2));
        Assert.Equal("00:12:34", ClockFormat.Hms(754));
        Assert.Equal("01:00:00", ClockFormat.Hms(3600));
    }

    [Fact]
    public void Position_RoundTripsThroughConfig()
    {
        var store = new InMemoryConfigStore();
        var vm = new PipViewModel(null!);
        vm.Position = new Point(300, 200);
        vm.Visible = true;

        vm.SavePosition(store);
        var loaded = new PipViewModel(null!);
        loaded.LoadPosition(store);

        Assert.Equal(new Point(300, 200), loaded.Position);
        Assert.True(loaded.Visible);
    }
}
