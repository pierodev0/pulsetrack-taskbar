namespace PulseTrack.Taskbar;

public sealed record TimerViewState(
    string? AppName,
    string Clock,
    bool Running,
    string Glyph,
    string LapText,
    bool CanPause,
    bool CanLap,
    bool CanStop,
    IReadOnlyList<LapInfo> Laps)
{
    public static readonly TimerViewState Empty =
        new(null, "00:00:00", false, "▶", "", false, false, false, Array.Empty<LapInfo>());

    public bool HasApp => !string.IsNullOrEmpty(AppName);

    public string AppDisplay => HasApp ? AppName! : "Any app";

    public string GlyphClock => $"{Glyph} {Clock}";
}
