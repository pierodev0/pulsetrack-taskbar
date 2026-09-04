using System.Runtime.InteropServices;
using System.Text;

namespace PulseTrack.Taskbar;

public class TaskbarOverlayForm : Form
{
    private string _text = "";
    private int _scrollOffset;
    private int _textWidth;
    private bool _idle => string.IsNullOrEmpty(_text);
    private const int _preferredWidth = 160;
    private const int _barHeight = 40;
    private const int _gapFromNeighbor = 8;
    private const int _scrollSpeed = 2;
    private const int _scrollIntervalMs = 50;
    private const int _zBumpIntervalMs = 100;

    private static readonly string[] _systemClasses =
    {
        "Start", "TrayNotifyWnd", "TrayDummySearchControl",
        "ReBarWindow32", "MSTaskSwWClass", "MSTaskListWClass"
    };

    private IntPtr _taskbarHwnd;
    private bool _fullScreen;
    private readonly System.Windows.Forms.Timer _scrollTimer = new();
    private readonly System.Windows.Forms.Timer _reposTimer = new();
    private Font _font = new("Segoe UI", 9, FontStyle.Regular);
    private Color _textColor = Color.FromArgb(240, 255, 255, 255);
    private bool _showBackground = true;
    private Color _bgColor = Color.FromArgb(180, 26, 26, 46);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    public event Action? LeftClicked;
    public event Action? RightClicked;

    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const int WM_GETOBJECT = 0x003D;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTTRANSPARENT = -1;

    private struct RECT { public int left, top, right, bottom; public int W => right - left; public int H => bottom - top; }

    private static readonly int WM_TASKBARCREATED_MSG = RegisterWindowMessage("TaskbarCreated");

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int RegisterWindowMessage(string lpString);

    public TaskbarOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        TransparencyKey = Color.Black;
        BackColor = Color.Black;
        DoubleBuffered = true;
        Width = _preferredWidth;
        Height = _barHeight;

        _scrollTimer.Interval = _scrollIntervalMs;
        _scrollTimer.Tick += (_, _) => { try { _scrollOffset -= _scrollSpeed; Invalidate(); } catch (Exception ex) { OverlayConfig.Log("Overlay", $"ScrollTimer: {ex.Message}"); } };

