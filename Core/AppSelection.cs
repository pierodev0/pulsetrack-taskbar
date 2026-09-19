namespace PulseTrack.Taskbar;

public readonly record struct AppSelection(bool Confirmed, string? AppName)
{
    public static readonly AppSelection Cancelled = new(false, null);

    public static readonly AppSelection AnyApp = new(true, null);

    public static AppSelection Of(string appName) => new(true, appName);
}
