namespace PulseTrack.Taskbar;

public class NormalForm : Form, ITimerSurface
{
    private static readonly Size _defaultSize = new(376, 340);

    private readonly TimerCommands _commands;
    private readonly IConfigStore _configStore;
    private readonly Action<AppMode> _onModeRequested;
    private readonly IAppLogger _logger;

    private readonly Label _appLabel = new() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _pickBtn = new() { Text = "Select app..." };
    private readonly Label _clockLabel = new() { TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _lapLabel = new() { TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _countdownLabel = new() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _toggleBtn = new();
    private readonly Button _lapBtn = new() { Text = "🏁 Lap" };
    private readonly Button _stopBtn = new() { Text = "⏹ Stop" };
    private readonly ListBox _lapList = new() { IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _viewLabel = new() { Text = "View:", TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _taskbarBtn = new() { Text = "Taskbar" };
    private readonly Button _pipBtn = new() { Text = "PiP" };

    private readonly TabControl _tabs = new();
    private readonly TabPage _stopwatchPage = new() { Text = "Stopwatch" };
    private readonly TabPage _timerPage = new() { Text = "Timer" };
    private readonly Label _cdClockLabel = new() { TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _cdStatusLabel = new() { TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _cdMinLabel = new() { Text = "Minutes", AutoSize = true };
    private readonly Label _cdSecLabel = new() { Text = "Seconds", AutoSize = true };
    private readonly NumericUpDown _cdMinutes = new();
    private readonly NumericUpDown _cdSeconds = new();
    private readonly Button _cdStartBtn = new() { Text = "Start" };
    private readonly Button _cdPreset5 = new() { Text = "5 min" };
    private readonly Button _cdPreset10 = new() { Text = "10 min" };
    private readonly Button _cdPreset25 = new() { Text = "25 min" };
    private readonly Button _cdPreset60 = new() { Text = "1 h" };
    private readonly Button _cdToggleBtn = new();
    private readonly Button _cdCancelBtn = new() { Text = "✕ Cancel" };

    private bool _countdownRunning;
    private double _syncedCountdownTotal = -1;

    private static readonly int[] _presetSeconds = { 300, 600, 1500, 3600 };

    private bool _allowClose;

    public AppMode Mode => AppMode.Normal;

    public NormalForm(TimerCommands commands, IConfigStore configStore, Action<AppMode> onModeRequested, IAppLogger? logger = null)
    {
        _commands = commands;
        _configStore = configStore;
        _onModeRequested = onModeRequested;
        _logger = logger ?? NullLogger.Instance;

        Text = "PulseTrack";
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        MinimizeBox = true;
        MaximizeBox = false;
        BackColor = Color.FromArgb(32, 32, 36);
        ForeColor = Color.White;
        ClientSize = WindowPlacement.RestoreClientSize(_configStore.Load(), _defaultSize);
        MinimumSize = new Size(360, 340);
        DoubleBuffered = true;

        BuildLayout();
        Render(TimerViewState.Empty);
    }

    private void BuildLayout()
    {
        BuildTabs();
        BuildStopwatchTab();
        BuildTimerTab();
        BuildFooter();
    }

    private void BuildTabs()
    {
        _tabs.Location = new Point(10, 8);
        _tabs.Size = new Size(356, 282);
        _tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _tabs.BackColor = Color.FromArgb(44, 44, 50);
        _tabs.ForeColor = Color.White;
        _tabs.Font = new Font("Segoe UI", 9, FontStyle.Regular);

        _stopwatchPage.BackColor = BackColor;
        _timerPage.BackColor = BackColor;
        _tabs.TabPages.Add(_stopwatchPage);
        _tabs.TabPages.Add(_timerPage);
        _tabs.SelectedIndex = 0;
        _tabs.SelectedIndexChanged += (_, _) => SyncFocusFromTab();
        Controls.Add(_tabs);
    }

    private void SyncFocusFromTab() =>
        _commands.SetFocus(_tabs.SelectedIndex == 1 ? FocusMode.Timer : FocusMode.Stopwatch);

    private void BuildStopwatchTab()
    {
        _appLabel.Location = new Point(12, 8);
        _appLabel.Size = new Size(196, 24);
        _appLabel.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        _appLabel.ForeColor = Color.FromArgb(200, 200, 200);
        _stopwatchPage.Controls.Add(_appLabel);

        _pickBtn.Location = new Point(214, 6);
        _pickBtn.Size = new Size(116, 28);
        StyleButton(_pickBtn);
        _pickBtn.Click += (_, _) => _ = _commands.PickAppAsync();
        _stopwatchPage.Controls.Add(_pickBtn);

        _clockLabel.Location = new Point(12, 38);
        _clockLabel.Size = new Size(326, 56);
        _clockLabel.Font = new Font("Segoe UI", 30, FontStyle.Bold);
        _clockLabel.ForeColor = Color.White;
        _stopwatchPage.Controls.Add(_clockLabel);

        _lapLabel.Location = new Point(12, 96);
        _lapLabel.Size = new Size(326, 18);
        _lapLabel.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _lapLabel.ForeColor = Color.FromArgb(150, 150, 150);
        _stopwatchPage.Controls.Add(_lapLabel);

        _countdownLabel.Location = new Point(12, 116);
        _countdownLabel.Size = new Size(326, 16);
        _countdownLabel.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _countdownLabel.ForeColor = Color.FromArgb(255, 190, 90);
        _stopwatchPage.Controls.Add(_countdownLabel);

        _toggleBtn.Location = new Point(12, 138);
        _toggleBtn.Size = new Size(100, 34);
        StyleButton(_toggleBtn);
        _toggleBtn.Click += async (_, _) => await _commands.ToggleStartPauseAsync().ConfigureAwait(true);
        _stopwatchPage.Controls.Add(_toggleBtn);

        _lapBtn.Location = new Point(118, 138);
        _lapBtn.Size = new Size(100, 34);
        StyleButton(_lapBtn);
        _lapBtn.Click += async (_, _) => await _commands.LapAsync().ConfigureAwait(true);
        _stopwatchPage.Controls.Add(_lapBtn);

        _stopBtn.Location = new Point(224, 138);
        _stopBtn.Size = new Size(100, 34);
        StyleButton(_stopBtn);
        _stopBtn.Click += async (_, _) => await _commands.StopAsync().ConfigureAwait(true);
        _stopwatchPage.Controls.Add(_stopBtn);

        _lapList.Location = new Point(12, 178);
        _lapList.Size = new Size(326, 64);
        _lapList.Font = new Font("Consolas", 9, FontStyle.Regular);
        _lapList.BackColor = Color.FromArgb(24, 24, 28);
        _lapList.ForeColor = Color.FromArgb(220, 220, 220);
        _lapList.BorderStyle = BorderStyle.FixedSingle;
        _stopwatchPage.Controls.Add(_lapList);
    }

    private void BuildTimerTab()
    {
        _cdClockLabel.Location = new Point(12, 20);
        _cdClockLabel.Size = new Size(326, 56);
        _cdClockLabel.Font = new Font("Segoe UI", 30, FontStyle.Bold);
        _cdClockLabel.ForeColor = Color.White;
        _timerPage.Controls.Add(_cdClockLabel);

        _cdStatusLabel.Location = new Point(12, 78);
        _cdStatusLabel.Size = new Size(326, 18);
        _cdStatusLabel.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _cdStatusLabel.ForeColor = Color.FromArgb(255, 190, 90);
        _timerPage.Controls.Add(_cdStatusLabel);

        _cdMinLabel.Location = new Point(30, 108);
        _cdMinLabel.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        _cdMinLabel.ForeColor = Color.FromArgb(140, 140, 140);
        _timerPage.Controls.Add(_cdMinLabel);

        _cdSecLabel.Location = new Point(160, 108);
        _cdSecLabel.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        _cdSecLabel.ForeColor = Color.FromArgb(140, 140, 140);
        _timerPage.Controls.Add(_cdSecLabel);

        _cdMinutes.Location = new Point(30, 130);
        _cdMinutes.Size = new Size(100, 24);
        _cdMinutes.Minimum = 0;
        _cdMinutes.Maximum = 180;
        _cdMinutes.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _cdMinutes.BackColor = Color.FromArgb(24, 24, 28);
        _cdMinutes.ForeColor = Color.White;
        _timerPage.Controls.Add(_cdMinutes);

        _cdSeconds.Location = new Point(160, 130);
        _cdSeconds.Size = new Size(100, 24);
        _cdSeconds.Minimum = 0;
        _cdSeconds.Maximum = 59;
        _cdSeconds.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _cdSeconds.BackColor = Color.FromArgb(24, 24, 28);
        _cdSeconds.ForeColor = Color.White;
        _timerPage.Controls.Add(_cdSeconds);

        var lastTotal = Math.Clamp(_configStore.Load().LastCountdownSeconds, 0, 180 * 60);
        _cdMinutes.Value = lastTotal / 60;
        _cdSeconds.Value = lastTotal % 60;
        _syncedCountdownTotal = lastTotal;

        _cdMinutes.ValueChanged += (_, _) => OnTimerInputChanged();
        _cdSeconds.ValueChanged += (_, _) => OnTimerInputChanged();

        _cdStartBtn.Location = new Point(270, 129);
        _cdStartBtn.Size = new Size(66, 26);
        StyleButton(_cdStartBtn);
        _cdStartBtn.Click += (_, _) => StartCountdownFromInputs();
        _timerPage.Controls.Add(_cdStartBtn);

        var presets = new (Button Btn, int X)[] { (_cdPreset5, 12), (_cdPreset10, 96), (_cdPreset25, 180), (_cdPreset60, 264) };
        for (int i = 0; i < presets.Length; i++)
        {
            var btn = presets[i].Btn;
            var seconds = _presetSeconds[i];
            btn.Location = new Point(presets[i].X, 166);
            btn.Size = new Size(78, 28);
            StyleButton(btn);
            btn.Click += (_, _) => StagePreset(seconds);
            _timerPage.Controls.Add(btn);
        }

        _cdToggleBtn.Location = new Point(12, 204);
        _cdToggleBtn.Size = new Size(158, 34);
        StyleButton(_cdToggleBtn);
        _cdToggleBtn.Click += (_, _) => ToggleCountdown();
        _timerPage.Controls.Add(_cdToggleBtn);

        _cdCancelBtn.Location = new Point(176, 204);
        _cdCancelBtn.Size = new Size(158, 34);
        StyleButton(_cdCancelBtn);
        _cdCancelBtn.Click += (_, _) => CancelCountdown();
        _timerPage.Controls.Add(_cdCancelBtn);
    }

    private void BuildFooter()
    {
        _viewLabel.Location = new Point(14, 302);
        _viewLabel.Size = new Size(46, 22);
        _viewLabel.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        _viewLabel.ForeColor = Color.FromArgb(140, 140, 140);
        _viewLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(_viewLabel);

        _taskbarBtn.Location = new Point(212, 298);
        _taskbarBtn.Size = new Size(72, 28);
        StyleViewButton(_taskbarBtn);
        _taskbarBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _taskbarBtn.Click += (_, _) => SwitchTo(AppMode.Taskbar);
        Controls.Add(_taskbarBtn);

        _pipBtn.Location = new Point(290, 298);
        _pipBtn.Size = new Size(72, 28);
        StyleViewButton(_pipBtn);
        _pipBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _pipBtn.Click += (_, _) => SwitchTo(AppMode.Pip);
        Controls.Add(_pipBtn);
    }

    private double InputTotalSeconds() =>
        (double)_cdMinutes.Value * 60 + (double)_cdSeconds.Value;

    private void StartCountdownFromInputs()
    {
        var total = InputTotalSeconds();
        if (total <= 0) return;
        _commands.StartCountdown(total);
    }

    private void StagePreset(int seconds)
    {
        _commands.StageCountdown(seconds);
        SetTimerInputs(seconds);
    }

    private void OnTimerInputChanged()
    {
        UpdateCountdownStartEnabled();
        _commands.StageCountdown(InputTotalSeconds());
    }

    private void ToggleCountdown() => _commands.ToggleCountdown();

    private void CancelCountdown() => _commands.CancelCountdown();

    internal void ClickTimerStartForTest() => StartCountdownFromInputs();

    internal void ClickTimerPresetForTest(int index) => StagePreset(_presetSeconds[index]);

    internal void ClickTimerToggleForTest() => ToggleCountdown();

    internal void ClickTimerCancelForTest() => CancelCountdown();

    private void SetTimerInputs(int totalSeconds)
    {
        var total = Math.Clamp(totalSeconds, 0, 180 * 60);
        _syncedCountdownTotal = total;
        _cdMinutes.Value = total / 60;
        _cdSeconds.Value = total % 60;
    }

    private void UpdateCountdownStartEnabled() =>
        _cdStartBtn.Enabled = InputTotalSeconds() > 0 && !_countdownRunning;

    private void SyncCountdownInputs(CountdownView? countdown)
    {
        if (countdown is null)
        {
            _syncedCountdownTotal = -1;
            return;
        }

        if (Math.Abs(countdown.TotalSeconds - _syncedCountdownTotal) < 0.001)
            return;

        SetTimerInputs((int)Math.Round(countdown.TotalSeconds));
    }

    private static void StyleViewButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(72, 72, 82);
        btn.BackColor = Color.FromArgb(42, 42, 48);
        btn.ForeColor = Color.FromArgb(210, 210, 210);
        btn.Font = new Font("Segoe UI", 8, FontStyle.Regular);
    }

    internal void SwitchTo(AppMode mode)
    {
        try { _onModeRequested(mode); }
        catch (Exception ex) { _logger.Log("Normal", $"SwitchMode: {ex.Message}"); }
    }

    internal IReadOnlyList<string> LapRows =>
        _lapList.Items.Cast<object>().Select(i => i?.ToString() ?? "").ToList();

    internal bool ToggleEnabled => _toggleBtn.Enabled;

    internal Rectangle LapListBounds => ToFormCoordinates(_lapList);

    internal Rectangle TaskbarButtonBounds => _taskbarBtn.Bounds;

    internal Rectangle PipButtonBounds => _pipBtn.Bounds;

    internal IReadOnlyList<string> TabNames => new[] { _stopwatchPage.Text, _timerPage.Text };

    internal int ActiveTabIndex => _tabs.SelectedIndex;

    internal void ActivateTabForTest(int index)
    {
        _tabs.SelectedIndex = index;
        SyncFocusFromTab();
    }

    internal string TimerClockText => _cdClockLabel.Text;

    internal string TimerStatusText => _cdStatusLabel.Text;

    internal NumericUpDown TimerMinutes => _cdMinutes;

    internal NumericUpDown TimerSeconds => _cdSeconds;

    internal Button TimerStartButton => _cdStartBtn;

    internal Button TimerToggleButton => _cdToggleBtn;

    internal Button TimerCancelButton => _cdCancelBtn;

    internal IReadOnlyList<Rectangle> TimerControlBounds => new[]
    {
        _cdClockLabel.Bounds, _cdStatusLabel.Bounds, _cdMinutes.Bounds, _cdSeconds.Bounds,
        _cdStartBtn.Bounds, _cdPreset5.Bounds, _cdPreset10.Bounds, _cdPreset25.Bounds,
        _cdPreset60.Bounds, _cdToggleBtn.Bounds, _cdCancelBtn.Bounds
    };

    private Rectangle ToFormCoordinates(Control control)
    {
        var bounds = control.Bounds;
        for (var parent = control.Parent; parent != null && parent != this; parent = parent.Parent)
            bounds.Offset(parent.Location);
        return bounds;
    }

    private static void StyleButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Color.FromArgb(58, 58, 66);
        btn.ForeColor = Color.White;
        btn.Font = new Font("Segoe UI", 9, FontStyle.Regular);
    }

    public void Render(TimerViewState state)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            try { BeginInvoke(() => Render(state)); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            return;
        }

        _appLabel.Text = state.AppDisplay;
        _appLabel.ForeColor = state.HasApp ? Color.FromArgb(200, 200, 200) : Color.FromArgb(130, 130, 130);
        _clockLabel.Text = state.Clock;
        _lapLabel.Text = state.LapText;
        _countdownLabel.Text = state.Countdown?.Label ?? "";

        _toggleBtn.Text = state.Running ? "⏸ Pause" : state.CanStop ? "▶ Resume" : "▶ Start";
        _lapBtn.Enabled = state.CanLap;
        _stopBtn.Enabled = state.CanStop;

        var countdown = state.Countdown;
        _cdClockLabel.Text = countdown?.Text ?? "0:00";
        _cdStatusLabel.Text = countdown?.Label ?? "No timer set";
        _countdownRunning = countdown is { Running: true };
        _cdToggleBtn.Text = _countdownRunning
            ? "⏸ Pause"
            : countdown is { Started: true } ? "▶ Resume" : "▶ Start";
        _cdToggleBtn.Enabled = countdown is { Finished: false };
        _cdCancelBtn.Enabled = countdown is not null;
        UpdateCountdownStartEnabled();
        SyncCountdownInputs(countdown);

        var tabIndex = state.Focus == FocusMode.Timer ? 1 : 0;
        if (_tabs.SelectedIndex != tabIndex)
            _tabs.SelectedIndex = tabIndex;

        SyncLaps(state.Laps);
    }

    private void SyncLaps(IReadOnlyList<LapInfo> laps)
    {
        if (_lapList.Items.Count != laps.Count)
        {
            _lapList.BeginUpdate();
            _lapList.Items.Clear();
            foreach (var lap in laps)
                _lapList.Items.Add(TimerViewStateFactory.LapLabel(lap));
            _lapList.EndUpdate();
            if (_lapList.Items.Count > 0)
                _lapList.TopIndex = _lapList.Items.Count - 1;
            return;
        }

        for (int i = 0; i < laps.Count; i++)
        {
            var text = TimerViewStateFactory.LapLabel(laps[i]);
            if (!Equals(_lapList.Items[i], text))
                _lapList.Items[i] = text;
        }
    }

    public void SetVisible(bool visible)
    {
        if (IsDisposed)
        {
            return;
        }

        if (!visible)
        {
            PersistBounds();
            if (Visible) Hide();
            return;
        }

        if (!Visible)
        {
            RestoreLocation();
            Show();
        }
    }

    private void RestoreLocation()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = WindowPlacement.Restore(_configStore.Load(), AppMode.Normal, Size, area);
    }

    private void PersistBounds()
    {
        if (!IsHandleCreated || IsDisposed) return;
        try
        {
            _configStore.Update(c =>
            {
                WindowPlacement.SaveLocation(c, AppMode.Normal, Location);
                WindowPlacement.SaveClientSize(c, ClientSize);
            });
        }
        catch (Exception ex) { _logger.Log("Normal", $"SaveBounds: {ex.Message}"); }
    }

    public void AllowClose() => _allowClose = true;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            PersistBounds();
            Hide();
            return;
        }

        PersistBounds();
        base.OnFormClosing(e);
    }

    internal void SimulateSaveErrorForTest(Exception ex) => _logger.Log("Normal", $"SaveBounds: {ex.Message}");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PersistBounds();
            foreach (Control c in Controls)
            {
                c.Font?.Dispose();
                c.Dispose();
            }
            foreach (var page in new[] { _stopwatchPage, _timerPage })
            {
                foreach (Control c in page.Controls)
                {
                    c.Font?.Dispose();
                    c.Dispose();
                }
            }
        }
        base.Dispose(disposing);
    }
}
