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
    private readonly Button _toggleBtn = new();
    private readonly Button _lapBtn = new() { Text = "🏁 Lap" };
    private readonly Button _stopBtn = new() { Text = "⏹ Stop" };
    private readonly ListBox _lapList = new() { IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _viewLabel = new() { Text = "View:", TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _taskbarBtn = new() { Text = "Taskbar" };
    private readonly Button _pipBtn = new() { Text = "PiP" };

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
        _appLabel.Location = new Point(14, 14);
        _appLabel.Size = new Size(210, 24);
        _appLabel.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        _appLabel.ForeColor = Color.FromArgb(200, 200, 200);
        _appLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_appLabel);

        _pickBtn.Location = new Point(238, 12);
        _pickBtn.Size = new Size(124, 28);
        StyleButton(_pickBtn);
        _pickBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _pickBtn.Click += (_, _) => _ = _commands.PickAppAsync();
        Controls.Add(_pickBtn);

        _clockLabel.Location = new Point(14, 46);
        _clockLabel.Size = new Size(348, 60);
        _clockLabel.Font = new Font("Segoe UI", 30, FontStyle.Bold);
        _clockLabel.ForeColor = Color.White;
        _clockLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_clockLabel);

        _lapLabel.Location = new Point(14, 108);
        _lapLabel.Size = new Size(348, 18);
        _lapLabel.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _lapLabel.ForeColor = Color.FromArgb(150, 150, 150);
        _lapLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_lapLabel);

        _toggleBtn.Location = new Point(14, 136);
        _toggleBtn.Size = new Size(104, 34);
        StyleButton(_toggleBtn);
        _toggleBtn.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _toggleBtn.Click += async (_, _) => await _commands.ToggleStartPauseAsync().ConfigureAwait(true);
        Controls.Add(_toggleBtn);

        _lapBtn.Location = new Point(126, 136);
        _lapBtn.Size = new Size(104, 34);
        StyleButton(_lapBtn);
        _lapBtn.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _lapBtn.Click += async (_, _) => await _commands.LapAsync().ConfigureAwait(true);
        Controls.Add(_lapBtn);

        _stopBtn.Location = new Point(258, 136);
        _stopBtn.Size = new Size(104, 34);
        StyleButton(_stopBtn);
        _stopBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _stopBtn.Click += async (_, _) => await _commands.StopAsync().ConfigureAwait(true);
        Controls.Add(_stopBtn);

        _lapList.Location = new Point(14, 180);
        _lapList.Size = new Size(348, 110);
        _lapList.Font = new Font("Consolas", 9, FontStyle.Regular);
        _lapList.BackColor = Color.FromArgb(24, 24, 28);
        _lapList.ForeColor = Color.FromArgb(220, 220, 220);
        _lapList.BorderStyle = BorderStyle.FixedSingle;
        _lapList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_lapList);

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

    internal Rectangle LapListBounds => _lapList.Bounds;

    internal Rectangle TaskbarButtonBounds => _taskbarBtn.Bounds;

    internal Rectangle PipButtonBounds => _pipBtn.Bounds;

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

        _toggleBtn.Text = state.Running ? "⏸ Pause" : state.CanStop ? "▶ Resume" : "▶ Start";
        _lapBtn.Enabled = state.CanLap;
        _stopBtn.Enabled = state.CanStop;

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
        }
        base.Dispose(disposing);
    }
}
