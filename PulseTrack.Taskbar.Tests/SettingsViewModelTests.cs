using PulseTrack.Taskbar;

namespace PulseTrack.Taskbar.Tests;

public sealed class InMemoryConfigStore : IConfigStore
{
    public AppConfig Config { get; set; } = new();
    public int SaveCount { get; private set; }
    public string ProbeLogPath => Path.Combine(Path.GetTempPath(), "probe.log");
    public AppConfig Load() => Config;
    public void Save(AppConfig config) { Config = config; SaveCount++; }
    public void Update(Action<AppConfig> mutate)
    {
        var config = Load();
        mutate(config);
        Save(config);
    }
}

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Load_CopiesValuesFromStore()
    {
        var store = new InMemoryConfigStore
        {
            Config = new AppConfig { FontFamily = "Consolas", FontSize = 14f, ShowBackground = false }
        };
        var vm = new SettingsViewModel(store);

        vm.Load();

        Assert.Equal("Consolas", vm.FontFamily);
        Assert.Equal(14f, vm.FontSize);
        Assert.False(vm.ShowBackground);
    }

    [Fact]
    public void Apply_SavesAndReturnsConfig()
    {
        var store = new InMemoryConfigStore();
        var vm = new SettingsViewModel(store);
        vm.Load();
        vm.FontFamily = "Consolas";
        vm.FontSize = 14f;
        vm.Bold = true;
        vm.TextColor = Color.Red;
        vm.ShowBackground = false;

        var applied = vm.Apply();

        Assert.Equal(1, store.SaveCount);
        Assert.Equal("Consolas", applied.FontFamily);
        Assert.Equal(14f, applied.FontSize);
        Assert.True((applied.FontStyle & (int)FontStyle.Bold) != 0);
        Assert.Equal(Color.Red.ToArgb(), applied.TextColorArgb);
        Assert.False(applied.ShowBackground);
    }

    [Fact]
    public void Apply_CombinesBoldAndItalic()
    {
        var store = new InMemoryConfigStore();
        var vm = new SettingsViewModel(store);
        vm.Load();
        vm.Bold = true;
        vm.Italic = true;

        var applied = vm.Apply();

        Assert.Equal((int)(FontStyle.Bold | FontStyle.Italic), applied.FontStyle);
    }

    [Fact]
    public void Apply_PreservesWindowPlacementsAndMode()
    {
        var store = new InMemoryConfigStore
        {
            Config = new AppConfig
            {
                Mode = AppMode.Pip,
                PipX = 300,
                PipY = 200,
                NormalX = 40,
                NormalY = 60,
                NormalWidth = 420,
                NormalHeight = 360,
                LastApp = "Code",
            }
        };
        var vm = new SettingsViewModel(store);
        vm.Load();
        vm.FontFamily = "Consolas";

        vm.Apply();

        Assert.Equal(AppMode.Pip, store.Config.Mode);
        Assert.Equal(300, store.Config.PipX);
        Assert.Equal(200, store.Config.PipY);
        Assert.Equal(40, store.Config.NormalX);
        Assert.Equal(60, store.Config.NormalY);
        Assert.Equal(420, store.Config.NormalWidth);
        Assert.Equal(360, store.Config.NormalHeight);
        Assert.Equal("Code", store.Config.LastApp);
    }

    [Fact]
    public void Apply_PreservesOverlayTransparencySettings()
    {
        var store = new InMemoryConfigStore
        {
            Config = new AppConfig { BackgroundAlpha = 90, TransparencyKeyArgb = unchecked((int)0xFF112233) }
        };
        var vm = new SettingsViewModel(store);
        vm.Load();

        vm.Apply();

        Assert.Equal(90, store.Config.BackgroundAlpha);
        Assert.Equal(unchecked((int)0xFF112233), store.Config.TransparencyKeyArgb);
    }

    [Fact]
    public void FontSize_ClampsToValidRange()
    {
        var store = new InMemoryConfigStore();
        var vm = new SettingsViewModel(store);
        vm.Load();

        vm.FontSize = 100f;
        Assert.Equal(32f, vm.FontSize);

        vm.FontSize = 0f;
        Assert.Equal(6f, vm.FontSize);
    }

    [Fact]
    public void PreviewText_ReflectsFontSettings()
    {
        var store = new InMemoryConfigStore();
        var vm = new SettingsViewModel(store);
        vm.Load();
        vm.FontFamily = "Consolas";
        vm.FontSize = 14f;

        Assert.Contains("Consolas", vm.PreviewText);
        Assert.Contains("14", vm.PreviewText);
    }
}
