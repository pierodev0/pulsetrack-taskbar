namespace PulseTrack.Taskbar;

public interface ITimerSurface : IDisposable
{
    AppMode Mode { get; }
    void Render(TimerViewState state);
    void SetVisible(bool visible);
}
