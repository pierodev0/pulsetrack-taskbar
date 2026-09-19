namespace PulseTrack.Taskbar;

public sealed class SurfaceHost : IDisposable
{
    private readonly IReadOnlyList<ITimerSurface> _surfaces;
    private readonly IAppLogger _logger;
    private TimerViewState _last = TimerViewState.Empty;
    private bool _disposed;

    public AppMode Mode { get; private set; }

    public event Action<AppMode>? ModeChanged;

    public SurfaceHost(IEnumerable<ITimerSurface> surfaces, AppMode initial, IAppLogger? logger = null)
    {
        _surfaces = surfaces.ToList();
        _logger = logger ?? NullLogger.Instance;
        Mode = initial;
        ApplyVisibility();
        Publish();
    }

    public ITimerSurface? Active => _surfaces.FirstOrDefault(s => s.Mode == Mode);

    public TimerViewState LastState => _last;

    public void SetMode(AppMode mode)
    {
        if (_disposed || mode == Mode) return;
        Mode = mode;
        ApplyVisibility();
        Publish();
        ModeChanged?.Invoke(mode);
    }

    public void Render(TimerViewState state)
    {
        _last = state;
        Publish();
    }

    public void ShowActive()
    {
        if (_disposed) return;
        try { Active?.SetVisible(true); }
        catch (Exception ex) { _logger.Log("Host", $"ShowActive: {ex.Message}"); }
    }

    private void Publish()
    {
        try { Active?.Render(_last); }
        catch (Exception ex) { _logger.Log("Host", $"Render: {ex.Message}"); }
    }

    private void ApplyVisibility()
    {
        foreach (var surface in _surfaces)
        {
            try { surface.SetVisible(surface.Mode == Mode); }
            catch (Exception ex) { _logger.Log("Host", $"SetVisible {surface.Mode}: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var surface in _surfaces)
        {
            try { surface.Dispose(); } catch { }
        }
        GC.SuppressFinalize(this);
    }
}
