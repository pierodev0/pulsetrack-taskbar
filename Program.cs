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
    private readonly OverlayConfig _config;
    private readonly ForegroundTimer _timer = new();
    private readonly Database _db;
    private readonly System.Windows.Forms.Timer _flushTimer = new() { Interval = 60_000 };

    private ToolStripMenuItem _startPauseItem = default!;
    private ToolStripMenuItem _lapItem = default!;
    private ToolStripMenuItem _stopItem = default!;
    private ToolStripMenuItem _appItem = default!;

    private long? _sessionId;
    private long? _blockId;
    private double _lastLapStart;

    public TimerAppContext()
    {
        _config = OverlayConfig.Load();
        _db = new Database();
        try { _db.CloseStaleActive(); } catch (Exception ex) { OverlayConfig.Log("App", $"Crash recovery: {ex.Message}"); }

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PulseTrack - Detenido",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        _appItem = new ToolStripMenuItem("Select app...") { Enabled = true };
        _appItem.Click += (_, _) => PickApp();
        _trayIcon.ContextMenuStrip.Items.Add(_appItem);

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

        _startPauseItem = new ToolStripMenuItem("▶ Start");
        _startPauseItem.Click += (_, _) => ToggleStartPause();
        _trayIcon.ContextMenuStrip.Items.Add(_startPauseItem);

        _lapItem = new ToolStripMenuItem("🏁 Lap") { Enabled = false };
        _lapItem.Click += (_, _) => DoLap();
        _trayIcon.ContextMenuStrip.Items.Add(_lapItem);

        _stopItem = new ToolStripMenuItem("⏹ Stop") { Enabled = false };
        _stopItem.Click += (_, _) => DoStop();
        _trayIcon.ContextMenuStrip.Items.Add(_stopItem);

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _trayIcon.ContextMenuStrip.Items.Add("Auto-start", null, (_, _) => ToggleAutoStart());
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        });

        _overlay = new TaskbarOverlayForm();
        _overlay.ApplyConfig(_config);
        _overlay.LeftClicked += OnOverlayClicked;
        _overlay.RightClicked += () => _trayIcon.ContextMenuStrip?.Show(Cursor.Position);
        _overlay.Show();

        _timer.Ticked += OnTimerTick;

        _flushTimer.Tick += (_, _) =>
        {
            try
            {
                if (_sessionId.HasValue && _blockId.HasValue)
                {
                    var elapsed = _timer.ElapsedSeconds;
                    _db.UpdateSessionDuration(_sessionId.Value, elapsed);
                    _db.UpdateBlockDuration(_blockId.Value, elapsed - _lastLapStart);
                }
            }
            catch (Exception ex) { OverlayConfig.Log("App", $"Flush: {ex.Message}"); }
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
            if (_overlay.IsDisposed) return;
            if (_overlay.InvokeRequired)
            {
                try { _overlay.BeginInvoke(() => OnTimerTick(tick)); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }
            RefreshUi(tick);
        }
        catch (Exception ex) { OverlayConfig.Log("App", $"OnTimerTick: {ex.Message}"); }
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
            _overlay.SetTimer($"⏱ {clock}");
            _trayIcon.Text = $"PulseTrack - {clock} · {app}";
            _startPauseItem.Text = "⏸ Pause";
        }
        else if (tick.ElapsedSeconds > 0)
        {
            _overlay.SetTimer($"⏸ {clock}");
            _trayIcon.Text = $"PulseTrack - Paused {clock} · {app}";
            _startPauseItem.Text = "▶ Resume";
        }
        else
        {
            _overlay.SetTimer($"⏱ 00:00:00");
            _trayIcon.Text = $"PulseTrack - Ready · {app}";
            _startPauseItem.Text = "▶ Start";
        }

        _startPauseItem.Enabled = true;
        _lapItem.Enabled = tick.Running;
        _stopItem.Enabled = tick.ElapsedSeconds > 0 || tick.Running;
    }

    private void PickApp()
    {
        try
        {
            using var form = new AppPickerForm(_timer.SelectedApp ?? _config.LastApp);
            if (form.ShowDialog() == DialogResult.OK && form.SelectedApp != null)
            {
                if (_timer.Running || _timer.ElapsedSeconds > 0)
                    DoStop();
                _timer.Start(form.SelectedApp);
                var now = DateTime.UtcNow.ToString("o");
                _sessionId = _db.CreateSession(form.SelectedApp, now);
                _blockId = _db.CreateBlock(_sessionId.Value, form.SelectedApp, "Lap 1", now);
                _lastLapStart = 0;
                _timer.Pause();
                _config.LastApp = form.SelectedApp;
                _config.Save();
                RefreshUi(new TimerTick(0, false, 0, Array.Empty<LapInfo>()));
            }
        }
        catch (Exception ex) { OverlayConfig.Log("App", $"PickApp: {ex.Message}"); }
    }

    private void ToggleStartPause()
    {
        try
        {
            if (_timer.SelectedApp == null)
            {
                PickApp();
                return;
            }
            if (_timer.Running)
                _timer.Pause();
            else
                _timer.Resume();
        }
        catch (Exception ex) { OverlayConfig.Log("App", $"ToggleStartPause: {ex.Message}"); }
    }

    private void DoLap()
    {
        try
        {
            if (!_timer.Running || !_sessionId.HasValue || !_blockId.HasValue) return;
            var now = DateTime.UtcNow.ToString("o");
            var elapsed = _timer.ElapsedSeconds;
            _db.CloseBlock(_blockId.Value, now, elapsed - _lastLapStart);
            _timer.Lap();
            var lapCount = _timer.Laps.Count;
            _blockId = _db.CreateBlock(_sessionId.Value, _timer.SelectedApp!, $"Lap {lapCount}", now);
            _lastLapStart = elapsed;
            _db.UpdateSessionDuration(_sessionId.Value, elapsed);
        }
        catch (Exception ex) { OverlayConfig.Log("App", $"DoLap: {ex.Message}"); }
    }

    private void DoStop()
    {
        try
        {
            if (_timer.SelectedApp == null) return;
            var now = DateTime.UtcNow.ToString("o");
            var elapsed = _timer.ElapsedSeconds;
            if (_sessionId.HasValue && _blockId.HasValue)
            {
                _db.CloseBlock(_blockId.Value, now, elapsed - _lastLapStart);
                _db.CloseSession(_sessionId.Value, now, elapsed);
            }
            _timer.Stop();
            _sessionId = null;
            _blockId = null;
            _lastLapStart = 0;
            _overlay.SetTimer("Choose app");
            _trayIcon.Text = "PulseTrack - Detenido";
        }
        catch (Exception ex) { OverlayConfig.Log("App", $"DoStop: {ex.Message}"); }
    }

    private void OnOverlayClicked()
    {
        try { ToggleStartPause(); }
        catch (Exception ex) { OverlayConfig.Log("App", $"OverlayClicked: {ex.Message}"); }
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
        catch (Exception ex) { OverlayConfig.Log("App", $"ToggleAutoStart: {ex.Message}"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { if (_sessionId.HasValue && _blockId.HasValue) DoStop(); } catch { }
            _flushTimer.Dispose();
            _timer.Dispose();
            _db.Dispose();
            _config.Save();
            _overlay?.Dispose();
            _trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
