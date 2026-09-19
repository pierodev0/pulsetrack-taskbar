namespace PulseTrack.Taskbar;

public static class WindowPlacement
{
    public const int EdgeMargin = 24;
    private const int TitleBarHeight = 32;

    public static Point Restore(AppConfig config, AppMode mode, Size size, Rectangle workingArea)
    {
        var (x, y) = mode == AppMode.Pip ? (config.PipX, config.PipY) : (config.NormalX, config.NormalY);
        if (x >= 0 && y >= 0 && IsReachable(new Point(x, y), size, workingArea))
            return new Point(x, y);
        return DefaultFor(mode, size, workingArea);
    }

    public static void SaveLocation(AppConfig config, AppMode mode, Point location)
    {
        if (mode == AppMode.Pip)
        {
            config.PipX = location.X;
            config.PipY = location.Y;
        }
        else
        {
            config.NormalX = location.X;
            config.NormalY = location.Y;
        }
    }

    public static void SaveClientSize(AppConfig config, Size clientSize)
    {
        config.NormalWidth = clientSize.Width;
        config.NormalHeight = clientSize.Height;
    }

    public static Size RestoreClientSize(AppConfig config, Size fallback)
    {
        if (config.NormalWidth > 0 && config.NormalHeight > 0)
            return new Size(config.NormalWidth, config.NormalHeight);
        return fallback;
    }

    public static Point DefaultFor(AppMode mode, Size size, Rectangle workingArea) =>
        mode == AppMode.Pip
            ? new Point(workingArea.Right - size.Width - EdgeMargin, workingArea.Bottom - size.Height - EdgeMargin)
            : new Point(
                workingArea.Left + (workingArea.Width - size.Width) / 2,
                workingArea.Top + (workingArea.Height - size.Height) / 2);

    private static bool IsReachable(Point location, Size size, Rectangle workingArea)
    {
        var titleBar = new Rectangle(location, new Size(size.Width, Math.Min(TitleBarHeight, size.Height)));
        return workingArea.IntersectsWith(titleBar);
    }
}