        _reposTimer.Interval = _zBumpIntervalMs;
        _reposTimer.Tick += (_, _) => { try { RepositionWithFullscreenCheck(); } catch (Exception ex) { OverlayConfig.Log("Overlay", $"ReposTimer: {ex.Message}"); } };
    }

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
        _taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        _reposTimer.Start();
        Reposition();
    }

    private void RepositionWithFullscreenCheck()
    {
        var fg = GetForegroundWindow();
        bool wasFull = _fullScreen;
        _fullScreen = fg != Handle && fg != IntPtr.Zero && IsFullScreenApp(fg);

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

    private static bool IsFullScreenApp(IntPtr hWnd)
    {
        var clsSb = new StringBuilder(256);
        int len = GetClassName(hWnd, clsSb, 256);
        string cls = len > 0 ? clsSb.ToString(0, len) : "";
        if (cls == "Progman" || cls == "WorkerW")
            return false;

        GetWindowRect(hWnd, out var r);
        var screen = Screen.FromHandle(hWnd);
        var b = screen.Bounds;
        return r.left <= b.Left && r.top <= b.Top && r.right >= b.Right && r.bottom >= b.Bottom;
    }

    private void Reposition()
    {
        if (!IsHandleCreated || IsDisposed) return;

        if (_taskbarHwnd == IntPtr.Zero)
            _taskbarHwnd = FindWindow("Shell_TrayWnd", null);

        if (_taskbarHwnd == IntPtr.Zero) return;

        GetWindowRect(_taskbarHwnd, out var tr);
        int maxW = Math.Min(tr.W / 3, 280);

        if (_idle)
            Width = Math.Min(_preferredWidth, maxW);
        else
            Width = Math.Min(Math.Max(_textWidth + 30, 80), maxW);

        Height = _barHeight;

        int x = FindLeftOfTrayArea(tr);
        int yCenter = tr.top + (tr.H - _barHeight) / 2;

        Location = new Point(tr.left + x, yCenter);
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
    }

    private int FindLeftOfTrayArea(RECT taskbarRect)
    {
        var startHwnd = FindWindowEx(_taskbarHwnd, IntPtr.Zero, "Start", null);
        bool isCentered = startHwnd != IntPtr.Zero;
        int startLeft = 0;
        if (isCentered)
        {
            GetWindowRect(startHwnd, out var startRect);
            startLeft = startRect.left - taskbarRect.left;
            isCentered = startLeft > taskbarRect.W * 0.2;
        }

        if (!isCentered)
            return RightOfRightmostChild(taskbarRect);

        var trayHwnd = FindWindowEx(_taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
        if (trayHwnd != IntPtr.Zero)
        {
            GetWindowRect(trayHwnd, out var trayRect);
            int candidateX = trayRect.left - taskbarRect.left - Width;

            if (!HasChildInZone(trayRect.left - taskbarRect.left, candidateX))
                return candidateX;
        }

        return Math.Max(startLeft - Width - _gapFromNeighbor, _gapFromNeighbor);
    }

    private bool HasChildInZone(int zoneRight, int zoneLeft)
    {
        var clsSb = new StringBuilder(256);
        var child = IntPtr.Zero;
        GetWindowRect(_taskbarHwnd, out var tr);

        while ((child = FindWindowEx(_taskbarHwnd, child, null, null)) != IntPtr.Zero)
        {
            int len = GetClassName(child, clsSb, 256);
            string cls = len > 0 ? clsSb.ToString(0, len) : "";
            if (IsSystemWindow(cls, 0)) continue;

            GetWindowRect(child, out var cr);

            int childRight = cr.right - tr.left;
            int childLeft = cr.left - tr.left;

            if (childRight > zoneLeft && childLeft < zoneRight)
                return true;
        }

        return false;
    }

    private int RightOfRightmostChild(RECT taskbarRect)
    {
        var clsSb = new StringBuilder(256);
        var child = IntPtr.Zero;
        int rightmostRight = 0;
        int leftOfRightmost = taskbarRect.W;

        while ((child = FindWindowEx(_taskbarHwnd, child, null, null)) != IntPtr.Zero)
        {
            GetWindowRect(child, out var cr);
            int len = GetClassName(child, clsSb, 256);
            string cls = len > 0 ? clsSb.ToString(0, len) : "";
            int w = cr.W;
            int childRight = cr.right - taskbarRect.left;

            if (!IsSystemWindow(cls, w) && childRight > rightmostRight)
            {
                rightmostRight = childRight;
                leftOfRightmost = cr.left - taskbarRect.left;
            }
        }

        if (rightmostRight > 0)
            return leftOfRightmost - Width - _gapFromNeighbor;

        var trayHwnd = FindWindowEx(_taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
        if (trayHwnd != IntPtr.Zero)
        {
            GetWindowRect(trayHwnd, out var trayRect);
            return trayRect.left - taskbarRect.left - Width - _gapFromNeighbor;
        }
        return taskbarRect.W - Width - 120;
    }

    private static bool IsSystemWindow(string className, int width)
    {
        if (className.StartsWith("Windows.UI.")) return true;
        foreach (var c in _systemClasses)
            if (className == c) return true;
        return width < 30 || width > 500;
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
            OverlayConfig.Log("Overlay", "WndProc: TaskbarCreated detected, re-positioning");
            _taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            Reposition();
            return;
        }

        if (m.Msg == WM_GETOBJECT)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);
            if (m.Result == (IntPtr)HTTRANSPARENT)
                m.Result = (IntPtr)HTCLIENT;
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
}
