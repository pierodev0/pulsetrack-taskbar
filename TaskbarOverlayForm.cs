namespace PulseTrack.Taskbar;

public class TaskbarOverlayForm : Form
{
    private const int _scrollSpeed = 2;
    private const int _scrollIntervalMs = 50;
    private const int _zBumpIntervalMs = 100;

    private readonly TaskbarLayout _layout;
    private readonly IAppLogger _logger;
    private ITaskbarWidget _widget = new StaticTextWidget("");

    private string _text = "";
    private int _scrollOffset;
    private int _textWidth;
    private bool _idle => string.IsNullOrEmpty(_text);
    private bool _fullScreen;
    private readonly System.Windows.Forms.Timer _scrollTimer = new();
    private readonly System.Windows.Forms.Timer _reposTimer = new();
    private Font _font = new("Segoe UI", 9, FontStyle.Regular);
    private Color _textColor = Color.FromArgb(240, 255, 255, 255);
    private bool _showBackground = true;
    private Color _bgColor = Color.FromArgb(180, 26, 26, 46);

    private static readonly int WM_TASKBARCREATED_MSG = NativeMethods.RegisterWindowMessage("TaskbarCreated");

    public event Action? LeftClicked;
    public event Action? RightClicked;

    public TaskbarOverlayForm(IAppLogger? logger = null, ITaskbarGeometry? geometry = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _layout = new TaskbarLayout(geometry ?? new Win32TaskbarGeometry(), _logger);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        TransparencyKey = Color.Black;
        BackColor = Color.Black;
        DoubleBuffered = true;
        Width = _widget.GetWidthHint();
        Height = TaskbarLayout.BarHeight;

        _scrollTimer.Interval = _scrollIntervalMs;
        _scrollTimer.Tick += (_, _) => { try { OnScrollTick(); } catch (Exception ex) { _logger.Log("Overlay", $"ScrollTimer: {ex.Message}"); } };

        _reposTimer.Interval = _zBumpIntervalMs;
        _reposTimer.Tick += (_, _) => { try { RepositionWithFullscreenCheck(); } catch (Exception ex) { _logger.Log("Overlay", $"ReposTimer: {ex.Message}"); } };
    }

    public void SetWidget(ITaskbarWidget widget)
    {
        _widget = widget;
        SetTimer(widget.GetText());
    }

    private void OnScrollTick()
    {
        _scrollOffset -= _scrollSpeed;
        Invalidate();
    }

    internal void SimulateScrollErrorForTest(Exception ex) => _logger.Log("Overlay", $"ScrollTimer: {ex.Message}");
    internal void SimulateReposErrorForTest(Exception ex) => _logger.Log("Overlay", $"ReposTimer: {ex.Message}");

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x8000000;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _reposTimer.Start();
        Reposition();
    }

    private void RepositionWithFullscreenCheck()
    {
        var pos = _layout.Compute(_widget.GetWidthHint());
        bool wasFull = _fullScreen;
        _fullScreen = pos.Fullscreen;

        if (_fullScreen)
        {
            if (!wasFull) Visible = false;
            return;
        }

        if (wasFull)
        {
            Visible = true;
            Reposition();
        }
        else
        {
            Reposition();
        }
    }

    private void Reposition()
    {
        if (!IsHandleCreated || IsDisposed) return;

        var pos = _layout.Compute(Math.Max(_textWidth + 30, _widget.GetWidthHint()));
        if (pos.Fullscreen)
        {
            Visible = false;
            _fullScreen = true;
            return;
        }
        _fullScreen = false;

        Width = pos.Width;
        Height = pos.Height;
        Location = new Point(pos.X, pos.Y);
        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
    }

    public void SetTimer(string text)
    {
        if (IsDisposed) return;
        _text = text;

        if (_idle)
        {
            _scrollOffset = 0;
            _scrollTimer.Stop();
            Invalidate();
            return;
        }

        try
        {
            using var g = CreateGraphics();
            _textWidth = TextRenderer.MeasureText(g, _text, _font).Width;
        }
        catch
        {
            _textWidth = _text.Length * 12;
        }

        if (_textWidth > Width - 10)
        {
            _scrollOffset = Width;
            _scrollTimer.Start();
        }
        else
        {
            _scrollOffset = 0;
            _scrollTimer.Stop();
        }

        Reposition();
        Invalidate();
    }

    public void ApplyConfig(OverlayConfig config)
    {
        _font.Dispose();
        _font = new Font(config.FontFamily, config.FontSize, (FontStyle)config.FontStyle);
        _textColor = Color.FromArgb(config.TextAlpha, Color.FromArgb(config.TextColorArgb));
        _showBackground = config.ShowBackground;
        _bgColor = Color.FromArgb(config.BackgroundAlpha, Color.FromArgb(config.BackgroundColorArgb));
        TransparencyKey = Color.FromArgb(config.TransparencyKeyArgb);

        if (!_idle)
            SetTimer(_text);
        Reposition();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_showBackground)
        {
            using var bgBrush = new SolidBrush(_bgColor);
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        if (!_idle)
            DrawTimerText(g);
    }

    private void DrawTimerText(Graphics g)
    {
        if (_textWidth <= Width)
        {
            var rect = new Rectangle(0, 0, Width, Height);
            TextRenderer.DrawText(g, _text, _font, rect, _textColor, Color.Transparent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else
        {
            TextRenderer.DrawText(g, _text, _font, new Rectangle(_scrollOffset, 0, _textWidth + 60, Height), _textColor, Color.Transparent,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, _text, _font, new Rectangle(_scrollOffset + _textWidth + 60, 0, _textWidth + 60, Height), _textColor, Color.Transparent,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (_scrollOffset + _textWidth + 60 < 0)
                _scrollOffset += _textWidth + 60;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Right)
            RightClicked?.Invoke();
        else
            LeftClicked?.Invoke();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_TASKBARCREATED_MSG)
        {
            _logger.Log("Overlay", "WndProc: TaskbarCreated detected, re-positioning");
            Reposition();
            return;
        }

        if (m.Msg == NativeMethods.WM_GETOBJECT)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        if (m.Msg == NativeMethods.WM_NCHITTEST)
        {
            base.WndProc(ref m);
            if (m.Result == (IntPtr)NativeMethods.HTTRANSPARENT)
                m.Result = (IntPtr)NativeMethods.HTCLIENT;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
            _scrollTimer.Dispose();
            _reposTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class StaticTextWidget(string text) : ITaskbarWidget
    {
        public string Id => "static";
        public string GetText() => text;
        public int GetWidthHint() => 200;
        public void Refresh(TimerTick tick) { }
    }
}
