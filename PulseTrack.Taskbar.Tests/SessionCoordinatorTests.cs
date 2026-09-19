using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class FakeSessionStore : ISessionStore
{
    public sealed record SessionRow(long Id, string? App, string Start, string? End, double Duration, string Status);
    public sealed record BlockRow(long Id, long SessionId, string? App, string Label, string Start, string? End, double Duration, string Status);

    public readonly List<SessionRow> Sessions = new();
    public readonly List<BlockRow> Blocks = new();
    private long _nextSession = 1;
    private long _nextBlock = 1;

    public Task<long> CreateSessionAsync(string? appName, string startTime, CancellationToken ct = default)
    {
        var id = _nextSession++;
        Sessions.Add(new SessionRow(id, appName, startTime, null, 0, "active"));
        return Task.FromResult(id);
    }

    public Task CloseSessionAsync(long id, string endTime, double durationSeconds, CancellationToken ct = default)
    {
        var s = Sessions.First(x => x.Id == id);
        Sessions[Sessions.IndexOf(s)] = s with { End = endTime, Duration = durationSeconds, Status = "closed" };
        return Task.CompletedTask;
    }

    public Task UpdateSessionDurationAsync(long id, double durationSeconds, CancellationToken ct = default)
    {
        var s = Sessions.First(x => x.Id == id);
        Sessions[Sessions.IndexOf(s)] = s with { Duration = durationSeconds };
        return Task.CompletedTask;
    }

    public Task<long> CreateBlockAsync(long sessionId, string? appName, string label, string startTime, CancellationToken ct = default)
    {
        var id = _nextBlock++;
        Blocks.Add(new BlockRow(id, sessionId, appName, label, startTime, null, 0, "active"));
        return Task.FromResult(id);
    }

    public Task CloseBlockAsync(long id, string endTime, double durationSeconds, CancellationToken ct = default)
    {
        var b = Blocks.First(x => x.Id == id);
        Blocks[Blocks.IndexOf(b)] = b with { End = endTime, Duration = durationSeconds, Status = "closed" };
        return Task.CompletedTask;
    }

    public Task UpdateBlockDurationAsync(long id, double durationSeconds, CancellationToken ct = default)
    {
        var b = Blocks.First(x => x.Id == id);
        Blocks[Blocks.IndexOf(b)] = b with { Duration = durationSeconds };
        return Task.CompletedTask;
    }

    public Task CloseStaleActiveAsync(CancellationToken ct = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class FakeClock : IClock
{
    public DateTimeOffset Current { get; set; } = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    public DateTimeOffset UtcNow => Current;
}

public sealed class SessionCoordinatorTests : IDisposable
{
    private const string App = "Code";
    private readonly FakeForegroundSource _fg = new() { Current = App };
    private readonly ManualTickScheduler _sched = new();
    private readonly ForegroundTimer _timer;
    private readonly FakeSessionStore _store = new();
    private readonly FakeClock _clock = new();
    private readonly SessionCoordinator _coordinator;

    public SessionCoordinatorTests()
    {
        _timer = new ForegroundTimer(_fg, _sched);
        _coordinator = new SessionCoordinator(_timer, _store, _clock);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    [Fact]
    public async Task Start_CreatesSessionAndStartsRunning()
    {
        await _coordinator.StartAsync(App);

        Assert.Equal(App, _timer.SelectedApp);
        Assert.True(_timer.Running);
        Assert.Equal(0, _timer.ElapsedSeconds);
        Assert.NotNull(_coordinator.SessionId);
        Assert.NotNull(_coordinator.BlockId);
        Assert.Single(_store.Sessions);
        Assert.Single(_store.Blocks);
        Assert.Equal(App, _store.Sessions[0].App);
        Assert.Equal("Lap 1", _store.Blocks[0].Label);
    }

    [Fact]
    public async Task Start_WithoutApp_CreatesSessionWithNullApp()
    {
        await _coordinator.StartAsync(null);

        Assert.Null(_timer.SelectedApp);
        Assert.True(_timer.Running);
        Assert.Single(_store.Sessions);
        Assert.Null(_store.Sessions[0].App);
        Assert.Null(_store.Blocks[0].App);
    }

    [Fact]
    public async Task Start_SwitchingApp_StopsPreviousSession()
    {
        await _coordinator.StartAsync(App);
        var firstSession = _coordinator.SessionId!.Value;

        await _coordinator.StartAsync("Other");

        var closed = _store.Sessions.First(s => s.Id == firstSession);
        Assert.Equal("closed", closed.Status);
        Assert.Equal("Other", _timer.SelectedApp);
    }

    [Fact]
    public void Toggle_WithoutSession_ReturnsFalse()
    {
        Assert.False(_coordinator.ToggleStartPause());
    }

    [Fact]
    public async Task Toggle_WithSession_PausesAndResumes()
    {
        await _coordinator.StartAsync(null);

        Assert.True(_coordinator.ToggleStartPause());
        Assert.False(_timer.Running);

        Assert.True(_coordinator.ToggleStartPause());
        Assert.True(_timer.Running);
    }

    [Fact]
    public async Task Lap_ClosesBlockAndOpensNew()
    {
        await _coordinator.StartAsync(App);
        _fg.Current = App;
        _sched.Fire(4);

        await _coordinator.LapAsync();

        Assert.Equal(2, _store.Blocks.Count);
        Assert.Equal("closed", _store.Blocks[0].Status);
        Assert.Equal(2.0, _store.Blocks[0].Duration, precision: 5);
        Assert.Equal("Lap 2", _store.Blocks[1].Label);
    }

    [Fact]
    public async Task Lap_WithoutApp_StillWorks()
    {
        await _coordinator.StartAsync(null);
        _sched.Fire(4);

        await _coordinator.LapAsync();

        Assert.Equal(2, _store.Blocks.Count);
        Assert.Equal("Lap 2", _store.Blocks[1].Label);
        Assert.Null(_store.Blocks[1].App);
    }

    [Fact]
    public async Task Stop_ClosesSessionAndBlockAndKeepsSelectedApp()
    {
        await _coordinator.StartAsync(App);
        _fg.Current = App;
        _sched.Fire(4);

        await _coordinator.StopAsync();

        Assert.Equal(App, _timer.SelectedApp);
        Assert.Null(_coordinator.SessionId);
        Assert.Null(_coordinator.BlockId);
        Assert.Equal("closed", _store.Sessions[0].Status);
        Assert.Equal(2.0, _store.Sessions[0].Duration, precision: 5);
        Assert.Equal("closed", _store.Blocks[0].Status);
    }

    [Fact]
    public async Task Stop_WithoutSession_DoesNothing()
    {
        var duration = await _coordinator.StopAsync();

        Assert.Equal(0, duration);
        Assert.Empty(_store.Sessions);
    }

    [Fact]
    public async Task Flush_UpdatesDurations()
    {
        await _coordinator.StartAsync(App);
        _fg.Current = App;
        _sched.Fire(4);

        await _coordinator.FlushAsync();

        Assert.Equal(2.0, _store.Sessions[0].Duration, precision: 5);
        Assert.Equal(2.0, _store.Blocks[0].Duration, precision: 5);
    }
}
