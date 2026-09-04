namespace PulseTrack.Taskbar;

public static class ProbeCli
{
    private sealed class NullTickScheduler : ITickScheduler
    {
        public void Start(Action tick) { }
        public void Stop() { }
        public void Dispose() { }
    }

    public static void Run(string[] args)
    {
        var target = args.FirstOrDefault(a => !a.StartsWith("--"));
        var lines = new List<string>();

        void Emit(string msg)
        {
            lines.Add(msg);
            Console.WriteLine(msg);
        }

        Emit($"[probe] {DateTime.Now:yyyy-MM-dd HH:mm:ss} target={(target ?? "(any)")}, 10s, 1 sample/s");

        using var timer = target != null ? new ForegroundTimer(new SystemForegroundSource(), new NullTickScheduler()) : null;
        TimerTick? lastTick = null;
        if (timer != null)
        {
            timer.Start(target!);
            timer.Ticked += t => lastTick = t;
        }

        string? last = null;
        int matched = 0;
        for (int i = 0; i < 10; i++)
        {
            var fg = WindowWatcher.GetForegroundApp();
            var name = fg?.ProcessName ?? "(none)";
            var title = fg?.Title is { Length: > 0 } t ? (t.Length > 60 ? t[..60] + "…" : t) : "";
            bool match = target != null && string.Equals(name, target, StringComparison.OrdinalIgnoreCase);
            if (match) matched++;
            if (name != last || match)
                Emit($"[probe] t={i}s fg={name} match={match} title={title} elapsed={lastTick?.ElapsedSeconds ?? 0:F1}s");
            last = name;
            timer?.OnTick();
            Thread.Sleep(1000);
        }

        if (target != null && timer != null)
        {
            var duration = timer.Stop();
            Emit($"[probe] matched {matched}/10 samples for '{target}', timer accumulated {duration:F1}s");
        }

        Emit("[probe] open windows:");
        foreach (var w in WindowWatcher.ListOpenWindows())
            Emit($"[probe]   {w.ProcessName} — {(w.Title.Length > 80 ? w.Title[..80] + "…" : w.Title)}");

        try
        {
            var path = OverlayConfig.ProbeLogPath;
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllLines(path, lines);
            Console.WriteLine($"[probe] log written to {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[probe] log write failed: {ex.Message}");
        }
    }
}
