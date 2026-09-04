using System.Text.Json;

namespace PulseTrack.Taskbar;

public sealed class FileConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;
    private readonly IAppLogger _logger;

    public FileConfigStore(string? directory = null, IAppLogger? logger = null)
    {
        var dir = directory ?? FileLogger.DefaultDirectory();
        _settingsPath = Path.Combine(dir, "settings.json");
        ProbeLogPath = Path.Combine(dir, "probe.log");
        _logger = logger ?? NullLogger.Instance;
    }

    public string ProbeLogPath { get; }

    public OverlayConfig Load()
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
            _logger.Log("Config", $"Load failed: {ex.GetType().Name}: {ex.Message}");
        }
        return new OverlayConfig();
    }

    public void Save(OverlayConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(config, _jsonOptions));
        }
        catch (Exception ex)
        {
            _logger.Log("Config", $"Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
