namespace PulseTrack.Taskbar;

public class AppConfig
{
    public AppMode Mode { get; set; } = AppMode.Normal;

    public string FontFamily { get; set; } = "Segoe UI";
    public float FontSize { get; set; } = 12f;
    public int FontStyle { get; set; } = 0;
    public int TextColorArgb { get; set; } = unchecked((int)0xF0FFFFFF);
    public int TextAlpha { get; set; } = 255;
    public bool ShowBackground { get; set; } = true;
    public int BackgroundColorArgb { get; set; } = unchecked((int)0xB41A1A2E);
    public int BackgroundAlpha { get; set; } = 180;
    public int TransparencyKeyArgb { get; set; } = unchecked((int)0xFF000000);

    public int PipX { get; set; } = -1;
    public int PipY { get; set; } = -1;
    public int NormalX { get; set; } = -1;
    public int NormalY { get; set; } = -1;
    public int NormalWidth { get; set; } = 0;
    public int NormalHeight { get; set; } = 0;

    public string LastApp { get; set; } = "";
}
