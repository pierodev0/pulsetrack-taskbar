namespace PulseTrack.Taskbar;

public class PipForm : Form, ITimerSurface
{
    private static readonly Size _fixedSize = new(300, 88);

    private readonly TimerCommands _commands;
    private readonly IConfigStore _configStore;
    private readonly Action _onRestore;
    private readonly IAppLogger _logger;

    private readonly Label _appLabel = new() { AutoEllipsis = true };
    private readonly Label _timeLabel = new() { TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _lapLabel = new() { TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _pauseBtn = new();
    private readonly Button _lapBtn = new() { Text = "🏁" };
    private readonly Button _stopBtn = new() { Text = "⏹" };
    private readonly Button _closeBtn = new() { Text = "✕" };
    private bool _dragging;
    private Point _dragStart;

    public AppMode Mode => AppMode.Pip;

    public PipForm(TimerCommands commands, IConfigStore configStore, Action onRestore, IAppLogger? logger = null)
    {
        _commands = commands;
        _configStore = configStore;
        _onRestore = onRestore;
        _logger = logger ?? NullLogger.Instance;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = _fixedSize;
        BackColor = Color.FromArgb(30, 30, 34);
        ForeColor = Color.White;
        DoubleBuffered = true;

        BuildLayout();
        ApplyRounding();
        Render(TimerViewState.Empty);
    }

    private void BuildLayout()
    {
        _appLabel.Location = new Point(12, 6);
        _appLabel.Size = new Size(240, 16);
        _appLabel.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        _appLabel.ForeColor = Color.FromArgb(180, 180, 180);
        _appLabel.MouseDown += StartDrag;
        _appLabel.MouseMove += DoDrag;
        _appLabel.MouseUp += EndDrag;
        Controls.Add(_appLabel);

        _closeBtn.Location = new Point(262, 4);
        _closeBtn.Size = new Size(30, 24);
        StyleButton(_closeBtn);
        _closeBtn.ForeColor = Color.FromArgb(150, 150, 150);
        _closeBtn.Click += (_, _) => RestoreToNormal();
        Controls.Add(_closeBtn);

        _timeLabel.Location = new Point(12, 22);
        _timeLabel.Size = new Size(160, 34);
        _timeLabel.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        _timeLabel.ForeColor = Color.White;
        _timeLabel.MouseDown += StartDrag;
        _timeLabel.MouseMove += DoDrag;
        _timeLabel.MouseUp += EndDrag;
        Controls.Add(_timeLabel);

        _lapLabel.Location = new Point(12, 56);
        _lapLabel.Size = new Size(160, 16);
        _lapLabel.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        _lapLabel.ForeColor = Color.FromArgb(150, 150, 150);
        Controls.Add(_lapLabel);

        var btnY = 26;
        var btnSize = new Size(26, 24);

        _pauseBtn.Location = new Point(180, btnY);
        _pauseBtn.Size = btnSize;
        StyleButton(_pauseBtn);
        _pauseBtn.Click += async (_, _) => await _commands.ToggleStartPauseAsync().ConfigureAwait(true);
        Controls.Add(_pauseBtn);

        _lapBtn.Location = new Point(210, btnY);
        _lapBtn.Size = btnSize;
        StyleButton(_lapBtn);
        _lapBtn.Click += async (_, _) => await _commands.LapAsync().ConfigureAwait(true);
        Controls.Add(_lapBtn);

        _stopBtn.Location = new Point(180, 54);
        _stopBtn.Size = new Size(26, 22);
        StyleButton(_stopBtn);
        _stopBtn.Click += async (_, _) => await _commands.StopAsync().ConfigureAwait(true);
        Controls.Add(_stopBtn);

        MouseDown += StartDrag;
        MouseMove += DoDrag;
        MouseUp += EndDrag;
    }

    private static void StyleButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Color.FromArgb(50, 50, 55);
        btn.ForeColor = Color.White;
        btn.Font = new Font("Segoe UI", 8, FontStyle.Regular);
    }

    private void ApplyRounding()
    {
        try
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, 24, 24, 180, 90);
            path.AddArc(Width - 24, 0, 24, 24, 270, 90);
            path.AddArc(Width - 24, Height - 24, 24, 24, 0, 90);
            path.AddArc(0, Height - 24, 24, 24, 90, 90);
            path.CloseFigure();
            Region?.Dispose();
            Region = new Region(path);
        }
        catch { }
    }

    private void StartDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _dragStart = e.Location;
        }
    }

    private void DoDrag(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var screen = PointToScreen(e.Location);
        Location = new Point(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
    }

    private void EndDrag(object? sender, MouseEventArgs e) => _dragging = false;

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
        _appLabel.ForeColor = state.HasApp ? Color.FromArgb(180, 180, 180) : Color.FromArgb(120, 120, 120);
        _timeLabel.Text = state.Clock;
        _lapLabel.Text = state.LapText;
        _pauseBtn.Text = state.Glyph;
        _pauseBtn.Enabled = state.CanPause || state.CanStop;
        _lapBtn.Enabled = state.CanLap;
        _stopBtn.Enabled = state.CanStop;
    }

    public void SetVisible(bool visible)
    {
        if (IsDisposed) return;

        if (!visible)
        {
            PersistLocation();
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
        Location = WindowPlacement.Restore(_configStore.Load(), AppMode.Pip, Size, area);
    }

    private void PersistLocation()
    {
        if (!IsHandleCreated || IsDisposed) return;
        try { _configStore.Update(c => WindowPlacement.SaveLocation(c, AppMode.Pip, Location)); }
        catch (Exception ex) { _logger.Log("Pip", $"SavePosition: {ex.Message}"); }
    }

    internal void RestoreToNormal()
    {
        try { _onRestore(); }
        catch (Exception ex) { _logger.Log("Pip", $"Restore: {ex.Message}"); }
    }

    internal IReadOnlyList<Rectangle> ButtonBounds => new[]
    {
        _pauseBtn.Bounds, _lapBtn.Bounds, _stopBtn.Bounds, _closeBtn.Bounds
    };

    internal void SimulateSaveErrorForTest(Exception ex) => _logger.Log("Pip", $"SavePosition: {ex.Message}");

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyRounding();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PersistLocation();
            Region?.Dispose();
            foreach (Control c in Controls)
            {
                c.Font?.Dispose();
                c.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
