namespace PulseTrack.Taskbar;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--probe", StringComparer.OrdinalIgnoreCase))
        {
            ProbeCli.Run(Array.Empty<string>());
            return;
        }
        if (args.Contains("--log", StringComparer.OrdinalIgnoreCase))
        {
            var appArg = args.SkipWhile(a => !a.Equals("--log", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
            ProbeCli.Run(appArg != null ? new[] { appArg } : Array.Empty<string>());
            return;
        }
        ApplicationConfiguration.Initialize();
        using var app = new TimerAppContext(AppModeParser.From(args));
        Application.Run();
    }
}

public static class AppModeParser
{
    public static AppMode? From(string[] args)
    {
        var index = Array.FindIndex(args, a => a.Equals("--mode", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) return null;

        return args[index + 1].ToLowerInvariant() switch
        {
            "normal" => AppMode.Normal,
            "taskbar" => AppMode.Taskbar,
            "pip" => AppMode.Pip,
            "picture-in-picture" => AppMode.Pip,
            _ => null,
        };
    }
}

public class TimerAppContext : ApplicationContext
{
    private readonly IConfigStore _configStore;
    private readonly IAppLogger _logger;
    private readonly ForegroundTimer _timer;
    private readonly CountdownTimer _countdown;
    private readonly IAlarm _alarm = new SystemAlarm();
    private readonly Database _db;
    private readonly ChannelSessionStore _store;
    private readonly SessionCoordinator _coordinator;
    private readonly TimerCommands _commands;
    private readonly NormalForm _normal;
    private readonly TaskbarOverlayForm _overlay;
    private readonly PipForm _pip;
    private readonly SurfaceHost _surfaces;
    private readonly TrayIconPresenter _tray;
    private readonly System.Windows.Forms.Timer _flushTimer = new() { Interval = 60_000 };

    private readonly int _uiThreadId;
    private readonly SynchronizationContext _uiContext;

    private TimerTick _lastTick = new(0, false, 0, Array.Empty<LapInfo>());
    private CountdownState _lastCountdown = new(0, 0, false, false);

    public TimerAppContext(AppMode? modeOverride = null)
        : this(modeOverride, FileLogger.Default, new FileConfigStore(), null, null)
    {
    }

    internal TimerAppContext(AppMode? modeOverride, IAppLogger logger, IConfigStore configStore, ForegroundTimer? timer, ISessionStore? store)
    {
        _uiThreadId = Environment.CurrentManagedThreadId;
        _uiContext = new WindowsFormsSynchronizationContext();
        _logger = logger;
        _configStore = configStore;
        _db = new Database();
        _timer = timer ?? new ForegroundTimer(new SystemForegroundSource(), new FormsTickScheduler(ForegroundTimer.PollIntervalMs, _logger));
        _store = store as ChannelSessionStore ?? new ChannelSessionStore(_db);
        _coordinator = new SessionCoordinator(_timer, _store, SystemClock.Instance);
        _countdown = new CountdownTimer(new FormsTickScheduler(CountdownTimer.TickMs, _logger));
        try { _store.CloseStaleActiveAsync().GetAwaiter().GetResult(); } catch (Exception ex) { _logger.Log("App", $"Crash recovery: {ex.Message}"); }

        _commands = new TimerCommands(_timer, _coordinator, _countdown, _configStore, ShowAppPickerAsync, _logger);

        var config = _configStore.Load();

        SurfaceHost? host = null;
        TrayIconPresenter? tray = null;

        _normal = new NormalForm(_commands, _configStore, mode => host?.SetMode(mode), _logger);
        _overlay = new TaskbarOverlayForm(_logger);
        _overlay.ApplyConfig(config);
        _pip = new PipForm(_commands, _configStore, () => host?.SetMode(AppMode.Normal), _logger);

        _overlay.LeftClicked += () => _ = _commands.TogglePrimaryAsync();
        _overlay.RightClicked += () => tray?.ShowMenu();

        _surfaces = new SurfaceHost(new ITimerSurface[] { _normal, _overlay, _pip }, modeOverride ?? config.Mode, _logger);
        host = _surfaces;
        _surfaces.ModeChanged += mode => PersistMode(mode);

        _tray = new TrayIconPresenter(
            _commands,
            () => _surfaces.Mode,
            mode => _surfaces.SetMode(mode),
            () => _surfaces.ShowActive(),
            OpenSettings,
            ToggleAutoStart,
            OpenCustomTimerPicker,
            _logger);
        tray = _tray;

        _timer.Ticked += OnTimerTick;
        _countdown.Ticked += OnCountdownTick;
        _countdown.Expired += OnCountdownExpired;
        _commands.FocusChanged += _ => RenderState();

        _flushTimer.Tick += async (_, _) =>
        {
            try { await _coordinator.FlushAsync().ConfigureAwait(false); }
            catch (Exception ex) { _logger.Log("App", $"Flush: {ex.Message}"); }
        };
        _flushTimer.Start();

        OnTimerTick(new TimerTick(_timer.ElapsedSeconds, _timer.Running, _timer.Laps.Count, _timer.Laps));
    }

