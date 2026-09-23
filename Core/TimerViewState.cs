namespace PulseTrack.Taskbar;

public sealed record CountdownView(string Text, bool Running, bool Finished, double TotalSeconds, bool Started = false)
{
    public string Label => Finished
        ? $"Timer {Text} — time's up!"
        : Running
            ? $"Timer {Text}"
            : Started
                ? $"Timer {Text} (paused)"
                : $"Timer {Text} — ready";
}

public sealed record TimerViewState(
    string? AppName,
    string Clock,
    bool Running,
    string Glyph,
    string LapText,
    bool CanPause,
    bool CanLap,
    bool CanStop,
    IReadOnlyList<LapInfo> Laps,
    CountdownView? Countdown = null,
    FocusMode Focus = FocusMode.Stopwatch)
{
    public static readonly TimerViewState Empty =
        new(null, "00:00:00", false, "▶", "", false, false, false, Array.Empty<LapInfo>());

    public bool HasApp => !string.IsNullOrEmpty(AppName);

    public string AppDisplay => HasApp ? AppName! : "Any app";

    public string GlyphClock => $"{Glyph} {Clock}";

    public bool HasCountdown => Countdown is not null;
}
