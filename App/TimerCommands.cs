namespace PulseTrack.Taskbar;

public sealed class TimerCommands
{
    private readonly ForegroundTimer _timer;
    private readonly SessionCoordinator _coordinator;
    private readonly IConfigStore _configStore;
    private readonly Func<string?, Task<string?>> _pickApp;
    private readonly IAppLogger _logger;

    public TimerCommands(
        ForegroundTimer timer,
        SessionCoordinator coordinator,
        IConfigStore configStore,
        Func<string?, Task<string?>> pickApp,
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
            var chosen = await _pickApp(SuggestedApp).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(chosen))
                await StartWithAsync(chosen!).ConfigureAwait(true);
        }
        catch (Exception ex) { _logger.Log("App", $"PickApp: {ex.Message}"); }
    }

    public async Task ToggleStartPauseAsync()
    {
        try
        {
            if (_coordinator.ToggleStartPause()) return;
            await PickAppAsync().ConfigureAwait(true);
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
        try
        {
            if (_timer.SelectedApp == null) return;
            await _coordinator.StopAsync().ConfigureAwait(true);
        }
        catch (Exception ex) { _logger.Log("App", $"Stop: {ex.Message}"); }
    }

    private async Task StartWithAsync(string appName)
    {
        await _coordinator.PickAppAsync(appName).ConfigureAwait(true);
        _configStore.Update(c => c.LastApp = appName);
    }
}
