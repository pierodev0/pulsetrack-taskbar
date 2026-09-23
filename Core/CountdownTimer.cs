namespace PulseTrack.Taskbar;

public record CountdownState(double RemainingSeconds, double TotalSeconds, bool Running, bool Finished, bool Started = false);

public sealed class CountdownTimer : IDisposable
{
    public const int TickMs = 500;

    private readonly ITickScheduler _scheduler;
    private bool _disposed;
    private bool _expiredRaised;

    public double TotalSeconds { get; private set; }
    public double RemainingSeconds { get; private set; }
    public bool Running { get; private set; }
    public bool Started { get; private set; }
    public bool HasValue => TotalSeconds > 0;
    public bool Finished => HasValue && RemainingSeconds <= 0;

    public event Action<CountdownState>? Ticked;
    public event Action? Expired;

    public CountdownTimer(ITickScheduler scheduler)
    {
        _scheduler = scheduler;
        _scheduler.Start(OnTick);
        _scheduler.Stop();
    }

    private CountdownState CurrentState() => new(RemainingSeconds, TotalSeconds, Running, Finished, Started);

    private void Emit() => Ticked?.Invoke(CurrentState());

    internal void OnTick()
    {
        if (!Running) return;

        RemainingSeconds -= TickMs / 1000.0;

        if (RemainingSeconds <= 0)
        {
            RemainingSeconds = 0;
            Running = false;
            _scheduler.Stop();
            Emit();
            if (!_expiredRaised)
            {
                _expiredRaised = true;
                Expired?.Invoke();
            }
            return;
        }

        Emit();
    }

    public void Set(double totalSeconds)
    {
        _scheduler.Stop();
        Running = false;
        Started = false;
        TotalSeconds = Math.Max(0, totalSeconds);
        RemainingSeconds = TotalSeconds;
        _expiredRaised = false;
        Emit();
    }

    public void Start()
    {
        if (!HasValue || Finished || Running) return;
        Running = true;
        Started = true;
        _scheduler.Stop();
        _scheduler.Start(OnTick);
        Emit();
    }

    public void Pause()
    {
        if (!Running) return;
        Running = false;
        _scheduler.Stop();
        Emit();
    }

    public void Toggle()
    {
        if (Running) Pause();
        else Start();
    }

    public void Cancel() => Set(0);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scheduler.Dispose();
        GC.SuppressFinalize(this);
    }
}
