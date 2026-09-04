namespace PulseTrack.Taskbar;

public sealed class FileLogger : IAppLogger
{
    private readonly string _logPath;

    public FileLogger(string directory)
    {
        _logPath = Path.Combine(directory, "log.txt");
    }

    public static FileLogger Default { get; } = new(DefaultDirectory());

    public static string DefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PulseTrackTaskbar");

    public void Log(string scope, string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{scope}] {message}\n");
        }
        catch { }
    }
}
