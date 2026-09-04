namespace PulseTrack.Taskbar;

public static class ClockFormat
{
    public static string Hms(double seconds)
    {
        var h = (int)(seconds / 3600);
        var m = (int)((seconds % 3600) / 60);
        var s = (int)(seconds % 60);
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    public static string Hmmss(double seconds)
    {
        var h = (int)(seconds / 3600);
        var m = (int)((seconds % 3600) / 60);
        var s = (int)(seconds % 60);
        return h > 0 ? $"{h}:{m:D2}:{s:D2}" : $"{m}:{s:D2}";
    }
}
