namespace PulseTrack.Taskbar;

public sealed class PipViewModel
{
    private readonly ForegroundTimer? _timer;

    public string AppName { get; private set; } = "Choose app";
    public string ElapsedText { get; private set; } = "00:00:00";
    public string LapText { get; private set; } = "";
    public string PauseGlyph { get; private set; } = "▶";
    public bool CanPause { get; private set; }
    public bool CanLap { get; private set; }
    public bool CanStop { get; private set; }
    public Point Position { get; set; } = Point.Empty;
    public bool Visible { get; set; }

    public PipViewModel(ForegroundTimer? timer)
    {
        _timer = timer;
    }

    public void Refresh(TimerTick tick)
    {
        var app = _timer?.SelectedApp;
        AppName = app ?? "Choose app";
        ElapsedText = ClockFormat.Hms(tick.ElapsedSeconds);
        PauseGlyph = tick.Running ? "⏸" : "▶";
        CanPause = tick.Running;
        CanLap = tick.Running;
        CanStop = tick.ElapsedSeconds > 0 || tick.Running;

        var laps = tick.Laps;
        if (app != null && laps.Count > 0)
        {
            var current = laps[^1];
            LapText = $"Lap {current.Number} · {ClockFormat.Hms(current.DurationSeconds)}";
        }
        else
        {
            LapText = "";
        }
    }

    public void SavePosition(IConfigStore store)
    {
        var config = store.Load();
        config.PipX = Position.X;
        config.PipY = Position.Y;
        config.PipVisible = Visible;
        store.Save(config);
    }

    public void LoadPosition(IConfigStore store)
    {
        var config = store.Load();
        Position = new Point(config.PipX, config.PipY);
        Visible = config.PipVisible;
    }
}
