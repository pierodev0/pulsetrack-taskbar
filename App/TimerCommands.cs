namespace PulseTrack.Taskbar;

public sealed class TimerCommands
{
    private readonly ForegroundTimer _timer;
    private readonly SessionCoordinator _coordinator;
    private readonly IConfigStore _configStore;
    private readonly Func<string?, Task<AppSelection>> _pickApp;
    private readonly IAppLogger _logger;

    public TimerCommands(
        ForegroundTimer timer,
        SessionCoordinator coordinator,
        IConfigStore configStore,
        Func<string?, Task<AppSelection>> pickApp,
        IAppLogger? logger = null)
    {
        _timer = timer;
        _coordinator = coordinator;
        _configStore = configStore;
        _pickApp = pickApp;
        _logger = logger ?? NullLogger.Instance;
    }

    public string? SelectedApp => _timer.SelectedApp;

    public string SuggestedApp => _timer.SelectedApp ?? _configStore.Load().LastApp;

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
}
