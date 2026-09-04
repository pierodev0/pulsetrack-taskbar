using System.Diagnostics;
using System.Text;

namespace PulseTrack.Taskbar;

public record WindowInfo(string ProcessName, string Title, IntPtr Handle);

public static class WindowWatcher
{
    private static int OwnProcessId = Environment.ProcessId;

    private static WindowInfo? FromHandle(IntPtr hWnd)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0 || pid == (uint)OwnProcessId) return null;

            int len = NativeMethods.GetWindowTextLength(hWnd);
            if (len == 0) return null;

            var sb = new StringBuilder(len + 1);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
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
            var hWnd = NativeMethods.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return null;
            return FromHandle(hWnd);
        }
        catch { return null; }
    }

    public static List<WindowInfo> ListOpenWindows(IAppLogger? logger = null)
    {
        var result = new List<WindowInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                try
                {
                    if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                    var info = FromHandle(hWnd);
                    if (info == null) return true;
                    if (!seen.Add(info.ProcessName)) return true;
                    result.Add(info);
                }
                catch { }
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex) { (logger ?? NullLogger.Instance).Log("Watcher", $"ListOpenWindows: {ex.Message}"); }
        return result;
    }
}
