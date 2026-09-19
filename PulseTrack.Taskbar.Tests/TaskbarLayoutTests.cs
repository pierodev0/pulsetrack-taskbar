using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class FakeTaskbarGeometry : ITaskbarGeometry
{
    public TaskbarRect Taskbar { get; set; } = new(0, 1000, 1920, 40);
    public TaskbarRect? StartButton { get; set; }
    public TaskbarRect? TrayArea { get; set; }
    public List<(int Left, int Right)> Children { get; set; } = new();
    public IntPtr ForegroundWindow { get; set; } = new(1234);
    public Rectangle ForegroundRect { get; set; } = new(0, 0, 100, 100);
    public string ForegroundClass { get; set; } = "Chrome_WidgetWin_1";
    public Rectangle ScreenBounds { get; set; } = new(0, 0, 1920, 1080);

    public TaskbarRect GetTaskbarRect() => Taskbar;
    public TaskbarRect? GetStartButtonRect() => StartButton;
    public TaskbarRect? GetTrayRect() => TrayArea;
    public IReadOnlyList<(int Left, int Right)> GetChildZones() => Children;
    public IntPtr GetForegroundWindow() => ForegroundWindow;
    public string GetWindowClass(IntPtr hWnd) => ForegroundClass;
    public Rectangle GetWindowRect(IntPtr hWnd) => ForegroundRect;
    public Rectangle GetScreenBounds(IntPtr hWnd) => ScreenBounds;
}

public sealed class TaskbarLayoutTests
{
    private static TaskbarLayout LeftLayout(FakeTaskbarGeometry geo) => new(geo, NullLogger.Instance);

    [Fact]
    public void LeftAligned_PlacesLeftOfRightmostChild()
    {
        var geo = new FakeTaskbarGeometry
        {
            Taskbar = new TaskbarRect(0, 1040, 1920, 40),
            Children = new List<(int, int)> { (100, 500), (500, 900) },
        };

        var pos = LeftLayout(geo).Compute(160);

        Assert.Equal(500 - 160 - 8, pos.X);
        Assert.Equal(1040 + (40 - 40) / 2, pos.Y);
        Assert.Equal(160, pos.Width);
        Assert.Equal(40, pos.Height);
        Assert.False(pos.Fullscreen);
    }

    [Fact]
    public void Centered_PlacesLeftOfTrayWhenFree()
    {
        var geo = new FakeTaskbarGeometry
        {
            Taskbar = new TaskbarRect(0, 1040, 1920, 40),
            StartButton = new TaskbarRect(800, 1040, 120, 40),
            TrayArea = new TaskbarRect(1700, 1040, 220, 40),
            Children = new List<(int, int)> { (100, 300) },
        };

        var pos = LeftLayout(geo).Compute(160);

        Assert.Equal(1700 - 160, pos.X);
    }

    [Fact]
    public void Centered_FallsBackLeftOfStartWhenZoneOccupied()
    {
        var geo = new FakeTaskbarGeometry
        {
            Taskbar = new TaskbarRect(0, 1040, 1920, 40),
            StartButton = new TaskbarRect(800, 1040, 120, 40),
            TrayArea = new TaskbarRect(1700, 1040, 220, 40),
            Children = new List<(int, int)> { (1500, 1700) },
        };

        var pos = LeftLayout(geo).Compute(160);

        Assert.Equal(800 - 160 - 8, pos.X);
    }

    [Fact]
    public void Width_ClampsToThirdOfTaskbar()
    {
        var geo = new FakeTaskbarGeometry
        {
            Taskbar = new TaskbarRect(0, 1040, 600, 40),
            Children = new List<(int, int)>(),
        };

        var pos = LeftLayout(geo).Compute(500);

        Assert.Equal(Math.Min(600 / 3, 280), pos.Width);
    }

    [Fact]
    public void FullscreenApp_HidesOverlay()
    {
        var geo = new FakeTaskbarGeometry
        {
            ForegroundRect = new Rectangle(0, 0, 1920, 1080),
            ScreenBounds = new Rectangle(0, 0, 1920, 1080),
            ForegroundClass = "Chrome_WidgetWin_1",
        };

        var pos = LeftLayout(geo).Compute(160);

        Assert.True(pos.Fullscreen);
    }

    [Fact]
    public void DesktopWindow_NeverCountsAsFullscreen()
    {
        foreach (var cls in new[] { "Progman", "WorkerW" })
        {
            var geo = new FakeTaskbarGeometry
            {
                ForegroundRect = new Rectangle(0, 0, 1920, 1080),
                ScreenBounds = new Rectangle(0, 0, 1920, 1080),
                ForegroundClass = cls,
            };

            Assert.False(LeftLayout(geo).Compute(160).Fullscreen);
        }
    }

    [Fact]
    public void IsSystemWindow_FiltersKnownClasses()
    {
        Assert.True(TaskbarLayout.IsSystemWindow("Start", 100));
        Assert.True(TaskbarLayout.IsSystemWindow("TrayNotifyWnd", 100));
        Assert.True(TaskbarLayout.IsSystemWindow("Windows.UI.Core.CoreWindow", 100));
        Assert.True(TaskbarLayout.IsSystemWindow("Whatever", 10));
        Assert.True(TaskbarLayout.IsSystemWindow("Whatever", 900));
        Assert.False(TaskbarLayout.IsSystemWindow("Chrome_WidgetWin_1", 200));
    }
}
