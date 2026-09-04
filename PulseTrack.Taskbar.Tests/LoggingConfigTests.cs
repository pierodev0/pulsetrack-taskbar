using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class FakeLogger : IAppLogger
{
    public readonly List<(string Scope, string Message)> Entries = new();
    public void Log(string scope, string message) => Entries.Add((scope, message));
}

public sealed class LoggingTests
{
    [Fact]
    public void FormsTickScheduler_LogsTickExceptions()
    {
        var logger = new FakeLogger();
        using var sched = new FormsTickScheduler(50, logger);
        sched.Start(() => throw new InvalidOperationException("boom"));
        sched.FireForTest();

        Assert.Single(logger.Entries);
        Assert.Equal("Timer", logger.Entries[0].Scope);
        Assert.Contains("boom", logger.Entries[0].Message);
    }

    [Fact]
    public void FormsTickScheduler_NullLogger_NeverThrows()
    {
        using var sched = new FormsTickScheduler(50);
        sched.Start(() => throw new InvalidOperationException("boom"));

        var ex = Record.Exception(() => sched.FireForTest());
        Assert.Null(ex);
    }

    [Fact]
    public void TaskbarOverlayForm_UsesInjectedLogger()
    {
        var logger = new FakeLogger();
        using var form = new TaskbarOverlayForm(logger);

        form.SimulateScrollErrorForTest(new InvalidOperationException("scroll boom"));
        form.SimulateReposErrorForTest(new InvalidOperationException("repos boom"));

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Equal("Overlay", e.Scope));
    }

    [Fact]
    public void WindowWatcher_UsesInjectedLogger()
    {
        var logger = new FakeLogger();
        var windows = WindowWatcher.ListOpenWindows(logger);

        Assert.NotNull(windows);
        Assert.All(logger.Entries, e => Assert.Equal("Watcher", e.Scope));
    }
}

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pt-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var store = new FileConfigStore(_dir);
        var config = new OverlayConfig { FontFamily = "Consolas", FontSize = 12f, LastApp = "Code" };

        store.Save(config);
        var loaded = store.Load();

        Assert.Equal("Consolas", loaded.FontFamily);
        Assert.Equal(12f, loaded.FontSize);
        Assert.Equal("Code", loaded.LastApp);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = new FileConfigStore(_dir);

        var loaded = store.Load();

        Assert.Equal("Segoe UI", loaded.FontFamily);
        Assert.Equal("", loaded.LastApp);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndLogs()
    {
        var store = new FileConfigStore(_dir);
        var logger = new FakeLogger();
        var logging = new FileConfigStore(_dir, logger);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ not json");

        var loaded = logging.Load();

        Assert.Equal("Segoe UI", loaded.FontFamily);
        Assert.Single(logger.Entries);
        Assert.Equal("Config", logger.Entries[0].Scope);
    }

    [Fact]
    public void ProbeLogPath_LivesNextToSettings()
    {
        var store = new FileConfigStore(_dir);

        Assert.Equal(Path.Combine(_dir, "probe.log"), store.ProbeLogPath);
    }
}
