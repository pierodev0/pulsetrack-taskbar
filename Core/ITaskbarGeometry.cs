namespace PulseTrack.Taskbar;

public record TaskbarRect(int Left, int Top, int Width, int Height)
{
    public int W => Width;
    public int H => Height;
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public record OverlayPosition(int X, int Y, int Width, int Height, bool Fullscreen);

public interface ITaskbarGeometry
{
    TaskbarRect GetTaskbarRect();
    TaskbarRect? GetStartButtonRect();
    TaskbarRect? GetTrayRect();
    IReadOnlyList<(int Left, int Right)> GetChildZones();
    IntPtr GetForegroundWindow();
    string GetWindowClass(IntPtr hWnd);
    Rectangle GetWindowRect(IntPtr hWnd);
    Rectangle GetScreenBounds(IntPtr hWnd);
}
