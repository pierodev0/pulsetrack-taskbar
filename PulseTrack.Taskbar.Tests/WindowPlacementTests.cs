using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class WindowPlacementTests
{
    private static readonly Rectangle _area = new(0, 0, 1920, 1080);
    private static readonly Size _pipSize = new(300, 88);
    private static readonly Size _normalSize = new(400, 300);

    [Fact]
    public void Pip_WithoutSavedPosition_GoesBottomRight()
    {
        var point = WindowPlacement.Restore(new AppConfig(), AppMode.Pip, _pipSize, _area);

        Assert.Equal(new Point(1920 - 300 - 24, 1080 - 88 - 24), point);
    }

    [Fact]
    public void Normal_WithoutSavedPosition_IsCentered()
    {
        var point = WindowPlacement.Restore(new AppConfig(), AppMode.Normal, _normalSize, _area);

        Assert.Equal(new Point((1920 - 400) / 2, (1080 - 300) / 2), point);
    }

    [Fact]
    public void SavedPosition_IsRestored()
    {
        var config = new AppConfig { PipX = 300, PipY = 200 };

        var point = WindowPlacement.Restore(config, AppMode.Pip, _pipSize, _area);

        Assert.Equal(new Point(300, 200), point);
    }

    [Fact]
    public void OffScreenSavedPosition_FallsBackToDefault()
    {
        var config = new AppConfig { PipX = 9000, PipY = 9000 };
        var fallback = WindowPlacement.DefaultFor(AppMode.Pip, _pipSize, _area);

        var point = WindowPlacement.Restore(config, AppMode.Pip, _pipSize, _area);

        Assert.Equal(fallback, point);
    }

    [Fact]
    public void NegativeSavedPosition_FallsBackToDefault()
    {
        var config = new AppConfig { PipX = -1, PipY = -1 };
        var fallback = WindowPlacement.DefaultFor(AppMode.Pip, _pipSize, _area);

        Assert.Equal(fallback, WindowPlacement.Restore(config, AppMode.Pip, _pipSize, _area));
    }

    [Fact]
    public void PartiallyVisiblePosition_IsStillRestored()
    {
        var config = new AppConfig { PipX = 1900, PipY = 1070 };

        var point = WindowPlacement.Restore(config, AppMode.Pip, _pipSize, _area);

        Assert.Equal(new Point(1900, 1070), point);
    }

    [Fact]
    public void SaveLocation_TargetsTheGivenMode()
    {
        var config = new AppConfig();

        WindowPlacement.SaveLocation(config, AppMode.Pip, new Point(1, 2));
        WindowPlacement.SaveLocation(config, AppMode.Normal, new Point(3, 4));

        Assert.Equal(1, config.PipX);
        Assert.Equal(2, config.PipY);
        Assert.Equal(3, config.NormalX);
        Assert.Equal(4, config.NormalY);
    }

    [Fact]
    public void ClientSize_RoundTrips()
    {
        var config = new AppConfig();

        WindowPlacement.SaveClientSize(config, new Size(500, 400));

        Assert.Equal(new Size(500, 400), WindowPlacement.RestoreClientSize(config, _normalSize));
    }

    [Fact]
    public void ClientSize_NotSaved_FallsBack()
    {
        Assert.Equal(_normalSize, WindowPlacement.RestoreClientSize(new AppConfig(), _normalSize));
    }
}
