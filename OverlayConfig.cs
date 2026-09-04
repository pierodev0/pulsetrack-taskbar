namespace PulseTrack.Taskbar;

public class OverlayConfig
{
    public string FontFamily { get; set; } = "Segoe UI";
    public float FontSize { get; set; } = 12f;
    public int FontStyle { get; set; } = 0;
    public int TextColorArgb { get; set; } = unchecked((int)0xF0FFFFFF);
    public int TextAlpha { get; set; } = 255;
    public bool ShowBackground { get; set; } = true;
    public int BackgroundColorArgb { get; set; } = unchecked((int)0xB41A1A2E);
    public int BackgroundAlpha { get; set; } = 180;
    public int TransparencyKeyArgb { get; set; } = unchecked((int)0xFF000000);
    public string LastApp { get; set; } = "";
}
