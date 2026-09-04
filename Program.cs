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
        using var app = new TimerAppContext();
        Application.Run();
    }
}

public class TimerAppContext : ApplicationContext
{
    private readonly TaskbarOverlayForm _overlay;
    private readonly NotifyIcon _trayIcon;
    private readonly IConfigStore _configStore;
    private readonly IAppLogger _logger;
    private readonly OverlayConfig _config;
    private readonly ForegroundTimer _timer;
    private readonly Database _db;
    private readonly ChannelSessionStore _store;
    private readonly SessionCoordinator _coordinator;
    private readonly System.Windows.Forms.Timer _flushTimer = new() { Interval = 60_000 };

    private ToolStripMenuItem _startPauseItem = default!;
    private ToolStripMenuItem _lapItem = default!;
    private ToolStripMenuItem _stopItem = default!;
    private ToolStripMenuItem _appItem = default!;
    private ToolStripMenuItem _pipItem = default!;
    private PipForm? _pip;
    private PipViewModel? _pipVm;

    private long? _sessionId => _coordinator.SessionId;
    private long? _blockId => _coordinator.BlockId;

    public TimerAppContext()
        : this(FileLogger.Default, new FileConfigStore(), null, null)
    {
    }

    internal TimerAppContext(IAppLogger logger, IConfigStore configStore, ForegroundTimer? timer, ISessionStore? store)
    {
        _logger = logger;
        _configStore = configStore;
        _config = _configStore.Load();
        _db = new Database();
        _timer = timer ?? new ForegroundTimer(new SystemForegroundSource(), new FormsTickScheduler(ForegroundTimer.PollIntervalMs, _logger));
        _store = store as ChannelSessionStore ?? new ChannelSessionStore(_db);
        _coordinator = new SessionCoordinator(_timer, _store, SystemClock.Instance);
        try { _store.CloseStaleActiveAsync().GetAwaiter().GetResult(); } catch (Exception ex) { _logger.Log("App", $"Crash recovery: {ex.Message}"); }

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PulseTrack - Detenido",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        _appItem = new ToolStripMenuItem("Select app...") { Enabled = true };
        _appItem.Click += async (_, _) => await PickAppAsync();
        _trayIcon.ContextMenuStrip.Items.Add(_appItem);

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

        _startPauseItem = new ToolStripMenuItem("▶ Start");
        _startPauseItem.Click += (_, _) => ToggleStartPause();
        _trayIcon.ContextMenuStrip.Items.Add(_startPauseItem);

        _lapItem = new ToolStripMenuItem("🏁 Lap") { Enabled = false };
        _lapItem.Click += async (_, _) => await DoLapAsync();
        _trayIcon.ContextMenuStrip.Items.Add(_lapItem);

        _stopItem = new ToolStripMenuItem("⏹ Stop") { Enabled = false };
        _stopItem.Click += async (_, _) => await DoStopAsync();
        _trayIcon.ContextMenuStrip.Items.Add(_stopItem);

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _pipItem = new ToolStripMenuItem("Picture in Picture");
        _pipItem.Click += (_, _) => TogglePip();
        _trayIcon.ContextMenuStrip.Items.Add(_pipItem);
        _trayIcon.ContextMenuStrip.Items.Add("Settings...", null, (_, _) => OpenSettings());
        _trayIcon.ContextMenuStrip.Items.Add("Auto-start", null, (_, _) => ToggleAutoStart());
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        });

        _overlay = new TaskbarOverlayForm(_logger);
        _overlay.ApplyConfig(_config);
        _overlay.LeftClicked += OnOverlayClicked;
        _overlay.RightClicked += () => _trayIcon.ContextMenuStrip?.Show(Cursor.Position);
        _overlay.Show();

        _timer.Ticked += OnTimerTick;

        _flushTimer.Tick += async (_, _) =>
        {
            try { await _coordinator.FlushAsync().ConfigureAwait(false); }
            catch (Exception ex) { _logger.Log("App", $"Flush: {ex.Message}"); }
        };
        _flushTimer.Start();

        RefreshUi(new TimerTick(0, false, 0, Array.Empty<LapInfo>()));
    }

    private static string FormatHms(double seconds)
    {
        var h = (int)(seconds / 3600);
        var m = (int)((seconds % 3600) / 60);
        var s = (int)(seconds % 60);
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    private void OnTimerTick(TimerTick tick)
    {
        try
        {
            if (_overlay.InvokeRequired)
            {
                try { _overlay.BeginInvoke(() => OnTimerTick(tick)); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }
            if (!_overlay.IsDisposed)
                RefreshUi(tick);
            _pip?.UpdateUi(tick);
        }
        catch (Exception ex) { _logger.Log("App", $"OnTimerTick: {ex.Message}"); }
    }

    private void RefreshUi(TimerTick tick)
    {
        var app = _timer.SelectedApp;
        if (app == null)
        {
            _overlay.SetTimer("Choose app");
            _trayIcon.Text = "PulseTrack - Detenido";
            _appItem.Text = "Select app...";
            _startPauseItem.Text = "▶ Start";
            _startPauseItem.Enabled = false;
            _lapItem.Enabled = false;
            _stopItem.Enabled = false;
            return;
        }

        _appItem.Text = $"App: {app}";
        var clock = FormatHms(tick.ElapsedSeconds);

        if (tick.Running)
        {
            _overlay.SetTimer($"⏸ {clock}");
            _trayIcon.Text = $"PulseTrack - {clock} · {app}";
            _startPauseItem.Text = "⏸ Pause";
        }
        else if (tick.ElapsedSeconds > 0)
        {
            _overlay.SetTimer($"▶ {clock}");
            _trayIcon.Text = $"PulseTrack - Paused {clock} · {app}";
            _startPauseItem.Text = "▶ Resume";
        }
        else
        {
            _overlay.SetTimer($"▶ 00:00:00");
            _trayIcon.Text = $"PulseTrack - Ready · {app}";
            _startPauseItem.Text = "▶ Start";
        }

        _startPauseItem.Enabled = true;
        _lapItem.Enabled = tick.Running;
        _stopItem.Enabled = tick.ElapsedSeconds > 0 || tick.Running;
    }

    private async Task PickAppAsync()
    {
        try
        {
            using var form = new AppPickerForm(_timer.SelectedApp ?? _config.LastApp);
            if (form.ShowDialog() == DialogResult.OK && form.SelectedApp != null)
            {
                await _coordinator.PickAppAsync(form.SelectedApp).ConfigureAwait(true);
                _config.LastApp = form.SelectedApp;
                _configStore.Save(_config);
                RefreshUi(new TimerTick(0, true, 0, Array.Empty<LapInfo>()));
            }
        }
        catch (Exception ex) { _logger.Log("App", $"PickApp: {ex.Message}"); }
    }

    private void ToggleStartPause()
    {
        try
        {
            if (!_coordinator.ToggleStartPause())
                _ = PickAppAsync();
        }
        catch (Exception ex) { _logger.Log("App", $"ToggleStartPause: {ex.Message}"); }
    }

    private async Task DoLapAsync()
    {
        try { await _coordinator.LapAsync().ConfigureAwait(true); }
        catch (Exception ex) { _logger.Log("App", $"DoLap: {ex.Message}"); }
    }

    private async Task DoStopAsync()
    {
        try
        {
            if (_timer.SelectedApp == null) return;
            await _coordinator.StopAsync().ConfigureAwait(true);
            _overlay.SetTimer("Choose app");
            _trayIcon.Text = "PulseTrack - Detenido";
        }
        catch (Exception ex) { _logger.Log("App", $"DoStop: {ex.Message}"); }
    }

    private void OnOverlayClicked()
    {
        try { ToggleStartPause(); }
        catch (Exception ex) { _logger.Log("App", $"OverlayClicked: {ex.Message}"); }
    }

    private void TogglePip()
    {
        try
        {
            if (_pip != null && !_pip.IsDisposed)
            {
                ClosePip();
                return;
            }
            _pipVm = new PipViewModel(_timer);
            _pipVm.LoadPosition(_configStore);
            _pip = new PipForm(_pipVm, _logger, ToggleStartPause, DoLapAsync, DoStopAsync, ClosePip);
            if (_pipVm.Position != Point.Empty)
                _pip.Location = _pipVm.Position;
            else
            {
                var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
                _pip.Location = new Point(area.Right - _pip.Width - 24, area.Bottom - _pip.Height - 24);
            }
            _pip.FormClosed += (_, _) => SavePipPosition();
            _pip.Show();
            _pipItem.Checked = true;
            _pipVm.Visible = true;
            _pipVm.Position = _pip.Location;
            _pipVm.SavePosition(_configStore);
            _pip.UpdateUi(new TimerTick(_timer.ElapsedSeconds, _timer.Running, 0, _timer.Laps));
        }
        catch (Exception ex) { _logger.Log("App", $"Pip: {ex.Message}"); }
    }

    private void ClosePip()
    {
        try
        {
            SavePipPosition();
            _pip?.Close();
            _pip?.Dispose();
        }
        catch { }
        _pip = null;
        _pipItem.Checked = false;
    }

    private void SavePipPosition()
    {
        try
        {
            if (_pipVm != null && _pip != null && !_pip.IsDisposed)
            {
                _pipVm.Position = _pip.Location;
                _pipVm.Visible = _pip.Visible;
                _pipVm.SavePosition(_configStore);
            }
            else if (_pipVm != null)
            {
                _pipVm.Visible = false;
                _pipVm.SavePosition(_configStore);
            }
        }
        catch (Exception ex) { _logger.Log("App", $"PipSave: {ex.Message}"); }
    }

    private void OpenSettings()
    {
        try
        {
            var vm = new SettingsViewModel(_configStore);
            using var form = new SettingsForm(vm);
            if (form.ShowDialog() == DialogResult.OK)
            {
                var updated = vm.Apply();
                _config.FontFamily = updated.FontFamily;
                _config.FontSize = updated.FontSize;
                _config.FontStyle = updated.FontStyle;
                _config.TextColorArgb = updated.TextColorArgb;
                _config.TextAlpha = updated.TextAlpha;
                _config.ShowBackground = updated.ShowBackground;
                _config.BackgroundColorArgb = updated.BackgroundColorArgb;
                _overlay.ApplyConfig(_config);
            }
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
                _trayIcon.ShowBalloonTip(2000, "PulseTrack", "Auto-start disabled", ToolTipIcon.Info);
            }
            else
            {
                key.SetValue("PulseTrackTaskbar", appPath);
                _trayIcon.ShowBalloonTip(2000, "PulseTrack", "Auto-start enabled", ToolTipIcon.Info);
            }
        }
        catch (Exception ex) { _logger.Log("App", $"ToggleAutoStart: {ex.Message}"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { if (_coordinator.SessionId.HasValue) _coordinator.StopAsync().GetAwaiter().GetResult(); } catch { }
            try { SavePipPosition(); } catch { }
            _flushTimer.Dispose();
            _timer.Dispose();
            _store.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _configStore.Save(_config);
            try { _pip?.Dispose(); } catch { }
            _overlay?.Dispose();
            _trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
