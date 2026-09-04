using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PulseTrack.Taskbar;

public record WindowInfo(string ProcessName, string Title, IntPtr Handle);

public static class WindowWatcher
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private static int OwnProcessId = Environment.ProcessId;

    private static WindowInfo? FromHandle(IntPtr hWnd)
    {
        try
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0 || pid == (uint)OwnProcessId) return null;

            int len = GetWindowTextLength(hWnd);
            if (len == 0) return null;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString().Trim();
            if (string.IsNullOrEmpty(title)) return null;

            string processName;
            try { processName = Process.GetProcessById((int)pid).ProcessName; }
            catch { return null; }

            return new WindowInfo(processName, title, hWnd);
        }
        catch { return null; }
    }

    public static WindowInfo? GetForegroundApp()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return null;
            return FromHandle(hWnd);
        }
        catch { return null; }
    }

    public static List<WindowInfo> ListOpenWindows()
    {
        var result = new List<WindowInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            EnumWindows((hWnd, _) =>
            {
                try
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    var info = FromHandle(hWnd);
                    if (info == null) return true;
                    if (!seen.Add(info.ProcessName)) return true;
                    result.Add(info);
                }
                catch { }
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex) { OverlayConfig.Log("Watcher", $"ListOpenWindows: {ex.Message}"); }
        return result;
    }
}
