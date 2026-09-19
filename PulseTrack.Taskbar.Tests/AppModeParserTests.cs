using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class AppModeParserTests
{
    [Theory]
    [InlineData("normal", AppMode.Normal)]
    [InlineData("taskbar", AppMode.Taskbar)]
    [InlineData("pip", AppMode.Pip)]
    [InlineData("PIP", AppMode.Pip)]
    [InlineData("Taskbar", AppMode.Taskbar)]
    [InlineData("picture-in-picture", AppMode.Pip)]
    public void ParsesKnownModes(string value, AppMode expected)
    {
        Assert.Equal(expected, AppModeParser.From(new[] { "--mode", value }));
    }

    [Fact]
    public void NoArgs_ReturnsNull()
    {
        Assert.Null(AppModeParser.From(Array.Empty<string>()));
    }

    [Fact]
    public void MissingValue_ReturnsNull()
    {
        Assert.Null(AppModeParser.From(new[] { "--mode" }));
    }

    [Fact]
    public void UnknownValue_ReturnsNull()
    {
        Assert.Null(AppModeParser.From(new[] { "--mode", "banana" }));
    }

    [Fact]
    public void IgnoresUnrelatedArgs()
    {
        Assert.Equal(AppMode.Pip, AppModeParser.From(new[] { "--probe", "--mode", "pip" }));
    }

    [Fact]
    public void DefaultMode_IsNormal()
    {
        Assert.Equal(AppMode.Normal, new AppConfig().Mode);
        Assert.Equal(AppMode.Normal, default(AppMode));
    }
}
