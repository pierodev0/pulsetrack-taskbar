namespace PulseTrack.Taskbar;

public sealed class SettingsViewModel
{
    public const float MinFontSize = 6f;
    public const float MaxFontSize = 32f;

    private readonly IConfigStore _store;
    private OverlayConfig _original = new();

    private float _fontSize = 12f;

    public string FontFamily { get; set; } = "Segoe UI";

    public float FontSize
    {
        get => _fontSize;
        set => _fontSize = Math.Clamp(value, MinFontSize, MaxFontSize);
    }

    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public Color TextColor { get; set; } = Color.White;
    public bool ShowBackground { get; set; } = true;
    public Color BackgroundColor { get; set; } = Color.FromArgb(26, 26, 46);

    public string PreviewText => $"⏱ 00:12:34 — {FontFamily} {FontSize:0.#}pt";

    public SettingsViewModel(IConfigStore store)
    {
        _store = store;
    }

    public void Load()
    {
        var config = _store.Load();
        _original = config;
        FontFamily = config.FontFamily;
        FontSize = config.FontSize;
        Bold = (config.FontStyle & (int)FontStyle.Bold) != 0;
        Italic = (config.FontStyle & (int)FontStyle.Italic) != 0;
        TextColor = Color.FromArgb(config.TextColorArgb);
        ShowBackground = config.ShowBackground;
        BackgroundColor = Color.FromArgb(config.BackgroundColorArgb);
    }

    public OverlayConfig Apply()
    {
        var style = FontStyle.Regular;
        if (Bold) style |= FontStyle.Bold;
        if (Italic) style |= FontStyle.Italic;

        var config = new OverlayConfig
        {
            FontFamily = FontFamily,
            FontSize = FontSize,
            FontStyle = (int)style,
            TextColorArgb = TextColor.ToArgb(),
            TextAlpha = 255,
            ShowBackground = ShowBackground,
            BackgroundColorArgb = BackgroundColor.ToArgb(),
            BackgroundAlpha = _original.BackgroundAlpha,
            TransparencyKeyArgb = _original.TransparencyKeyArgb,
            LastApp = _original.LastApp,
        };
        _original = config;
        _store.Save(config);
        return config;
    }
}
