using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class CountdownTimerTests : IDisposable
{
    private readonly ManualTickScheduler _sched = new();
    private readonly CountdownTimer _countdown;
    private readonly List<CountdownState> _emitted = new();
    private int _expiredCount;

    public CountdownTimerTests()
    {
        _countdown = new CountdownTimer(_sched);
        _countdown.Ticked += s => _emitted.Add(s);
        _countdown.Expired += () => _expiredCount++;
    }

    public void Dispose() => _countdown.Dispose();

    [Fact]
    public void StartsIdle()
    {
        Assert.False(_countdown.HasValue);
        Assert.False(_countdown.Running);
        Assert.False(_countdown.Finished);
        Assert.Equal(0, _countdown.TotalSeconds);
        Assert.Equal(0, _countdown.RemainingSeconds);
    }

    [Fact]
    public void Set_ConfiguresWithoutStarting()
    {
        _countdown.Set(10);

        Assert.True(_countdown.HasValue);
        Assert.False(_countdown.Running);
        Assert.False(_countdown.Finished);
        Assert.Equal(10, _countdown.TotalSeconds);
        Assert.Equal(10, _countdown.RemainingSeconds);
    }

    [Fact]
    public void Ticks_WithoutStart_DoNotCountDown()
    {
        _countdown.Set(10);
        _sched.Fire(5);

        Assert.Equal(10, _countdown.RemainingSeconds);
        Assert.False(_countdown.Running);
    }

    [Fact]
    public void Start_CountsDownPerTick()
    {
        _countdown.Set(3);
        _countdown.Start();
        _sched.Fire(2);

        Assert.True(_countdown.Running);
        Assert.Equal(2.0, _countdown.RemainingSeconds, 5);
    }

    [Fact]
    public void ReachesZero_StopsAndFinishes()
    {
        _countdown.Set(1);
        _countdown.Start();
        _sched.Fire(2);

        Assert.Equal(0, _countdown.RemainingSeconds);
        Assert.False(_countdown.Running);
        Assert.True(_countdown.Finished);
    }

    [Fact]
    public void Expiry_RaisesExpiredExactlyOnce()
    {
        _countdown.Set(1);
        _countdown.Start();
        _sched.Fire(2);
        _sched.Fire(5);

        Assert.Equal(1, _expiredCount);
        Assert.Equal(0, _countdown.RemainingSeconds);
    }

    [Fact]
    public void Pause_FreezesAndResumeContinues()
    {
        _countdown.Set(5);
        _countdown.Start();
        _sched.Fire(2);
        _countdown.Pause();

        Assert.False(_countdown.Running);
        _sched.Fire(4);
        Assert.Equal(4.0, _countdown.RemainingSeconds, 5);

        _countdown.Start();
        _sched.Fire(1);
        Assert.Equal(3.5, _countdown.RemainingSeconds, 5);
    }

    [Fact]
    public void Cancel_ClearsTheValue()
    {
        _countdown.Set(5);
        _countdown.Start();
        _sched.Fire(2);
        _countdown.Cancel();

        Assert.False(_countdown.HasValue);
        Assert.False(_countdown.Running);
        Assert.False(_countdown.Finished);
        Assert.Equal(0, _countdown.RemainingSeconds);

        _sched.Fire(3);
        Assert.Equal(0, _expiredCount);
    }

    [Fact]
    public void SetAfterExpiry_RearmsTheAlarm()
    {
        _countdown.Set(1);
        _countdown.Start();
        _sched.Fire(2);
        Assert.Equal(1, _expiredCount);

        _countdown.Set(2);
        Assert.False(_countdown.Finished);
        _countdown.Start();
        _sched.Fire(4);

        Assert.Equal(2, _expiredCount);
    }

    [Fact]
    public void Start_WhenFinished_DoesNothing()
    {
        _countdown.Set(1);
        _countdown.Start();
        _sched.Fire(2);

        _countdown.Start();

        Assert.False(_countdown.Running);
        Assert.Equal(1, _expiredCount);
    }

    [Fact]
    public void SetWhileRunning_StopsAndResets()
    {
        _countdown.Set(5);
        _countdown.Start();
        _sched.Fire(2);
        _countdown.Set(30);

        Assert.False(_countdown.Running);
        Assert.Equal(30, _countdown.RemainingSeconds);
    }

    [Fact]
    public void Toggle_PausesAndResumes()
    {
        _countdown.Set(5);
        _countdown.Start();
        _countdown.Toggle();
        Assert.False(_countdown.Running);

        _countdown.Toggle();
        Assert.True(_countdown.Running);
    }

    [Fact]
    public void Ticked_EmitsOnEveryMutation()
    {
        _countdown.Set(5);
        _countdown.Start();
        _sched.Fire(2);
        _countdown.Pause();

        Assert.Equal(5, _emitted.Count);
        Assert.Equal(5, _emitted[0].RemainingSeconds);
        Assert.True(_emitted[1].Running);
        Assert.Equal(4.5, _emitted[2].RemainingSeconds, 5);
        Assert.Equal(4.0, _emitted[3].RemainingSeconds, 5);
        Assert.False(_emitted[4].Running);
    }

    [Fact]
    public void Started_ReflectsWhetherTheTimerRanSinceLastSet()
    {
        Assert.False(_countdown.Started);

        _countdown.Set(5);
        Assert.False(_countdown.Started);

        _countdown.Start();
        Assert.True(_countdown.Started);

        _countdown.Pause();
        Assert.True(_countdown.Started);

        _countdown.Set(30);
        Assert.False(_countdown.Started);
    }

    [Fact]
    public void Expiry_EmitsFinalStateBeforeAlarm()
    {
        var sched = new ManualTickScheduler();
        using var countdown = new CountdownTimer(sched);
        var states = new List<CountdownState>();
        var order = new List<string>();
        countdown.Ticked += s => { states.Add(s); order.Add("ticked"); };
        countdown.Expired += () => order.Add("expired");

        countdown.Set(0.5);
        countdown.Start();
        sched.Fire(1);

        Assert.True(states[^1].Finished);
        Assert.Equal("ticked", order[^2]);
        Assert.Equal("expired", order[^1]);
    }
}
