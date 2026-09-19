using System.Threading.Channels;

namespace PulseTrack.Taskbar;

public sealed class ChannelSessionStore : ISessionStore
{
    private readonly ISessionRepository _inner;
    private readonly Channel<Func<Task>> _channel = Channel.CreateUnbounded<Func<Task>>();
    private readonly Task _loop;
    private bool _disposed;

    public ChannelSessionStore(ISessionRepository inner)
    {
        _inner = inner;
        _loop = RunLoopAsync();
    }

    private async Task RunLoopAsync()
    {
        await foreach (var work in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try { await work().ConfigureAwait(false); }
            catch { }
        }
    }

    private Task Enqueue(Action work)
    {
        return Enqueue(() =>
        {
            work();
            return Task.CompletedTask;
        });
    }

    private Task Enqueue(Func<Task> work)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite(async () =>
        {
            try
            {
                await work().ConfigureAwait(false);
                tcs.TrySetResult();
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }))
            throw new ObjectDisposedException(nameof(ChannelSessionStore));
        return tcs.Task;
    }

    private Task<T> Enqueue<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite(() =>
        {
            try { tcs.TrySetResult(work()); }
            catch (Exception ex) { tcs.TrySetException(ex); }
            return Task.CompletedTask;
        }))
            throw new ObjectDisposedException(nameof(ChannelSessionStore));
        return tcs.Task;
    }

    public Task<long> CreateSessionAsync(string? appName, string startTime, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Enqueue(() => _inner.CreateSession(appName, startTime));
    }

    public Task CloseSessionAsync(long id, string endTime, double durationSeconds, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Enqueue(() => { _inner.CloseSession(id, endTime, durationSeconds); });
    }

    public Task UpdateSessionDurationAsync(long id, double durationSeconds, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Enqueue(() => { _inner.UpdateSessionDuration(id, durationSeconds); });
    }

    public Task<long> CreateBlockAsync(long sessionId, string? appName, string label, string startTime, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Enqueue(() => _inner.CreateBlock(sessionId, appName, label, startTime));
    }

    public Task CloseBlockAsync(long id, string endTime, double durationSeconds, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Enqueue(() => { _inner.CloseBlock(id, endTime, durationSeconds); });
    }

    public Task UpdateBlockDurationAsync(long id, double durationSeconds, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Enqueue(() => { _inner.UpdateBlockDuration(id, durationSeconds); });
    }

    public Task CloseStaleActiveAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Enqueue(() => { _inner.CloseStaleActive(); });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Writer.Complete();
        await _loop.ConfigureAwait(false);
        _inner.Dispose();
    }
}
