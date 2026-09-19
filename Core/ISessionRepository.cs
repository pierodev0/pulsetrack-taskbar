namespace PulseTrack.Taskbar;

public interface ISessionRepository : IDisposable
{
    long CreateSession(string? appName, string startTime);
    void CloseSession(long id, string endTime, double durationSeconds);
    void UpdateSessionDuration(long id, double durationSeconds);
    long CreateBlock(long sessionId, string? appName, string label, string startTime);
    void CloseBlock(long id, string endTime, double durationSeconds);
    void UpdateBlockDuration(long id, double durationSeconds);
    void CloseStaleActive();
}
