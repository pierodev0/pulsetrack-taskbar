namespace PulseTrack.Taskbar;

public sealed class TimerCommands
{
    private readonly ForegroundTimer _timer;
    private readonly SessionCoordinator _coordinator;
    private readonly CountdownTimer _countdown;
    private readonly IConfigStore _configStore;
    private readonly Func<string?, Task<AppSelection>> _pickApp;
    private readonly IAppLogger _logger;

    public TimerCommands(
        ForegroundTimer timer,
        SessionCoordinator coordinator,
        CountdownTimer countdown,
        IConfigStore configStore,
        Func<string?, Task<AppSelection>> pickApp,
        IAppLogger? logger = null)
    {
        _timer = timer;
        _coordinator = coordinator;
        _countdown = countdown;
        _configStore = configStore;
        _pickApp = pickApp;
        _logger = logger ?? NullLogger.Instance;
        Focus = configStore.Load().Focus;
    }

    public string? SelectedApp => _timer.SelectedApp;

    public string SuggestedApp => _timer.SelectedApp ?? _configStore.Load().LastApp;

    public FocusMode Focus { get; private set; }

    public event Action<FocusMode>? FocusChanged;

    public void SetFocus(FocusMode focus)
    {
        if (focus == Focus) return;
        Focus = focus;
        try { _configStore.Update(c => c.Focus = focus); }
        catch (Exception ex) { _logger.Log("App", $"SetFocus: {ex.Message}"); }
        FocusChanged?.Invoke(focus);
    }

    public async Task PickAppAsync()
    {
        try
        {
            var selection = await _pickApp(SuggestedApp).ConfigureAwait(true);
            if (!selection.Confirmed) return;

            await _coordinator.StartAsync(selection.AppName).ConfigureAwait(true);
            _configStore.Update(c => c.LastApp = selection.AppName ?? "");
        }
        catch (Exception ex) { _logger.Log("App", $"PickApp: {ex.Message}"); }
    }

    public async Task ToggleStartPauseAsync()
    {
        try
        {
            if (_coordinator.ToggleStartPause()) return;
            await _coordinator.StartAsync(_timer.SelectedApp).ConfigureAwait(true);
        }
        catch (Exception ex) { _logger.Log("App", $"ToggleStartPause: {ex.Message}"); }
    }

    public async Task LapAsync()
    {
        try { await _coordinator.LapAsync().ConfigureAwait(true); }
        catch (Exception ex) { _logger.Log("App", $"Lap: {ex.Message}"); }
    }

    public async Task StopAsync()
    {
        try { await _coordinator.StopAsync().ConfigureAwait(true); }
        catch (Exception ex) { _logger.Log("App", $"Stop: {ex.Message}"); }
    }

    public void StartCountdown(double seconds)
    {
        try
        {
            _countdown.Set(seconds);
            _countdown.Start();
            _configStore.Update(c => c.LastCountdownSeconds = (int)Math.Round(seconds));
        }
        catch (Exception ex) { _logger.Log("App", $"StartCountdown: {ex.Message}"); }
    }

    public void StageCountdown(double seconds)
    {
        try
        {
            if (_countdown.Running) return;
            _countdown.Set(seconds);
            _configStore.Update(c => c.LastCountdownSeconds = (int)Math.Round(seconds));
        }
        catch (Exception ex) { _logger.Log("App", $"StageCountdown: {ex.Message}"); }
    }

    public void ToggleCountdown()
    {
        try { _countdown.Toggle(); }
        catch (Exception ex) { _logger.Log("App", $"ToggleCountdown: {ex.Message}"); }
    }

    public void CancelCountdown()
    {
        try { _countdown.Cancel(); }
        catch (Exception ex) { _logger.Log("App", $"CancelCountdown: {ex.Message}"); }
    }

    public async Task TogglePrimaryAsync()
    {
        if (Focus == FocusMode.Timer)
        {
            ToggleCountdown();
            return;
        }
        await ToggleStartPauseAsync().ConfigureAwait(true);
    }

    public async Task StopPrimaryAsync()
    {
        if (Focus == FocusMode.Timer)
        {
            CancelCountdown();
            return;
        }
        await StopAsync().ConfigureAwait(true);
    }
}
