# PulseTrack.Taskbar

Cronómetro de app en primer plano que vive en la taskbar de Windows.
C# WinForms (.NET 9), sin instalador: un solo exe.

Elegís una app, apretás Start y el tiempo corre solo mientras esa app esté en
primer plano. El tiempo se ve directo sobre la taskbar. Click izquierdo sobre
el overlay = pause/resume.

## Requisitos

- Windows 10 1809+ / 11
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
  (el runtime común no sirve, tiene que ser el Desktop)

## Uso

1. Corré `PulseTrack.Taskbar.exe` (o `dotnet run -c Release`).
2. Tray icon → `Select app...` → elegí el proceso → OK.
3. `▶ Start`. El overlay muestra `⏱ 00:00:00`; pausado muestra `⏸`.
4. `🏁 Lap` para marcar vueltas, `⏹ Stop` para cerrar la sesión.

Sin app seleccionada el overlay dice `Choose app`.

## Comandos (todos desde la raíz del repo)

```powershell
dotnet build PulseTrack.Taskbar.csproj -c Release
dotnet test PulseTrack.Taskbar.Tests
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --probe
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --log chrome
dotnet publish PulseTrack.Taskbar.csproj -c Release -o publish-single /p:PublishSingleFile=true --no-self-contained
```

## Modos

- **Sin args** → UI real: tray icon + overlay en taskbar + diálogo de
  selección de app. Click izq en overlay = start/pause.
- `--probe` → CLI diagnóstico 10s: loguea foreground 1 muestra/s + lista
  ventanas abiertas. No abre UI.
- `--log <Proceso>` → igual que `--probe` pero marca `match=True/False`
  contra ese `ProcessName` y resume `matched X/10`. Ej: `--log chrome`.
  Ejercita el `ForegroundTimer` real e informa `timer accumulated Xs`.

Ambos modos escriben `%AppData%/PulseTrackTaskbar/probe.log` (además de
consola).

## Logs

- `log.txt` → `%AppData%/PulseTrackTaskbar/log.txt`, formato
  `[fecha] [scope] mensaje`. Scopes: `Overlay`, `Timer`, `App`, `Watcher`,
  `Config`.
- Config → `%AppData%/PulseTrackTaskbar/settings.json` (fuente, colores,
  última app).
- DB → `%AppData%/reloj-svelte/sessions.db` (tablas `app_sessions` +
  `time_blocks` con `source='manual'`; crash-recovery cierra `active` a
  `closed` al arrancar).

## Tests

```powershell
dotnet test PulseTrack.Taskbar.Tests
```

9 tests xUnit sobre `ForegroundTimer` con `FakeForegroundSource` +
`ManualTickScheduler`: acumula solo en foreground, case-insensitive,
pause/resume, stop, laps que suman al elapsed, no duplica ticks en
resume, eventos emitidos.

## Archivos

| Archivo | Qué es |
|---|---|
| `Program.cs` | `AppContext`: tray + timer + overlay + flush 60s + auto-start |
| `ForegroundTimer.cs` | Timer manual (tick 500ms, laps); `IForegroundSource` + `ITickScheduler` inyectables |
| `WindowWatcher.cs` | Detección Win32 (`GetForegroundWindow`, `EnumWindows`); compara por `ProcessName` |
| `TaskbarOverlayForm.cs` | Overlay transparente en taskbar (`SetTimer()`), anclaje a `Shell_TrayWnd` |
| `Database.cs` | SQLite mínimo (`Microsoft.Data.Sqlite`) |
| `AppPickerForm.cs` | Diálogo de selección de app |
| `OverlayConfig.cs` | Config JSON + logging |
| `ProbeCli.cs` | CLI `--probe` / `--log` |

## Agradecimientos

https://github.com/yaffalhakim1/nowplaying-windows
