namespace PulseTrack.Taskbar;

public sealed class SystemAlarm : IAlarm
{
    public void Play() => System.Media.SystemSounds.Exclamation.Play();
}
