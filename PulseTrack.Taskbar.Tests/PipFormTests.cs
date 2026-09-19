using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class PipFormTests : IDisposable
{
    private const string App = "Code";

    private readonly FakeForegroundSource _fg = new() { Current = App };
    private readonly ManualTickScheduler _sched = new();
    private readonly ForegroundTimer _timer;
    private readonly SessionCoordinator _coordinator;
    private readonly InMemoryConfigStore _config = new();
    private readonly List<string> _restoreCalls = new();

    public PipFormTests()
    {
        _timer = new ForegroundTimer(_fg, _sched);
        _coordinator = new SessionCoordinator(_timer, new FakeSessionStore(), new FakeClock());
    }

    public void Dispose() => _timer.Dispose();

    private TimerCommands Commands() =>
        new(_timer, _coordinator, _config, _ => Task.FromResult(AppSelection.Cancelled));

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
    public void Render_WithState_DoesNotThrow()
    {
        using var form = Create();
        var state = new TimerViewState(App, "00:00:05", true, "⏸", "Lap 1 · 00:00:05", true, true, true,
            new[] { new LapInfo(1, "Lap 1", 5) });

        var ex = Record.Exception(() => form.Render(state));

        Assert.Null(ex);
    }
}
