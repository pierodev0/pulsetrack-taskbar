namespace PulseTrack.Taskbar;

public interface IConfigStore
{
    string ProbeLogPath { get; }
    OverlayConfig Load();
    void Save(OverlayConfig config);
}
