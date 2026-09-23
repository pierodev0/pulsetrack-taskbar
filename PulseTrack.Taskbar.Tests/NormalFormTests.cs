using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class NormalFormTests : IDisposable
{
    private const string App = "Code";

    private readonly FakeForegroundSource _fg = new() { Current = App };
    private readonly ManualTickScheduler _sched = new();
    private readonly ForegroundTimer _timer;
    private readonly CountdownTimer _countdown;
    private readonly SessionCoordinator _coordinator;
    private readonly InMemoryConfigStore _config = new();
    private readonly List<AppMode> _requested = new();

    public NormalFormTests()
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

    private NormalForm Create(IAppLogger? logger = null) => new(Commands(), _config, _requested.Add, logger);

    private static TimerViewState StateWith(params LapInfo[] laps) => new(
        App,
        "00:00:01",
        true,
        "⏸",
        laps.Length > 0 ? TimerViewStateFactory.LapLabel(laps[^1]) : "",
        true,
        true,
        true,
        laps);

    [Fact]
    public void Mode_IsNormal()
    {
        using var form = Create();

        Assert.Equal(AppMode.Normal, form.Mode);
    }

    [Fact]
    public void SwitchTo_RequestsTaskbarMode()
    {
        using var form = Create();

        form.SwitchTo(AppMode.Taskbar);

        Assert.Equal(new[] { AppMode.Taskbar }, _requested);
    }

    [Fact]
    public void SwitchTo_RequestsPipMode()
    {
        using var form = Create();

        form.SwitchTo(AppMode.Pip);

        Assert.Equal(new[] { AppMode.Pip }, _requested);
    }

    [Fact]
    public void SwitchTo_CallbackThrows_IsLoggedAndSwallowed()
    {
        var logger = new FakeLogger();
        using var form = new NormalForm(Commands(), _config,
            _ => throw new InvalidOperationException("switch boom"), logger);

        var ex = Record.Exception(() => form.SwitchTo(AppMode.Pip));

        Assert.Null(ex);
        Assert.Single(logger.Entries);
        Assert.Equal("Normal", logger.Entries[0].Scope);
        Assert.Contains("switch boom", logger.Entries[0].Message);
    }

    [Fact]
    public void Render_EmptyState_ShowsIdleAndClearsRows()
    {
        using var form = Create();
        form.Render(StateWith(new LapInfo(1, "Lap 1", 1)));

        var ex = Record.Exception(() => form.Render(TimerViewState.Empty));

        Assert.Null(ex);
        Assert.Empty(form.LapRows);
    }

    [Fact]
    public void Render_AddsOneRowPerLap()
    {
        using var form = Create();

        form.Render(StateWith(new LapInfo(1, "Lap 1", 1)));
        Assert.Equal(new[] { "Lap 1 · 00:00:01" }, form.LapRows);

        form.Render(StateWith(new LapInfo(1, "Lap 1", 1), new LapInfo(2, "Lap 2", 1)));
        Assert.Equal(new[] { "Lap 1 · 00:00:01", "Lap 2 · 00:00:01" }, form.LapRows);
    }

    [Fact]
    public void Render_SameLapCount_UpdatesDurationWithoutAddingRows()
    {
        using var form = Create();

        form.Render(StateWith(new LapInfo(1, "Lap 1", 1)));
        form.Render(StateWith(new LapInfo(1, "Lap 1", 7)));

        Assert.Equal(new[] { "Lap 1 · 00:00:07" }, form.LapRows);
    }

    [Fact]
    public void Render_WhenStopped_StillAllowsStart()
    {
        using var form = Create();

        form.Render(TimerViewState.Empty);

        Assert.True(form.ToggleEnabled, "el boton de start debe estar habilitado aunque el cronometro este parado");
    }

    [Fact]
    public void ModeButtons_FitInsideTheClientArea()
    {
        using var form = Create();

        Assert.True(form.PipButtonBounds.Right <= form.ClientSize.Width, "el boton PiP se sale a lo ancho");
        Assert.True(form.TaskbarButtonBounds.Right <= form.ClientSize.Width, "el boton Taskbar se sale a lo ancho");
        Assert.True(form.PipButtonBounds.Bottom <= form.ClientSize.Height, "los botones se salen a lo alto");
    }

    [Fact]
    public void ModeButtons_DoNotOverlapEachOtherOrTheLapList()
    {
        using var form = Create();

        Assert.False(form.TaskbarButtonBounds.IntersectsWith(form.PipButtonBounds), "los botones de modo se pisan");
        Assert.False(form.LapListBounds.IntersectsWith(form.TaskbarButtonBounds), "la lista de laps pisa los botones");
        Assert.False(form.LapListBounds.IntersectsWith(form.PipButtonBounds), "la lista de laps pisa los botones");
    }

    [Fact]
    public void Render_RepeatedTicks_DoNotThrow()
    {
        using var form = Create();

        var ex = Record.Exception(() =>
        {
            for (int i = 0; i < 5; i++)
                form.Render(StateWith(new LapInfo(1, "Lap 1", i)));
        });

        Assert.Null(ex);
        Assert.Single(form.LapRows);
    }

    private TimerViewState StateWithCountdown() =>
        TimerViewStateFactory.From(
            new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null,
            new CountdownState(
                _countdown.RemainingSeconds, _countdown.TotalSeconds,
                _countdown.Running, _countdown.Finished, _countdown.Started));

    [Fact]
    public void Form_HasStopwatchAndTimerTabs()
    {
        using var form = Create();

        Assert.Equal(new[] { "Stopwatch", "Timer" }, form.TabNames);
        Assert.Equal(0, form.ActiveTabIndex);

        form.ActivateTabForTest(1);
        Assert.Equal(1, form.ActiveTabIndex);
    }

    [Fact]
    public void TimerTab_StartFromInputs_StartsCountdown()
    {
        using var form = Create();
        form.TimerMinutes.Value = 1;
        form.TimerSeconds.Value = 30;

        form.ClickTimerStartForTest();

        Assert.True(_countdown.Running);
        Assert.Equal(90, _countdown.RemainingSeconds);
        Assert.Equal(90, _config.Config.LastCountdownSeconds);
    }

    [Fact]
    public void TimerTab_Preset_StagesDurationWithoutStarting()
    {
        using var form = Create();

        form.ClickTimerPresetForTest(0);

        Assert.True(_countdown.HasValue);
        Assert.False(_countdown.Running);
        Assert.Equal(300, _countdown.RemainingSeconds);
        Assert.Equal(300, (double)form.TimerMinutes.Value * 60 + (double)form.TimerSeconds.Value);
    }

    [Fact]
    public void TypingInputs_StagesDurationAndShowsReadyState()
    {
        using var form = Create();

        form.TimerMinutes.Value = 2;
        form.TimerSeconds.Value = 0;

        Assert.True(_countdown.HasValue);
        Assert.False(_countdown.Running);
        Assert.Equal(120, _countdown.RemainingSeconds);
        Assert.Equal(120, _config.Config.LastCountdownSeconds);

        form.Render(StateWithCountdown());

        Assert.Equal("2:00", form.TimerClockText);
        Assert.Equal("Timer 2:00 — ready", form.TimerStatusText);
        Assert.Equal("▶ Start", form.TimerToggleButton.Text);
        Assert.True(form.TimerStartButton.Enabled);
    }

    [Fact]
    public void TimerTab_RenderWithoutCountdown_DisablesControls()
    {
        using var form = Create();

        form.Render(TimerViewState.Empty);

        Assert.Equal("0:00", form.TimerClockText);
        Assert.Equal("No timer set", form.TimerStatusText);
        Assert.False(form.TimerToggleButton.Enabled);
        Assert.False(form.TimerCancelButton.Enabled);
    }

    [Fact]
    public void TimerTab_RenderWithRunningCountdown_UpdatesDisplayAndInputs()
    {
        using var form = Create();

        form.Render(TimerViewStateFactory.From(
            new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null,
            new CountdownState(600, 600, true, false)));

        Assert.Equal("10:00", form.TimerClockText);
        Assert.Equal("Timer 10:00", form.TimerStatusText);
        Assert.True(form.TimerToggleButton.Enabled);
        Assert.Equal("⏸ Pause", form.TimerToggleButton.Text);
        Assert.True(form.TimerCancelButton.Enabled);
        Assert.False(form.TimerStartButton.Enabled);
        Assert.Equal(10, (int)form.TimerMinutes.Value);
        Assert.Equal(0, (int)form.TimerSeconds.Value);
    }

    [Fact]
    public void TimerTab_Finished_DisablesToggleButKeepsCancelAndRestart()
    {
        using var form = Create();

        form.Render(TimerViewStateFactory.From(
            new TimerTick(0, false, 0, Array.Empty<LapInfo>()), null,
            new CountdownState(0, 300, false, true)));

        Assert.Equal("0:00", form.TimerClockText);
        Assert.Equal("Timer 0:00 — time's up!", form.TimerStatusText);
        Assert.False(form.TimerToggleButton.Enabled);
        Assert.True(form.TimerCancelButton.Enabled);
        Assert.True(form.TimerStartButton.Enabled);
    }

    [Fact]
    public void TimerTab_ToggleAndCancel_WireThroughCommands()
    {
        using var form = Create();
        form.TimerMinutes.Value = 2;
        form.TimerSeconds.Value = 0;
        form.ClickTimerStartForTest();
        form.Render(StateWithCountdown());

        form.ClickTimerToggleForTest();
        Assert.False(_countdown.Running);

        form.Render(StateWithCountdown());
        Assert.Equal("▶ Resume", form.TimerToggleButton.Text);

        form.ClickTimerToggleForTest();
        Assert.True(_countdown.Running);

        form.Render(StateWithCountdown());
        form.ClickTimerCancelForTest();
        Assert.False(_countdown.HasValue);
    }

    [Fact]
    public void TimerTab_Controls_DoNotOverlapOrEscapeTheClientArea()
    {
        using var form = Create();
        var bounds = form.TimerControlBounds;

        for (int i = 0; i < bounds.Count; i++)
        {
            Assert.True(bounds[i].Right <= form.ClientSize.Width, "un control del timer se sale a lo ancho");
            Assert.True(bounds[i].Bottom <= form.ClientSize.Height, "un control del timer se sale a lo alto");

            for (int j = i + 1; j < bounds.Count; j++)
                Assert.False(bounds[i].IntersectsWith(bounds[j]), "los controles del timer se pisan");
        }
    }

    [Fact]
    public void Render_FocusTimer_SelectsTimerTabAndViceVersa()
    {
        using var form = Create();
        Assert.Equal(0, form.ActiveTabIndex);

        form.Render(StateWith() with { Focus = FocusMode.Timer });
        Assert.Equal(1, form.ActiveTabIndex);

        form.Render(StateWith());
        Assert.Equal(0, form.ActiveTabIndex);
    }

    [Fact]
    public void SelectingTimerTab_SetsFocusAndPersistsIt()
    {
        using var form = Create();

        form.ActivateTabForTest(1);
        Assert.Equal(FocusMode.Timer, _config.Config.Focus);

        form.ActivateTabForTest(0);
        Assert.Equal(FocusMode.Stopwatch, _config.Config.Focus);
    }
}