    public AppMode Mode => _surfaces.Mode;

    private void PersistMode(AppMode mode)
    {
        try { _configStore.Update(c => c.Mode = mode); }
        catch (Exception ex) { _logger.Log("App", $"SaveMode: {ex.Message}"); }
    }

    private void OnTimerTick(TimerTick tick)
    {
        _lastTick = tick;
        RenderState();
    }

    private void OnCountdownTick(CountdownState countdown)
    {
        _lastCountdown = countdown;
        RenderState();
    }

    private void RenderState()
    {
        try
        {
            if (Environment.CurrentManagedThreadId != _uiThreadId)
            {
                _uiContext.Post(_ => RenderState(), null);
                return;
            }

            var state = TimerViewStateFactory.From(_lastTick, _timer.SelectedApp, _lastCountdown, _commands.Focus);
            _surfaces.Render(state);
            _tray.Update(state);
        }
        catch (Exception ex) { _logger.Log("App", $"RenderState: {ex.Message}"); }
    }

    private void OnCountdownExpired()
    {
        try { _alarm.Play(); }
        catch (Exception ex) { _logger.Log("App", $"Alarm: {ex.Message}"); }

        try
        {
            if (Environment.CurrentManagedThreadId != _uiThreadId)
                _uiContext.Post(_ => _tray.Notify("Time's up!"), null);
            else
                _tray.Notify("Time's up!");
        }
        catch (Exception ex) { _logger.Log("App", $"ExpiryNotify: {ex.Message}"); }
    }

    private void OpenCustomTimerPicker()
    {
        try
        {
            var last = _configStore.Load().LastCountdownSeconds;
            using var form = new TimerPickerForm(last);
            if (form.ShowDialog() == DialogResult.OK)
                _commands.StageCountdown(form.SelectedSeconds);
        }
        catch (Exception ex) { _logger.Log("App", $"TimerPicker: {ex.Message}"); }
    }

    private Task<AppSelection> ShowAppPickerAsync(string? current)
    {
        if (Environment.CurrentManagedThreadId == _uiThreadId)
            return Task.FromResult(RunPicker(current));

        var result = new TaskCompletionSource<AppSelection>(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ => result.SetResult(RunPicker(current)), null);
        return result.Task;
    }

    private AppSelection RunPicker(string? current)
    {
        try
        {
            using var form = new AppPickerForm(current);
            if (form.ShowDialog() != DialogResult.OK)
                return AppSelection.Cancelled;

            return form.SelectedApp == null ? AppSelection.AnyApp : AppSelection.Of(form.SelectedApp);
        }
        catch (Exception ex)
        {
            _logger.Log("App", $"Picker: {ex.Message}");
            return AppSelection.Cancelled;
        }
    }

    private void OpenSettings()
    {
        try
        {
            var vm = new SettingsViewModel(_configStore);
            using var form = new SettingsForm(vm);
            if (form.ShowDialog() == DialogResult.OK)
                _overlay.ApplyConfig(vm.Apply());
        }
        catch (Exception ex) { _logger.Log("App", $"Settings: {ex.Message}"); }
    }

    private void ToggleAutoStart()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
            var existing = key.GetValue("PulseTrackTaskbar") as string;

            if (existing == appPath)
            {
                key.DeleteValue("PulseTrackTaskbar");
                _tray.Notify("Auto-start disabled");
            }
            else
            {
                key.SetValue("PulseTrackTaskbar", appPath);
                _tray.Notify("Auto-start enabled");
            }
        }
        catch (Exception ex) { _logger.Log("App", $"ToggleAutoStart: {ex.Message}"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { if (_coordinator.SessionId.HasValue) _coordinator.StopAsync().GetAwaiter().GetResult(); } catch { }
            _flushTimer.Dispose();
            _timer.Dispose();
            _countdown.Dispose();
            _store.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _normal.AllowClose();
            _surfaces.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
