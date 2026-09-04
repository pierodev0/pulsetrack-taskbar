using System.Text.Json;

namespace PulseTrack.Taskbar;

public class OverlayConfig
{
    public string FontFamily { get; set; } = "Segoe UI";
    public float FontSize { get; set; } = 9f;
    public int FontStyle { get; set; } = 0;
    public int TextColorArgb { get; set; } = unchecked((int)0xF0FFFFFF);
    public int TextAlpha { get; set; } = 255;
    public bool ShowBackground { get; set; } = true;
    public int BackgroundColorArgb { get; set; } = unchecked((int)0xB41A1A2E);
    public int BackgroundAlpha { get; set; } = 180;
    public int TransparencyKeyArgb { get; set; } = unchecked((int)0xFF000000);
    public string LastApp { get; set; } = "";

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private static readonly string _settingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PulseTrackTaskbar");

    private static readonly string _settingsPath =
        Path.Combine(_settingsDir, "settings.json");

    private static readonly string _logPath =
        Path.Combine(_settingsDir, "log.txt");

    public static string ProbeLogPath => Path.Combine(_settingsDir, "probe.log");

    public static void Log(string scope, string message)
    {
        try
        {
            if (!Directory.Exists(_settingsDir))
                Directory.CreateDirectory(_settingsDir);
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{scope}] {message}\n");
        }
        catch { }
    }

    public static OverlayConfig Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<OverlayConfig>(json, _jsonOptions) ?? new OverlayConfig();
            }
        }
        catch (Exception ex)
        {
            Log("Config", $"Load failed: {ex.GetType().Name}: {ex.Message}");
        }
        return new OverlayConfig();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(_settingsDir))
                Directory.CreateDirectory(_settingsDir);
            var json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            Log("Config", $"Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
