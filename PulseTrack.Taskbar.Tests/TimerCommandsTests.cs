using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class TimerCommandsTests : IDisposable
{
    private const string App = "Code";

    private readonly FakeForegroundSource _fg = new() { Current = App };
    private readonly ManualTickScheduler _sched = new();
    private readonly ForegroundTimer _timer;
    private readonly FakeSessionStore _store = new();
    private readonly SessionCoordinator _coordinator;
    private readonly InMemoryConfigStore _config = new();
    private readonly List<string?> _pickerRequests = new();
    private string? _pickerResult;

    public TimerCommandsTests()
    {
        _timer = new ForegroundTimer(_fg, _sched);
        _coordinator = new SessionCoordinator(_timer, _store, new FakeClock());
    }

    public void Dispose() => _timer.Dispose();

    private TimerCommands Create() => new(_timer, _coordinator, _config, current =>
    {
        _pickerRequests.Add(current);
        return Task.FromResult(_pickerResult);
    });

    [Fact]
    public void SuggestedApp_FallsBackToLastApp()
    {
        _config.Config.LastApp = "Chrome";
        var commands = Create();

        Assert.Equal("Chrome", commands.SuggestedApp);

        _timer.Start(App);
        Assert.Equal(App, commands.SuggestedApp);
    }

    [Fact]
    public async Task Toggle_WithoutApp_OpensPickerAndStarts()
    {
        _pickerResult = App;
        var commands = Create();

        await commands.ToggleStartPauseAsync();

        Assert.Single(_pickerRequests);
        Assert.Equal(App, _timer.SelectedApp);
        Assert.True(_timer.Running);
        Assert.Single(_store.Sessions);
    }

    [Fact]
    public async Task Toggle_WithoutApp_PickerCancelled_DoesNotStart()
    {
        _pickerResult = null;
        var commands = Create();

        await commands.ToggleStartPauseAsync();

        Assert.Single(_pickerRequests);
        Assert.Null(_timer.SelectedApp);
        Assert.Empty(_store.Sessions);
    }

    [Fact]
    public async Task Toggle_WithApp_PausesAndResumesWithoutPicker()
    {
        _pickerResult = App;
        var commands = Create();
        await commands.ToggleStartPauseAsync();

        await commands.ToggleStartPauseAsync();
        Assert.False(_timer.Running);
        Assert.Equal(App, _timer.SelectedApp);

        await commands.ToggleStartPauseAsync();
        Assert.True(_timer.Running);

        Assert.Single(_pickerRequests);
    }

    [Fact]
    public async Task PickApp_PersistsLastApp()
    {
        _pickerResult = App;
        var commands = Create();

        await commands.PickAppAsync();

        Assert.Equal(App, _config.Config.LastApp);
    }

    [Fact]
    public async Task PickApp_PassesSuggestedAppToPicker()
    {
        _config.Config.LastApp = "Chrome";
        _pickerResult = "Chrome";
        var commands = Create();

        await commands.PickAppAsync();

        Assert.Equal("Chrome", _pickerRequests[0]);
    }

    [Fact]
    public async Task Lap_DelegatesToCoordinator()
    {
        _pickerResult = App;
        var commands = Create();
        await commands.PickAppAsync();
        _fg.Current = App;
        _sched.Fire(4);

        await commands.LapAsync();

        Assert.Equal(2, _store.Blocks.Count);
        Assert.Equal("closed", _store.Blocks[0].Status);
    }

    [Fact]
    public async Task Stop_ClosesSessionAndResets()
    {
        _pickerResult = App;
        var commands = Create();
        await commands.PickAppAsync();
        _fg.Current = App;
        _sched.Fire(4);

        await commands.StopAsync();

        Assert.Null(_timer.SelectedApp);
        Assert.Null(_coordinator.SessionId);
        Assert.Equal("closed", _store.Sessions[0].Status);
    }

    [Fact]
    public async Task Stop_WithoutApp_DoesNothing()
    {
        var commands = Create();

        await commands.StopAsync();

        Assert.Empty(_store.Sessions);
    }

    [Fact]
    public async Task PickerFailure_IsLoggedAndSwallowed()
    {
        var logger = new FakeLogger();
        var commands = new TimerCommands(_timer, _coordinator, _config,
            _ => throw new InvalidOperationException("picker boom"), logger);

        var ex = await Record.ExceptionAsync(() => commands.PickAppAsync());

        Assert.Null(ex);
        Assert.Single(logger.Entries);
        Assert.Contains("picker boom", logger.Entries[0].Message);
    }
}
