using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class TimerViewStateFactoryTests
{
    private static (FakeForegroundSource Fg, ManualTickScheduler Sched, ForegroundTimer Timer) CreateTimer()
    {
        var fg = new FakeForegroundSource { Current = "Code" };
        var sched = new ManualTickScheduler();
        var timer = new ForegroundTimer(fg, sched);
        return (fg, sched, timer);
    }

    [Fact]
    public void NoApp_StillShowsTheClock()
    {
        var state = TimerViewStateFactory.From(new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null);

        Assert.False(state.HasApp);
        Assert.Equal("Any app", state.AppDisplay);
        Assert.Equal("00:00:00", state.Clock);
        Assert.Equal("▶ 00:00:00", state.GlyphClock);
        Assert.Equal("", state.LapText);
        Assert.False(state.CanPause);
        Assert.False(state.CanLap);
        Assert.False(state.CanStop);
        Assert.Empty(state.Laps);
    }

    [Fact]
    public void NoApp_Running_ShowsRunningClock()
    {
        var state = TimerViewStateFactory.From(
            new TimerTick(75, true, 0, new[] { new LapInfo(1, "Lap 1", 75) }), null);

        Assert.False(state.HasApp);
        Assert.Equal("⏸ 00:01:15", state.GlyphClock);
        Assert.Equal("Lap 1 · 00:01:15", state.LapText);
        Assert.True(state.CanPause);
        Assert.True(state.CanLap);
        Assert.True(state.CanStop);
    }

    [Fact]
    public void EmptyAppName_IsTreatedAsNoApp()
    {
        var state = TimerViewStateFactory.From(new TimerTick(0, false, 0, Array.Empty<LapInfo>()), "");

        Assert.False(state.HasApp);
        Assert.Equal("Any app", state.AppDisplay);
    }

    [Fact]
    public void Running_ShowsPauseGlyphAndLap()
    {
        var (fg, sched, timer) = CreateTimer();
        using (timer)
        using (sched)
        {
            timer.Start("Code");
            fg.Current = "Code";
            sched.Fire(4);

            var state = TimerViewStateFactory.From(new TimerTick(2.0, true, 0, timer.Laps), timer.SelectedApp);

            Assert.True(state.HasApp);
            Assert.Equal("Code", state.AppDisplay);
            Assert.Equal("00:00:02", state.Clock);
            Assert.Equal("⏸", state.Glyph);
            Assert.Equal("⏸ 00:00:02", state.GlyphClock);
            Assert.Equal("Lap 1 · 00:00:02", state.LapText);
            Assert.True(state.CanPause);
            Assert.True(state.CanLap);
            Assert.True(state.CanStop);
        }
    }

    [Fact]
    public void Paused_ShowsPlayGlyph()
    {
        var (fg, sched, timer) = CreateTimer();
        using (timer)
        using (sched)
        {
            timer.Start("Code");
            fg.Current = "Code";
            sched.Fire(2);
            timer.Pause();

            var state = TimerViewStateFactory.From(new TimerTick(1.0, false, 0, timer.Laps), timer.SelectedApp);

            Assert.Equal("▶", state.Glyph);
            Assert.Equal("▶ 00:00:01", state.GlyphClock);
            Assert.False(state.CanPause);
            Assert.False(state.CanLap);
            Assert.True(state.CanStop);
        }
    }

    [Fact]
    public void SecondLap_ShowsCurrentLapNumber()
    {
        var (fg, sched, timer) = CreateTimer();
        using (timer)
        using (sched)
        {
            timer.Start("Code");
            fg.Current = "Code";
            sched.Fire(4);
            timer.Lap();
            sched.Fire(2);

            var state = TimerViewStateFactory.From(new TimerTick(3.0, true, 1, timer.Laps), timer.SelectedApp);

            Assert.Equal("Lap 2 · 00:00:01", state.LapText);
            Assert.Equal(2, state.Laps.Count);
        }
    }

    [Fact]
    public void SelectedAppWithoutLaps_HasNoLapText()
    {
        var state = TimerViewStateFactory.From(new TimerTick(0, true, 0, Array.Empty<LapInfo>()), "Code");

        Assert.Equal("", state.LapText);
    }

    [Fact]
    public void FreshState_CannotPauseLapOrStop()
    {
        var state = TimerViewStateFactory.From(new TimerTick(0, false, 0, Array.Empty<LapInfo>()), "Code");

        Assert.Equal("▶ 00:00:00", state.GlyphClock);
        Assert.False(state.CanPause);
        Assert.False(state.CanLap);
        Assert.False(state.CanStop);
    }

    [Fact]
    public void LapLabel_FormatsNumberAndDuration()
    {
        Assert.Equal("Lap 3 · 00:01:05", TimerViewStateFactory.LapLabel(new LapInfo(3, "Lap 3", 65)));
        Assert.Equal("Lap 1 · 00:00:00", TimerViewStateFactory.LapLabel(new LapInfo(1, "Lap 1", 0)));
    }

    [Fact]
    public void LapText_UsesTheSameLabelAsTheLapList()
    {
        var lap = new LapInfo(2, "Lap 2", 30);

        var state = TimerViewStateFactory.From(new TimerTick(30, true, 1, new[] { lap }), "Code");

        Assert.Equal(TimerViewStateFactory.LapLabel(lap), state.LapText);
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
    public void ClockFormat_Hmmss_DropsLeadingHourWhenZero()
    {
        Assert.Equal("0:02", ClockFormat.Hmmss(2));
        Assert.Equal("12:34", ClockFormat.Hmmss(754));
        Assert.Equal("1:00:00", ClockFormat.Hmmss(3600));
    }

    [Fact]
    public void NoCountdown_YieldsNullView()
    {
        var state = TimerViewStateFactory.From(new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null);

        Assert.Null(state.Countdown);
        Assert.False(state.HasCountdown);
    }

    [Fact]
    public void ZeroTotalCountdown_YieldsNullView()
    {
        var state = TimerViewStateFactory.From(
            new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null, new CountdownState(0, 0, false, false));

        Assert.Null(state.Countdown);
    }

    [Fact]
    public void ActiveCountdown_FormatsRemainingAndFlags()
    {
        var state = TimerViewStateFactory.From(
            new TimerTick(65, true, 0, Array.Empty<LapInfo>()), null, new CountdownState(300, 300, true, false));

        Assert.NotNull(state.Countdown);
        Assert.Equal("5:00", state.Countdown!.Text);
        Assert.True(state.Countdown.Running);
        Assert.False(state.Countdown.Finished);
        Assert.Equal("Timer 5:00", state.Countdown.Label);
    }

    [Fact]
    public void PausedCountdown_LabelShowsPaused()
    {
        var state = TimerViewStateFactory.From(
            new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null,
            new CountdownState(120, 300, false, false, Started: true));

        Assert.Equal("2:00", state.Countdown!.Text);
        Assert.Equal("Timer 2:00 (paused)", state.Countdown.Label);
    }

    [Fact]
    public void StagedCountdown_LabelShowsReady()
    {
        var state = TimerViewStateFactory.From(
            new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null,
            new CountdownState(120, 120, false, false));

        Assert.Equal("2:00", state.Countdown!.Text);
        Assert.False(state.Countdown.Started);
        Assert.Equal("Timer 2:00 — ready", state.Countdown.Label);
    }

    [Fact]
    public void FinishedCountdown_ShowsZeroAndTimeUps()
    {
        var state = TimerViewStateFactory.From(
            new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null, new CountdownState(0, 300, false, true));

        Assert.Equal("0:00", state.Countdown!.Text);
        Assert.True(state.Countdown.Finished);
        Assert.Equal("Timer 0:00 — time's up!", state.Countdown.Label);
    }

    [Fact]
    public void NegativeRemaining_IsClampedToZero()
    {
        var state = TimerViewStateFactory.From(
            new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null, new CountdownState(-0.5, 300, false, true));

        Assert.Equal("0:00", state.Countdown!.Text);
    }
}
