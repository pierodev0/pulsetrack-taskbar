namespace PulseTrack.Taskbar;

public sealed class SettingsViewModel
{
    public const float MinFontSize = 6f;
    public const float MaxFontSize = 32f;

    private readonly IConfigStore _store;

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
        FontFamily = config.FontFamily;
        FontSize = config.FontSize;
        Bold = (config.FontStyle & (int)FontStyle.Bold) != 0;
        Italic = (config.FontStyle & (int)FontStyle.Italic) != 0;
        TextColor = Color.FromArgb(config.TextColorArgb);
        ShowBackground = config.ShowBackground;
        BackgroundColor = Color.FromArgb(config.BackgroundColorArgb);
    }

    public AppConfig Apply()
    {
        var style = FontStyle.Regular;
        if (Bold) style |= FontStyle.Bold;
        if (Italic) style |= FontStyle.Italic;

        AppConfig? updated = null;
        _store.Update(c =>
        {
            c.FontFamily = FontFamily;
            c.FontSize = FontSize;
            c.FontStyle = (int)style;
            c.TextColorArgb = TextColor.ToArgb();
            c.TextAlpha = 255;
            c.ShowBackground = ShowBackground;
            c.BackgroundColorArgb = BackgroundColor.ToArgb();
            updated = c;
        });

        return updated!;
    }
}
