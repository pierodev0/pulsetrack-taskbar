namespace PulseTrack.Taskbar;

public interface IConfigStore
{
    string ProbeLogPath { get; }
    AppConfig Load();
    void Save(AppConfig config);
    void Update(Action<AppConfig> mutate);
}
