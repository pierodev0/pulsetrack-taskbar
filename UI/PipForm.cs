namespace PulseTrack.Taskbar;

public class PipForm : Form
{
    private readonly PipViewModel _vm;
    private readonly IAppLogger _logger;
    private readonly Action _onToggle;
    private readonly Func<Task> _onLap;
    private readonly Func<Task> _onStop;
    private readonly Action _onClosePip;

    private readonly Label _appLabel = new() { AutoEllipsis = true };
    private readonly Label _timeLabel = new() { TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _lapLabel = new() { TextAlign = ContentAlignment.MiddleCenter };
    private readonly Button _pauseBtn = new();
    private readonly Button _lapBtn = new() { Text = "🏁" };
    private readonly Button _stopBtn = new() { Text = "⏹" };
    private readonly Button _closeBtn = new() { Text = "✕" };
    private bool _dragging;
    private Point _dragStart;

    public PipForm(PipViewModel vm, IAppLogger logger, Action onToggle, Func<Task> onLap, Func<Task> onStop, Action onClosePip)
    {
        _vm = vm;
        _logger = logger;
        _onToggle = onToggle;
        _onLap = onLap;
        _onStop = onStop;
        _onClosePip = onClosePip;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(300, 88);
        BackColor = Color.FromArgb(30, 30, 34);
        ForeColor = Color.White;
        DoubleBuffered = true;

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
        _closeBtn.FlatStyle = FlatStyle.Flat;
        _closeBtn.FlatAppearance.BorderSize = 0;
        _closeBtn.ForeColor = Color.FromArgb(150, 150, 150);
        _closeBtn.Click += (_, _) => { try { _onClosePip(); } catch (Exception ex) { _logger.Log("Pip", $"Close: {ex.Message}"); } };
        Controls.Add(_closeBtn);

        _timeLabel.Location = new Point(12, 22);
        _timeLabel.Size = new Size(160, 34);
        _timeLabel.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        _timeLabel.TextAlign = ContentAlignment.MiddleLeft;
        _timeLabel.ForeColor = Color.White;
        _timeLabel.MouseDown += StartDrag;
        _timeLabel.MouseMove += DoDrag;
        _timeLabel.MouseUp += EndDrag;
        Controls.Add(_timeLabel);

        _lapLabel.Location = new Point(12, 56);
        _lapLabel.Size = new Size(160, 16);
        _lapLabel.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        _lapLabel.TextAlign = ContentAlignment.MiddleLeft;
        _lapLabel.ForeColor = Color.FromArgb(150, 150, 150);
        Controls.Add(_lapLabel);

        var btnY = 26;
        var btnSize = new Size(26, 24);
        _pauseBtn.Location = new Point(180, btnY);
        _pauseBtn.Size = btnSize;
        StyleButton(_pauseBtn);
        _pauseBtn.Click += (_, _) => { try { _onToggle(); } catch (Exception ex) { _logger.Log("Pip", $"Toggle: {ex.Message}"); } };
        Controls.Add(_pauseBtn);

        _lapBtn.Location = new Point(210, btnY);
        _lapBtn.Size = btnSize;
        StyleButton(_lapBtn);
        _lapBtn.Click += async (_, _) => { try { await _onLap().ConfigureAwait(true); } catch (Exception ex) { _logger.Log("Pip", $"Lap: {ex.Message}"); } };
        Controls.Add(_lapBtn);

        _stopBtn.Location = new Point(180, 54);
        _stopBtn.Size = new Size(26, 22);
        StyleButton(_stopBtn);
        _stopBtn.Click += async (_, _) => { try { await _onStop().ConfigureAwait(true); } catch (Exception ex) { _logger.Log("Pip", $"Stop: {ex.Message}"); } };
        Controls.Add(_stopBtn);

        MouseDown += StartDrag;
        MouseMove += DoDrag;
        MouseUp += EndDrag;

        ApplyRounding();
        UpdateUi(new TimerTick(0, false, 0, Array.Empty<LapInfo>()));
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

    public void UpdateUi(TimerTick tick)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => UpdateUi(tick)); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            return;
        }
        _vm.Refresh(tick);
        _appLabel.Text = _vm.AppName;
        _appLabel.ForeColor = _vm.AppName == "Choose app" ? Color.FromArgb(120, 120, 120) : Color.FromArgb(180, 180, 180);
        _timeLabel.Text = _vm.ElapsedText;
        _lapLabel.Text = _vm.LapText;
        _pauseBtn.Text = _vm.PauseGlyph;
        _pauseBtn.Enabled = _vm.CanPause || (!_vm.CanPause && _vm.CanStop);
        _lapBtn.Enabled = _vm.CanLap;
        _stopBtn.Enabled = _vm.CanStop;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyRounding();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
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
