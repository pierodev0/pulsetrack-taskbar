using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class PipFormTests : IDisposable
{
    private const string App = "Code";

    private readonly FakeForegroundSource _fg = new() { Current = App };
    private readonly ManualTickScheduler _sched = new();
    private readonly ForegroundTimer _timer;
    private readonly CountdownTimer _countdown;
    private readonly SessionCoordinator _coordinator;
    private readonly InMemoryConfigStore _config = new();
    private readonly List<string> _restoreCalls = new();

    public PipFormTests()
    {
        _timer = new ForegroundTimer(_fg, _sched);
        _countdown = new CountdownTimer(new ManualTickScheduler());
        _coordinator = new SessionCoordinator(_timer, new FakeSessionStore(), new FakeClock());
    }

    public void Dispose()
    {
        _timer.Dispose();
        _countdown.Dispose();
    }

    private TimerCommands Commands() =>
        new(_timer, _coordinator, _countdown, _config, _ => Task.FromResult(AppSelection.Cancelled));

    private PipForm Create(IAppLogger? logger = null) =>
        new(Commands(), _config, () => _restoreCalls.Add("restore"), logger);

    [Fact]
    public void Mode_IsPip()
    {
        using var form = Create();

        Assert.Equal(AppMode.Pip, form.Mode);
    }

    [Fact]
    public void RestoreToNormal_InvokesCallback()
    {
        using var form = Create();

        form.RestoreToNormal();

        Assert.Single(_restoreCalls);
    }

    [Fact]
    public void RestoreToNormal_CallbackThrows_IsLoggedAndSwallowed()
    {
        var logger = new FakeLogger();
        using var form = new PipForm(Commands(), _config,
            () => throw new InvalidOperationException("restore boom"), logger);

        var ex = Record.Exception(() => form.RestoreToNormal());

        Assert.Null(ex);
        Assert.Single(logger.Entries);
        Assert.Equal("Pip", logger.Entries[0].Scope);
        Assert.Contains("restore boom", logger.Entries[0].Message);
    }

    [Fact]
    public void Buttons_DoNotOverlapEachOther()
    {
        using var form = Create();
        var bounds = form.ButtonBounds;

        for (int i = 0; i < bounds.Count; i++)
        {
            for (int j = i + 1; j < bounds.Count; j++)
            {
                Assert.False(
                    bounds[i].IntersectsWith(bounds[j]),
                    $"los botones {i} y {j} se pisan: {bounds[i]} vs {bounds[j]}");
            }
        }
    }

    [Fact]
    public void Buttons_FitInsideTheClientArea()
    {
        using var form = Create();

        foreach (var bounds in form.ButtonBounds)
        {
            Assert.True(bounds.Right <= form.ClientSize.Width, $"boton fuera de ancho: {bounds}");
            Assert.True(bounds.Bottom <= form.ClientSize.Height, $"boton fuera de alto: {bounds}");
        }
    }

    [Fact]
    public void Layout_HasFiveSquarelyPackedButtons()
    {
        using var form = Create();

        Assert.Equal(5, form.ButtonBounds.Count);
    }

    [Fact]
    public void ExpandButton_SitsInTheGridSlotNextToStop()
    {
        using var form = Create();
        var expand = form.ExpandButtonBounds;

        Assert.Equal(26, expand.Width);
        Assert.True(expand.Left > form.ButtonBounds[2].Left, "el boton expandir debe ir a la derecha del de stop");
        Assert.Equal(form.ButtonBounds[2].Top, expand.Top);
    }

    [Fact]
    public void Render_WhenStopped_StillAllowsStart()
    {
        using var form = Create();

        form.Render(TimerViewState.Empty);

        Assert.True(form.ToggleEnabled, "el boton de start debe estar habilitado aunque el cronometro este parado");
        Assert.False(form.LapEnabled);
        Assert.False(form.StopEnabled);
    }

    [Fact]
    public void Render_WhenRunning_EnablesEveryAction()
    {
        using var form = Create();
        var running = new TimerViewState(App, "00:00:05", true, "⏸", "Lap 1 · 00:00:05", true, true, true,
            new[] { new LapInfo(1, "Lap 1", 5) });

        form.Render(running);

        Assert.True(form.ToggleEnabled);
        Assert.True(form.LapEnabled);
        Assert.True(form.StopEnabled);
    }

    [Fact]
    public void Render_WhenPaused_AllowsResumeButNotLap()
    {
        using var form = Create();
        var paused = new TimerViewState(App, "00:00:05", false, "▶", "Lap 1 · 00:00:05", false, false, true,
            new[] { new LapInfo(1, "Lap 1", 5) });

        form.Render(paused);

        Assert.True(form.ToggleEnabled);
        Assert.False(form.LapEnabled);
        Assert.True(form.StopEnabled);
    }

    [Fact]
    public void Render_WithState_DoesNotThrow()
    {
        using var form = Create();
        var state = new TimerViewState(App, "00:00:05", true, "⏸", "Lap 1 · 00:00:05", true, true, true,
            new[] { new LapInfo(1, "Lap 1", 5) });

        var ex = Record.Exception(() => form.Render(state));

        Assert.Null(ex);
    }

    private static TimerViewState StateWithFocus(FocusMode focus, CountdownState? countdown) =>
        TimerViewStateFactory.From(
            new TimerTick(65, true, 1, new[] { new LapInfo(1, "Lap 1", 65) }), App, countdown, focus);

    private static readonly CountdownState RunningCountdown = new(300, 600, true, false);

    [Fact]
    public void Render_FocusTimer_ShowsOnlyTheTimer()
    {
        using var form = Create();

        form.Render(StateWithFocus(FocusMode.Timer, RunningCountdown));

        Assert.Equal("5:00", form.PrimaryClockText);
        Assert.Equal("Timer 5:00", form.SecondaryText);
        Assert.Equal("", form.MiniCountdownText);
        Assert.Equal("⏸", form.PauseGlyph);
        Assert.False(form.LapEnabled);
        Assert.True(form.StopEnabled);
    }

    [Fact]
    public void Render_FocusTimerWithoutCountdown_ShowsOffStateAndHidesStop()
    {
        using var form = Create();

        form.Render(StateWithFocus(FocusMode.Timer, null));

        Assert.Equal("0:00", form.PrimaryClockText);
        Assert.Equal("No timer set", form.SecondaryText);
        Assert.False(form.StopEnabled);
        Assert.False(form.LapEnabled);
    }

    [Fact]
    public void Render_FocusStopwatch_KeepsTheStopwatchClock()
    {
        using var form = Create();

        form.Render(StateWithFocus(FocusMode.Stopwatch, RunningCountdown));

        Assert.Equal("00:01:05", form.PrimaryClockText);
        Assert.Equal("Lap 1 · 00:01:05", form.SecondaryText);
        Assert.Equal("Timer 5:00", form.MiniCountdownText);
        Assert.Equal("⏸", form.PauseGlyph);
        Assert.True(form.LapEnabled);
        Assert.True(form.StopEnabled);
    }

    [Fact]
    public async Task PauseWithFocusTimer_TogglesTheCountdownNotTheStopwatch()
    {
        _config.Config.Focus = FocusMode.Timer;
        using var form = Create();
        _countdown.Set(60);
        _countdown.Start();
        form.Render(StateWithFocus(FocusMode.Timer, RunningCountdown));

        await form.ClickPauseForTest();
        Assert.False(_countdown.Running);
        Assert.False(_timer.Running);

        await form.ClickPauseForTest();
        Assert.True(_countdown.Running);
    }

    [Fact]
    public async Task PauseWithFocusStopwatch_TogglesTheStopwatch()
    {
        using var form = Create();

        await form.ClickPauseForTest();
        Assert.True(_timer.Running);
        Assert.False(_countdown.HasValue);

        await form.ClickPauseForTest();
        Assert.False(_timer.Running);
    }

    [Fact]
    public async Task StagedTimer_ShowsReadyAndStartsFromPiP()
    {
        _config.Config.Focus = FocusMode.Timer;
        using var form = Create();
        _countdown.Set(120);

        form.Render(StateWithFocus(FocusMode.Timer, new CountdownState(120, 120, false, false)));

        Assert.Equal("2:00", form.PrimaryClockText);
        Assert.Equal("Timer 2:00 — ready", form.SecondaryText);
        Assert.Equal("▶", form.PauseGlyph);
        Assert.False(form.LapEnabled);
        Assert.True(form.StopEnabled);

        await form.ClickPauseForTest();

        Assert.True(_countdown.Running);
    }

    [Fact]
    public async Task StopWithFocusTimer_CancelsCountdownOnly()
    {
        _config.Config.Focus = FocusMode.Timer;
        using var form = Create();
        _timer.Start(App);
        _countdown.Set(60);
        _countdown.Start();
        form.Render(StateWithFocus(FocusMode.Timer, RunningCountdown));

        await form.ClickStopForTest();

        Assert.False(_countdown.HasValue);
        Assert.True(_timer.Running);
    }
}
