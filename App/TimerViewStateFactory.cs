namespace PulseTrack.Taskbar;

public static class TimerViewStateFactory
{
    public static TimerViewState From(TimerTick tick, string? app)
    {
        if (string.IsNullOrEmpty(app))
            app = null;

        var laps = tick.Laps;
        return new TimerViewState(
            AppName: app,
            Clock: ClockFormat.Hms(tick.ElapsedSeconds),
            Running: tick.Running,
            Glyph: tick.Running ? "⏸" : "▶",
            LapText: laps.Count > 0 ? LapLabel(laps[^1]) : "",
            CanPause: tick.Running,
            CanLap: tick.Running,
            CanStop: tick.ElapsedSeconds > 0 || tick.Running,
            Laps: laps);
    }

    public static string LapLabel(LapInfo lap) => $"Lap {lap.Number} · {ClockFormat.Hms(lap.DurationSeconds)}";
}
