namespace PulseTrack.Taskbar;

public sealed class TrayIconPresenter : IDisposable
{
    private const int MaxTooltipLength = 63;

    private readonly TimerCommands _commands;
    private readonly Func<AppMode> _currentMode;
    private readonly Action<AppMode> _setMode;
    private readonly Action _showActiveSurface;
    private readonly Action _openSettings;
    private readonly Action _toggleAutoStart;
    private readonly Action _openCustomTimer;
    private readonly IAppLogger _logger;

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _appItem;
    private readonly ToolStripMenuItem _startPauseItem;
    private readonly ToolStripMenuItem _lapItem;
    private readonly ToolStripMenuItem _stopItem;
    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _countdownPauseItem;
    private readonly ToolStripMenuItem _countdownCancelItem;
    private readonly Dictionary<AppMode, ToolStripMenuItem> _modeItems = new();
    private bool _disposed;

    public TrayIconPresenter(
        TimerCommands commands,
        Func<AppMode> currentMode,
        Action<AppMode> setMode,
        Action showActiveSurface,
        Action openSettings,
        Action toggleAutoStart,
        Action openCustomTimer,
        IAppLogger? logger = null)
    {
        _commands = commands;
        _currentMode = currentMode;
        _setMode = setMode;
        _showActiveSurface = showActiveSurface;
        _openSettings = openSettings;
        _toggleAutoStart = toggleAutoStart;
        _openCustomTimer = openCustomTimer;
        _logger = logger ?? NullLogger.Instance;

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PulseTrack",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        var menu = _icon.ContextMenuStrip;

        _appItem = new ToolStripMenuItem("Select app...");
        _appItem.Click += (_, _) => _ = _commands.PickAppAsync();
        menu.Items.Add(_appItem);

        menu.Items.Add(new ToolStripSeparator());

        _startPauseItem = new ToolStripMenuItem("▶ Start");
        _startPauseItem.Click += (_, _) => _ = _commands.ToggleStartPauseAsync();
        menu.Items.Add(_startPauseItem);

        _lapItem = new ToolStripMenuItem("🏁 Lap") { Enabled = false };
        _lapItem.Click += (_, _) => _ = _commands.LapAsync();
        menu.Items.Add(_lapItem);

        _stopItem = new ToolStripMenuItem("⏹ Stop") { Enabled = false };
        _stopItem.Click += (_, _) => _ = _commands.StopAsync();
        menu.Items.Add(_stopItem);

        menu.Items.Add(BuildTimerMenu(out _countdownPauseItem, out _countdownCancelItem));

        menu.Items.Add(new ToolStripSeparator());

        _showItem = new ToolStripMenuItem("Show window");
        _showItem.Click += (_, _) => { try { _showActiveSurface(); } catch (Exception ex) { Log("Show", ex); } };
        menu.Items.Add(_showItem);

        menu.Items.Add(BuildModeMenu());

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Settings...", null, (_, _) => { try { _openSettings(); } catch (Exception ex) { Log("Settings", ex); } });
        menu.Items.Add("Auto-start", null, (_, _) => { try { _toggleAutoStart(); } catch (Exception ex) { Log("AutoStart", ex); } });
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _icon.Visible = false;
            Application.Exit();
        });

        _icon.DoubleClick += (_, _) => { try { _showActiveSurface(); } catch (Exception ex) { Log("Show", ex); } };

        SyncModeChecks();
    }

    private ToolStripMenuItem BuildModeMenu()
    {
        var root = new ToolStripMenuItem("Mode");
        AddModeItem(root, AppMode.Normal, "Normal window");
        AddModeItem(root, AppMode.Taskbar, "Taskbar overlay");
        AddModeItem(root, AppMode.Pip, "Picture in Picture");
        return root;
    }

    private ToolStripMenuItem BuildTimerMenu(out ToolStripMenuItem pauseItem, out ToolStripMenuItem cancelItem)
    {
        var root = new ToolStripMenuItem("Timer");

        foreach (var (label, seconds) in new[] { ("5 minutes", 300), ("10 minutes", 600), ("25 minutes", 1500), ("1 hour", 3600) })
        {
            var preset = seconds;
            root.DropDownItems.Add(label, null, (_, _) =>
            {
                try { _commands.StageCountdown(preset); }
                catch (Exception ex) { Log("Timer", ex); }
            });
        }

        root.DropDownItems.Add(new ToolStripSeparator());

        root.DropDownItems.Add("Custom...", null, (_, _) =>
        {
            try { _openCustomTimer(); }
            catch (Exception ex) { Log("Timer", ex); }
        });

        pauseItem = new ToolStripMenuItem("▶ Resume") { Enabled = false };
        pauseItem.Click += (_, _) =>
        {
            try { _commands.ToggleCountdown(); }
            catch (Exception ex) { Log("Timer", ex); }
        };
        root.DropDownItems.Add(pauseItem);

        cancelItem = new ToolStripMenuItem("✕ Cancel") { Enabled = false };
        cancelItem.Click += (_, _) =>
        {
            try { _commands.CancelCountdown(); }
            catch (Exception ex) { Log("Timer", ex); }
        };
        root.DropDownItems.Add(cancelItem);

        return root;
    }

    private void AddModeItem(ToolStripMenuItem parent, AppMode mode, string text)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) =>
        {
            try { _setMode(mode); }
            catch (Exception ex) { Log("Mode", ex); }
        };
        _modeItems[mode] = item;
        parent.DropDownItems.Add(item);
    }

    public void Update(TimerViewState state)
    {
        if (_disposed) return;

        _appItem.Text = state.HasApp ? $"App: {state.AppName}" : "Select app...";
        _startPauseItem.Text = state.Running ? "⏸ Pause" : state.CanStop ? "▶ Resume" : "▶ Start";
        _startPauseItem.Enabled = true;
        _lapItem.Enabled = state.CanLap;
        _stopItem.Enabled = state.CanStop;

        var countdown = state.Countdown;
        _countdownCancelItem.Enabled = countdown is not null;
        _countdownPauseItem.Enabled = countdown is { Finished: false };
        _countdownPauseItem.Text = countdown is { Running: true }
            ? "⏸ Pause"
            : countdown is { Started: true } ? "▶ Resume" : "▶ Start";

        _icon.Text = BuildTooltip(state);
        SyncModeChecks();
    }

    private static string BuildTooltip(TimerViewState state)
    {
        var status = state.Running ? state.Clock : state.CanStop ? $"Paused {state.Clock}" : "Ready";
        var countdown = state.Countdown is null ? "" : $" · {state.Countdown.Label}";
        var text = state.HasApp ? $"PulseTrack - {status}{countdown} · {state.AppName}" : $"PulseTrack - {status}{countdown}";
        return text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }

    private void SyncModeChecks()
    {
        var mode = _currentMode();
        foreach (var (key, item) in _modeItems)
            item.Checked = key == mode;

        _showItem.Enabled = mode == AppMode.Normal;
    }

    public void ShowMenu()
    {
        if (_disposed) return;
        try { _icon.ContextMenuStrip?.Show(Cursor.Position); } catch (Exception ex) { Log("Menu", ex); }
    }

    public void Notify(string message)
    {
        if (_disposed) return;
        try { _icon.ShowBalloonTip(2000, "PulseTrack", message, ToolTipIcon.Info); } catch (Exception ex) { Log("Notify", ex); }
    }

    private void Log(string scope, Exception ex) => _logger.Log("Tray", $"{scope}: {ex.Message}");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
        GC.SuppressFinalize(this);
    }
}
