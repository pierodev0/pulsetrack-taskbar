namespace PulseTrack.Taskbar;

public static class TimerViewStateFactory
{
    public static TimerViewState From(
        TimerTick tick, string? app, CountdownState? countdown = null, FocusMode focus = FocusMode.Stopwatch)
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
            Laps: laps,
            Countdown: BuildCountdown(countdown),
            Focus: focus);
    }

    private static CountdownView? BuildCountdown(CountdownState? state)
    {
        if (state is null || state.TotalSeconds <= 0)
            return null;

        return new CountdownView(
            ClockFormat.Hmmss(Math.Max(0, state.RemainingSeconds)),
            state.Running,
            state.Finished,
            state.TotalSeconds,
            state.Started);
    }

    public static string LapLabel(LapInfo lap) => $"Lap {lap.Number} · {ClockFormat.Hms(lap.DurationSeconds)}";
}
