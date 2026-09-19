namespace PulseTrack.Taskbar;

public record LapInfo(int Number, string Label, double DurationSeconds);

public record TimerTick(double ElapsedSeconds, bool Running, int LapCount, IReadOnlyList<LapInfo> Laps);

public interface IForegroundSource
{
    string? GetForegroundProcessName();
}

public interface ITickScheduler : IDisposable
{
    void Start(Action tick);
    void Stop();
}

public sealed class SystemForegroundSource : IForegroundSource
{
    public string? GetForegroundProcessName() =>
        WindowWatcher.GetForegroundApp()?.ProcessName;
}

public sealed class FormsTickScheduler : ITickScheduler
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly IAppLogger _logger;
    private EventHandler? _handler;

    public FormsTickScheduler(int intervalMs, IAppLogger? logger = null)
    {
        _timer = new System.Windows.Forms.Timer { Interval = intervalMs };
        _logger = logger ?? NullLogger.Instance;
    }

    public void Start(Action tick)
    {
        Stop();
        _handler = (_, _) => { try { tick(); } catch (Exception ex) { _logger.Log("Timer", $"Tick: {ex.Message}"); } };
        _timer.Tick += _handler;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_handler != null)
        {
            _timer.Tick -= _handler;
            _handler = null;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }

    internal void FireForTest()
    {
        _handler?.Invoke(_timer, EventArgs.Empty);
    }
}

public class ForegroundTimer : IDisposable
{
    public const int PollIntervalMs = 500;

    private readonly IForegroundSource _foreground;
    private readonly ITickScheduler _scheduler;

    public string? SelectedApp { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public bool Running { get; private set; }
    public IReadOnlyList<LapInfo> Laps => _laps.AsReadOnly();

    private readonly List<LapInfo> _laps = new();
    private bool _disposed;

    public event Action<TimerTick>? Ticked;

    public ForegroundTimer()
        : this(new SystemForegroundSource(), new FormsTickScheduler(PollIntervalMs))
    {
    }

    public ForegroundTimer(IForegroundSource foreground, ITickScheduler scheduler)
    {
        _foreground = foreground;
        _scheduler = scheduler;
        _scheduler.Start(OnTick);
        _scheduler.Stop();
    }

    private TimerTick CurrentTick() =>
        new(ElapsedSeconds, Running, _laps.Count > 0 ? _laps.Count - 1 : 0,
            _laps.Select((l, i) => l with { Number = i + 1 }).ToList());

    private void Emit() => Ticked?.Invoke(CurrentTick());

    internal void OnTick()
    {
        if (!Running) return;

        if (SelectedApp != null)
        {
            var active = _foreground.GetForegroundProcessName();
            if (!string.Equals(active, SelectedApp, StringComparison.OrdinalIgnoreCase))
                return;
        }

        ElapsedSeconds += PollIntervalMs / 1000.0;

        if (_laps.Count > 0)
        {
            var prevSum = _laps.Take(_laps.Count - 1).Sum(l => l.DurationSeconds);
            _laps[^1] = _laps[^1] with { DurationSeconds = ElapsedSeconds - prevSum };
        }

        Emit();
    }

    public void Start(string? appName)
    {
        SelectedApp = appName;
        ElapsedSeconds = 0;
        Running = true;
        _laps.Clear();
        _laps.Add(new LapInfo(1, "Lap 1", 0));

        _scheduler.Stop();
        _scheduler.Start(OnTick);
        Emit();
    }

    public double Stop()
    {
        Running = false;
        _scheduler.Stop();
        var duration = ElapsedSeconds;
        ElapsedSeconds = 0;
        _laps.Clear();
        Emit();
        return duration;
    }

    public void Lap()
    {
        if (!Running || _laps.Count == 0) return;
        _laps.Add(new LapInfo(_laps.Count + 1, $"Lap {_laps.Count + 1}", 0));
        Emit();
    }

    public void RenameLap(int index, string newLabel)
    {
        if (index >= 0 && index < _laps.Count)
        {
            _laps[index] = _laps[index] with { Label = newLabel };
            Emit();
        }
    }

    public void Pause()
    {
        Running = false;
        _scheduler.Stop();
        Emit();
    }

    public void Resume()
    {
        Running = true;
        _scheduler.Stop();
        _scheduler.Start(OnTick);
        Emit();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scheduler.Dispose();
        GC.SuppressFinalize(this);
    }
}
