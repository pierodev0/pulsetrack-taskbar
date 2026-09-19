using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class NormalFormTests : IDisposable
{
    private const string App = "Code";

    private readonly FakeForegroundSource _fg = new() { Current = App };
    private readonly ManualTickScheduler _sched = new();
    private readonly ForegroundTimer _timer;
    private readonly SessionCoordinator _coordinator;
    private readonly InMemoryConfigStore _config = new();
    private readonly List<AppMode> _requested = new();

    public NormalFormTests()
    {
        _timer = new ForegroundTimer(_fg, _sched);
        _coordinator = new SessionCoordinator(_timer, new FakeSessionStore(), new FakeClock());
    }

    public void Dispose() => _timer.Dispose();

    private TimerCommands Commands() =>
        new(_timer, _coordinator, _config, _ => Task.FromResult(AppSelection.Cancelled));

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
}
