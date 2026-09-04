namespace PulseTrack.Taskbar;

public interface ITaskbarWidget
{
    string Id { get; }
    string GetText();
    int GetWidthHint();
    void Refresh(TimerTick tick);
}
