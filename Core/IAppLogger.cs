namespace PulseTrack.Taskbar;

public interface IAppLogger
{
    void Log(string scope, string message);
}

public sealed class NullLogger : IAppLogger
{
    public static readonly NullLogger Instance = new();
    public void Log(string scope, string message) { }
}
