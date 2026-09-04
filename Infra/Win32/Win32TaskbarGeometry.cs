using System.Text;

namespace PulseTrack.Taskbar;

public sealed class Win32TaskbarGeometry : ITaskbarGeometry
{
    private IntPtr _taskbarHwnd;
    private TaskbarRect _lastKnown = new(0, 0, 1920, 40);

    public Win32TaskbarGeometry(IAppLogger? logger = null)
    {
    }

    private IntPtr EnsureTaskbar()
    {
        var found = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (found != IntPtr.Zero)
            _taskbarHwnd = found;
        return _taskbarHwnd;
    }

    public TaskbarRect GetTaskbarRect()
    {
        var hwnd = EnsureTaskbar();
        if (hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out var r))
            _lastKnown = new TaskbarRect(r.left, r.top, r.W, r.H);
        return _lastKnown;
    }

    public TaskbarRect? GetStartButtonRect() => GetChildRect("Start");

    public TaskbarRect? GetTrayRect() => GetChildRect("TrayNotifyWnd");

    private TaskbarRect? GetChildRect(string className)
    {
        var hwnd = EnsureTaskbar();
        if (hwnd == IntPtr.Zero) return null;
        var child = NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, className, null);
        if (child == IntPtr.Zero) return null;
        if (!NativeMethods.GetWindowRect(child, out var r)) return null;
        return new TaskbarRect(r.left, r.top, r.W, r.H);
    }

    public IReadOnlyList<(int Left, int Right)> GetChildZones()
    {
        var zones = new List<(int Left, int Right)>();
        var hwnd = EnsureTaskbar();
        if (hwnd == IntPtr.Zero) return zones;
        if (!NativeMethods.GetWindowRect(hwnd, out var tr)) return zones;

        var clsSb = new StringBuilder(256);
        var child = IntPtr.Zero;
        while ((child = NativeMethods.FindWindowEx(hwnd, child, null, null)) != IntPtr.Zero)
        {
            int len = NativeMethods.GetClassName(child, clsSb, 256);
            string cls = len > 0 ? clsSb.ToString(0, len) : "";
            if (!NativeMethods.GetWindowRect(child, out var cr)) continue;
            if (TaskbarLayout.IsSystemWindow(cls, cr.W)) continue;
            zones.Add((cr.left - tr.left, cr.right - tr.left));
        }
        return zones;
    }

    public IntPtr GetForegroundWindow() => NativeMethods.GetForegroundWindow();

    public string GetWindowClass(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        int len = NativeMethods.GetClassName(hWnd, sb, 256);
        return len > 0 ? sb.ToString(0, len) : "";
    }

    public Rectangle GetWindowRect(IntPtr hWnd)
    {
        if (!NativeMethods.GetWindowRect(hWnd, out var r)) return Rectangle.Empty;
        return new Rectangle(r.left, r.top, r.W, r.H);
    }

    public Rectangle GetScreenBounds(IntPtr hWnd)
    {
        try { return Screen.FromHandle(hWnd).Bounds; }
        catch { return Rectangle.Empty; }
    }
}
