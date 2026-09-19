using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class FakeForegroundSource : IForegroundSource
{
    public string? Current { get; set; }
    public string? GetForegroundProcessName() => Current;
}

public sealed class ManualTickScheduler : ITickScheduler
{
    private Action? _tick;
    public bool Started { get; private set; }

    public void Start(Action tick)
    {
        _tick = tick;
        Started = true;
    }

    public void Stop() => Started = false;

    public void Fire(int times = 1)
    {
        for (int i = 0; i < times; i++)
            _tick?.Invoke();
    }

    public void Dispose() { }
}

public sealed class ForegroundTimerTests : IDisposable
{
    private const string App = "Code";
    private readonly FakeForegroundSource _fg = new() { Current = App };
    private readonly ManualTickScheduler _sched = new();
    private readonly ForegroundTimer _timer;
    private readonly List<TimerTick> _emitted = new();

    public ForegroundTimerTests()
    {
        _timer = new ForegroundTimer(_fg, _sched);
        _timer.Ticked += t => _emitted.Add(t);
    }

    public void Dispose() => _timer.Dispose();

    [Fact]
    public void StartsIdle()
    {
        Assert.False(_timer.Running);
        Assert.Null(_timer.SelectedApp);
        Assert.Equal(0, _timer.ElapsedSeconds);
    }

    [Fact]
    public void Start_ResetsAndRuns()
    {
        _timer.Start("Other");
        _timer.Stop();

        _timer.Start(App);
        Assert.True(_timer.Running);
        Assert.Equal(App, _timer.SelectedApp);
        Assert.Equal(0, _timer.ElapsedSeconds);
        Assert.Single(_timer.Laps);
    }

    [Fact]
    public void Tick_AccumulatesOnlyWhenForegroundMatches()
    {
        _timer.Start(App);

        _fg.Current = App;
        _sched.Fire(4);
        Assert.Equal(2.0, _timer.ElapsedSeconds, precision: 5);

        _fg.Current = "Other";
        _sched.Fire(10);
        Assert.Equal(2.0, _timer.ElapsedSeconds, precision: 5);

        _fg.Current = null;
        _sched.Fire(10);
        Assert.Equal(2.0, _timer.ElapsedSeconds, precision: 5);
    }

    [Fact]
    public void Tick_MatchIsCaseInsensitive()
    {
        _timer.Start("code");
        _fg.Current = "CODE";
        _sched.Fire(2);
        Assert.Equal(1.0, _timer.ElapsedSeconds, precision: 5);
    }

    [Fact]
    public void Pause_KeepsElapsed_Resume_Continues()
    {
        _timer.Start(App);
        _fg.Current = App;
        _sched.Fire(2);

        _timer.Pause();
        Assert.False(_timer.Running);
        Assert.Equal(1.0, _timer.ElapsedSeconds, precision: 5);
        Assert.Equal(App, _timer.SelectedApp);

        _sched.Fire(10);
        Assert.Equal(1.0, _timer.ElapsedSeconds, precision: 5);

        _timer.Resume();
        _sched.Fire(2);
        Assert.Equal(2.0, _timer.ElapsedSeconds, precision: 5);
    }

    [Fact]
    public void Stop_ReturnsElapsedAndResetsButKeepsSelectedApp()
    {
        _timer.Start(App);
        _fg.Current = App;
        _sched.Fire(4);

        var duration = _timer.Stop();
        Assert.Equal(2.0, duration, precision: 5);
        Assert.False(_timer.Running);
        Assert.Equal(App, _timer.SelectedApp);
        Assert.Equal(0, _timer.ElapsedSeconds);
        Assert.Empty(_timer.Laps);
    }

    [Fact]
    public void Tick_WithoutApp_AccumulatesRegardlessOfForeground()
    {
        _timer.Start(null);

        _fg.Current = "Other";
        _sched.Fire(4);
        Assert.Equal(2.0, _timer.ElapsedSeconds, precision: 5);

        _fg.Current = null;
        _sched.Fire(2);
        Assert.Equal(3.0, _timer.ElapsedSeconds, precision: 5);
    }

    [Fact]
    public void Tick_WithAppFilter_StillIgnoresOtherApps()
    {
        _timer.Start(App);
        _fg.Current = "Other";
        _sched.Fire(4);

        Assert.Equal(0, _timer.ElapsedSeconds, precision: 5);
    }

    [Fact]
    public void PauseResume_WithoutApp_KeepsCounting()
    {
        _timer.Start(null);
        _sched.Fire(2);

        _timer.Pause();
        _sched.Fire(10);
        Assert.Equal(1.0, _timer.ElapsedSeconds, precision: 5);

        _timer.Resume();
        _sched.Fire(2);
        Assert.Equal(2.0, _timer.ElapsedSeconds, precision: 5);
    }

    [Fact]
    public void Lap_WithoutApp_SplitsDurations()
    {
        _timer.Start(null);
        _sched.Fire(4);
        _timer.Lap();
        _sched.Fire(2);

        Assert.Equal(2, _timer.Laps.Count);
        Assert.Equal(2.0, _timer.Laps[0].DurationSeconds, precision: 5);
        Assert.Equal(1.0, _timer.Laps[1].DurationSeconds, precision: 5);
        Assert.Equal(3.0, _timer.Laps.Sum(l => l.DurationSeconds), precision: 5);
    }

    [Fact]
    public void Resume_WithoutApp_StartsTicking()
    {
        _timer.Start(null);
        _timer.Pause();

        _timer.Resume();
        _sched.Fire(2);

        Assert.True(_timer.Running);
        Assert.Equal(1.0, _timer.ElapsedSeconds, precision: 5);
    }

    [Fact]
    public void Lap_SplitsDurationsThatSumToElapsed()
    {
        _timer.Start(App);
        _fg.Current = App;
        _sched.Fire(4); // 2.0s on Lap 1

        _timer.Lap();
        _sched.Fire(2); // +1.0s on Lap 2

        Assert.Equal(2, _timer.Laps.Count);
        Assert.Equal(3.0, _timer.ElapsedSeconds, precision: 5);
        Assert.Equal(3.0, _timer.Laps.Sum(l => l.DurationSeconds), precision: 5);
        Assert.Equal(2.0, _timer.Laps[0].DurationSeconds, precision: 5);
        Assert.Equal(1.0, _timer.Laps[1].DurationSeconds, precision: 5);
    }

    [Fact]
    public void Resume_DoesNotDuplicateTicks()
    {
        _timer.Start(App);
        _fg.Current = App;

        _timer.Pause();
        _timer.Resume();
        _timer.Pause();
        _timer.Resume();

        _sched.Fire(2);
        Assert.Equal(1.0, _timer.ElapsedSeconds, precision: 5);
    }

    [Fact]
    public void Tick_EmitsEventWithCurrentState()
    {
        _timer.Start(App);
        _fg.Current = App;
        _sched.Fire(2);

        var last = _emitted[^1];
        Assert.True(last.Running);
        Assert.Equal(1.0, last.ElapsedSeconds, precision: 5);
        Assert.Single(last.Laps);
    }
}
