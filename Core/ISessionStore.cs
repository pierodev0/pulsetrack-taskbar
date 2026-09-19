namespace PulseTrack.Taskbar;

public interface ISessionStore : IAsyncDisposable
{
    Task<long> CreateSessionAsync(string? appName, string startTime, CancellationToken ct = default);
    Task CloseSessionAsync(long id, string endTime, double durationSeconds, CancellationToken ct = default);
    Task UpdateSessionDurationAsync(long id, double durationSeconds, CancellationToken ct = default);
    Task<long> CreateBlockAsync(long sessionId, string? appName, string label, string startTime, CancellationToken ct = default);
    Task CloseBlockAsync(long id, string endTime, double durationSeconds, CancellationToken ct = default);
    Task UpdateBlockDurationAsync(long id, double durationSeconds, CancellationToken ct = default);
    Task CloseStaleActiveAsync(CancellationToken ct = default);
}
