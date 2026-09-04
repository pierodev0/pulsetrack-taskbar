namespace PulseTrack.Taskbar;

public sealed class TaskbarLayout
{
    public const int BarHeight = 40;
    public const int GapFromNeighbor = 8;
    public const int MaxWidth = 280;

    private static readonly string[] _systemClasses =
    {
        "Start", "TrayNotifyWnd", "TrayDummySearchControl",
        "ReBarWindow32", "MSTaskSwWClass", "MSTaskListWClass"
    };

    private readonly ITaskbarGeometry _geometry;

    public TaskbarLayout(ITaskbarGeometry geometry, IAppLogger? logger = null)
    {
        _geometry = geometry;
    }

    public OverlayPosition Compute(int widthHint)
    {
        var tr = _geometry.GetTaskbarRect();
        int maxW = Math.Min(tr.Width / 3, MaxWidth);
        int width = Math.Min(Math.Max(widthHint, 80), maxW);

        int x = FindLeftOfTrayArea(tr, width);
        int y = tr.Top + (tr.Height - BarHeight) / 2;

        return new OverlayPosition(x, y, width, BarHeight, IsFullscreenApp());
    }

    private bool IsFullscreenApp()
    {
        try
        {
            var fg = _geometry.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            var cls = _geometry.GetWindowClass(fg);
            if (cls == "Progman" || cls == "WorkerW") return false;
            var r = _geometry.GetWindowRect(fg);
            var b = _geometry.GetScreenBounds(fg);
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }
        catch { return false; }
    }

    private int FindLeftOfTrayArea(TaskbarRect taskbarRect, int width)
    {
        var start = _geometry.GetStartButtonRect();
        bool isCentered = start != null;
        int startLeft = 0;
        if (isCentered)
        {
            startLeft = start!.Left - taskbarRect.Left;
            isCentered = startLeft > taskbarRect.Width * 0.2;
        }

        if (!isCentered)
            return RightOfRightmostChild(taskbarRect, width);

        var tray = _geometry.GetTrayRect();
        if (tray != null)
        {
            int candidateX = tray.Left - taskbarRect.Left - width;
            if (!HasChildInZone(tray.Left - taskbarRect.Left, candidateX))
                return candidateX;
        }

        return Math.Max(startLeft - width - GapFromNeighbor, GapFromNeighbor);
    }

    private bool HasChildInZone(int zoneRight, int zoneLeft)
    {
        foreach (var (left, right) in _geometry.GetChildZones())
        {
            if (right > zoneLeft && left < zoneRight)
                return true;
        }
        return false;
    }

    private int RightOfRightmostChild(TaskbarRect taskbarRect, int width)
    {
        int rightmostRight = 0;
        int leftOfRightmost = taskbarRect.Width;

        foreach (var (left, right) in _geometry.GetChildZones())
        {
            if (right > rightmostRight)
            {
                rightmostRight = right;
                leftOfRightmost = left;
            }
        }

        if (rightmostRight > 0)
            return leftOfRightmost - width - GapFromNeighbor;

        var tray = _geometry.GetTrayRect();
        if (tray != null)
            return tray.Left - taskbarRect.Left - width - GapFromNeighbor;
        return taskbarRect.Width - width - 120;
    }

    public static bool IsSystemWindow(string className, int width)
    {
        if (className.StartsWith("Windows.UI.")) return true;
        foreach (var c in _systemClasses)
            if (className == c) return true;
        return width < 30 || width > 500;
    }
}
