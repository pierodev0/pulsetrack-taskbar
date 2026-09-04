namespace PulseTrack.Taskbar;

public sealed class TimerWidget : ITaskbarWidget
{
    private readonly ForegroundTimer _timer;

    public TimerWidget(ForegroundTimer timer)
    {
        _timer = timer;
    }

    public string Id => "timer";

    public int GetWidthHint() => 200;

    public void Refresh(TimerTick tick) { }

    public string GetText()
    {
        var app = _timer.SelectedApp;
        if (app == null)
            return "Choose app";
        var clock = FormatHms(_timer.ElapsedSeconds);
        if (_timer.Running)
            return $"⏸ {clock}";
        if (_timer.ElapsedSeconds > 0)
            return $"▶ {clock}";
        return "▶ 00:00:00";
    }

    private static string FormatHms(double seconds)
    {
        var h = (int)(seconds / 3600);
        var m = (int)((seconds % 3600) / 60);
        var s = (int)(seconds % 60);
        return $"{h:D2}:{m:D2}:{s:D2}";
    }
}
