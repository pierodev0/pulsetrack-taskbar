using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class FakeSessionRepository : ISessionRepository
{
    public sealed record SessionRow(long Id, string? App, string Start, string? End, double Duration, string Status);
    public sealed record BlockRow(long Id, long SessionId, string? App, string Label, string Start, string? End, double Duration, string Status);

    public readonly List<SessionRow> Sessions = new();
    public readonly List<BlockRow> Blocks = new();
    private long _nextSession = 1;
    private long _nextBlock = 1;

    public long CreateSession(string? appName, string startTime)
    {
        var id = _nextSession++;
        Sessions.Add(new SessionRow(id, appName, startTime, null, 0, "active"));
        return id;
    }

    public void CloseSession(long id, string endTime, double durationSeconds)
    {
        var s = Sessions.First(x => x.Id == id);
        Sessions[Sessions.IndexOf(s)] = s with { End = endTime, Duration = durationSeconds, Status = "closed" };
    }

    public void UpdateSessionDuration(long id, double durationSeconds)
    {
        var s = Sessions.First(x => x.Id == id);
        Sessions[Sessions.IndexOf(s)] = s with { Duration = durationSeconds };
    }

    public long CreateBlock(long sessionId, string? appName, string label, string startTime)
    {
        var id = _nextBlock++;
        Blocks.Add(new BlockRow(id, sessionId, appName, label, startTime, null, 0, "active"));
        return id;
    }

    public void CloseBlock(long id, string endTime, double durationSeconds)
    {
        var b = Blocks.First(x => x.Id == id);
        Blocks[Blocks.IndexOf(b)] = b with { End = endTime, Duration = durationSeconds, Status = "closed" };
    }

    public void UpdateBlockDuration(long id, double durationSeconds)
    {
        var b = Blocks.First(x => x.Id == id);
        Blocks[Blocks.IndexOf(b)] = b with { Duration = durationSeconds };
    }

    public void CloseStaleActive() { }

    public void Dispose() { }
}
