namespace PulseTrack.Taskbar;

public sealed class SessionCoordinator
{
    private readonly ForegroundTimer _timer;
    private readonly ISessionStore _store;
    private readonly IClock _clock;

    private double _lastLapStart;

    public long? SessionId { get; private set; }
    public long? BlockId { get; private set; }

    public SessionCoordinator(ForegroundTimer timer, ISessionStore store, IClock clock)
    {
        _timer = timer;
        _store = store;
        _clock = clock;
    }

    private string Now() => _clock.UtcNow.ToString("o");

    public async Task PickAppAsync(string appName, CancellationToken ct = default)
    {
        if (SessionId.HasValue)
            await StopAsync(ct).ConfigureAwait(false);
        _timer.Start(appName);
        var now = Now();
        SessionId = await _store.CreateSessionAsync(appName, now, ct).ConfigureAwait(false);
        BlockId = await _store.CreateBlockAsync(SessionId.Value, appName, "Lap 1", now, ct).ConfigureAwait(false);
        _lastLapStart = 0;
    }

    public bool ToggleStartPause()
    {
        if (_timer.SelectedApp == null)
            return false;
        if (_timer.Running)
            _timer.Pause();
        else
            _timer.Resume();
        return true;
    }

    public async Task LapAsync(CancellationToken ct = default)
    {
        if (!_timer.Running || !SessionId.HasValue || !BlockId.HasValue) return;
        var now = Now();
        var elapsed = _timer.ElapsedSeconds;
        await _store.CloseBlockAsync(BlockId.Value, now, elapsed - _lastLapStart, ct).ConfigureAwait(false);
        _timer.Lap();
        BlockId = await _store.CreateBlockAsync(SessionId.Value, _timer.SelectedApp!, $"Lap {_timer.Laps.Count}", now, ct).ConfigureAwait(false);
        _lastLapStart = elapsed;
        await _store.UpdateSessionDurationAsync(SessionId.Value, elapsed, ct).ConfigureAwait(false);
    }

    public async Task<double> StopAsync(CancellationToken ct = default)
    {
        if (_timer.SelectedApp == null) return 0;
        var now = Now();
        var elapsed = _timer.ElapsedSeconds;
        if (SessionId.HasValue && BlockId.HasValue)
        {
            await _store.CloseBlockAsync(BlockId.Value, now, elapsed - _lastLapStart, ct).ConfigureAwait(false);
            await _store.CloseSessionAsync(SessionId.Value, now, elapsed, ct).ConfigureAwait(false);
        }
        _timer.Stop();
        SessionId = null;
        BlockId = null;
        _lastLapStart = 0;
        return elapsed;
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (SessionId.HasValue && BlockId.HasValue)
        {
            var elapsed = _timer.ElapsedSeconds;
            await _store.UpdateSessionDurationAsync(SessionId.Value, elapsed, ct).ConfigureAwait(false);
            await _store.UpdateBlockDurationAsync(BlockId.Value, elapsed - _lastLapStart, ct).ConfigureAwait(false);
        }
    }
}
