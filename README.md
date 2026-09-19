# PulseTrack.Taskbar

Cronómetro para Windows. C# WinForms (.NET 9), sin instalador: un solo exe.

Funciona como un stopwatch normal: apretás Start y listo. Si además elegís una
app, el tiempo corre **solo mientras esa app esté en primer plano**. Podés verlo
en una ventana normal, sobre la taskbar o en una mini ventana flotante
(picture-in-picture).

## Requisitos

- Windows 10 1809+ / 11
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
  (el runtime común no sirve, tiene que ser el Desktop)

## Uso

1. Corré `PulseTrack.Taskbar.exe` (o `dotnet run -c Release`).
   Arranca en **modo Normal**, con la ventana del cronómetro.
2. `▶ Start` y listo: corre como stopwatch libre.
3. Opcional: `Select app...` → elegí el proceso → OK. Desde ahí el cronómetro
   solo acumula mientras esa app esté al frente. La primera opción de la lista
   (`— Any app —`) vuelve al modo libre.
4. `🏁 Lap` para marcar vueltas (quedan listadas), `⏹ Stop` para cerrar la
   sesión. La app elegida **se mantiene** después de Stop, así podés volver a
   arrancar sin elegirla de nuevo.
5. Cerrá la ventana con la X y la app sigue viva en la bandeja.

## Modos de UI

Tres modos **exclusivos** (uno a la vez). Se cambian desde
`Tray icon → Mode` y la elección se persiste en `settings.json`; el próximo
arranque respeta el último modo usado.

| Modo | Qué es | Cómo salir |
|---|---|---|
| **Normal** | Ventana estándar: reloj grande, botones, lista de laps y un pie `View: [Taskbar] [PiP]` para saltar a los otros modos. **Es el modo por defecto.** | Botones `Taskbar` / `PiP` del pie, o `Tray → Mode` |
| **Taskbar** | El overlay transparente sobre la barra de tareas. Click izquierdo = start/pause, derecho = menú del tray. Se oculta solo si hay una app en pantalla completa. | `Tray → Mode` |
| **Picture in Picture** | Mini ventana flotante siempre visible (`TopMost`), arrastrable. Botones `⏸` `🏁` `⏹` `⤢`. | `⤢` o `✕` vuelven a Normal, o `Tray → Mode` |

Cada modo recuerda su posición (y la ventana Normal también su tamaño). La `X` de la
ventana Normal la oculta a la bandeja sin cerrar la app (el cronómetro sigue
corriendo); para salir de verdad, `Tray → Exit`.

## Argumentos de línea de comandos

| Argumento | Qué hace |
|---|---|
| *(sin args)* | UI real, en el modo persistido (default `normal`). |
| `--mode normal\|taskbar\|pip` | Fuerza el modo **solo para esa corrida**, sin tocar `settings.json`. Útil para el autostart o para probar. |
| `--probe` | CLI diagnóstico 10s: loguea foreground 1 muestra/s + lista ventanas abiertas. No abre UI. |
| `--log <Proceso>` | Igual que `--probe` pero marca `match=True/False` contra ese `ProcessName` y resume `matched X/10`. Ej: `--log chrome`. Ejercita el `ForegroundTimer` real e informa `timer accumulated Xs`. |

`--probe` y `--log` escriben `%AppData%/PulseTrackTaskbar/probe.log` (además de
consola).

## Comandos (todos desde la raíz del repo)

```powershell
dotnet build PulseTrack.Taskbar.csproj -c Release
dotnet test PulseTrack.Taskbar.Tests -c Release
dotnet run --project PulseTrack.Taskbar.csproj -c Release
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --mode pip
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --probe
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --log chrome
dotnet publish PulseTrack.Taskbar.csproj -c Release -o publish-single /p:PublishSingleFile=true --no-self-contained
```

## Logs y datos

Todo vive en `%AppData%/PulseTrackTaskbar/`:

- `log.txt` — formato `[fecha] [scope] mensaje`. Scopes: `Overlay`, `Timer`,
  `App`, `Watcher`, `Config`, `Host`, `Normal`, `Pip`, `Tray`.
- `settings.json` — modo, fuente, colores, posiciones de cada superficie y la
  última app elegida.
- `sessions.db` — SQLite con las tablas `app_sessions` + `time_blocks`.
  `app_name` es **nullable**: `NULL` significa que esa sesión fue un stopwatch
  libre, sin app elegida. Crash-recovery cierra las sesiones `active` a
  `closed` al arrancar.
- `probe.log` — solo si corrés `--probe` o `--log`.

## Arquitectura

Cuatro capas dentro del mismo proyecto. Regla: `Core` no referencia WinForms ni
SQLite; `App` no referencia WinForms.

```
Core/    dominio puro + seams (AppMode, TimerViewState, ITimerSurface, AppSelection, IConfigStore, IClock, ...)
App/     lógica sin WinForms (TimerViewStateFactory, SurfaceHost, TimerCommands, WindowPlacement, SessionCoordinator)
Infra/   implementaciones (Database, ChannelSessionStore, FileConfigStore, FileLogger, Win32/)
UI/      WinForms tonto (NormalForm, TaskbarOverlayForm, PipForm, TrayIconPresenter, SettingsForm, AppPickerForm)
```

Las tres superficies implementan `ITimerSurface` (`Render` / `SetVisible`) y
`SurfaceHost` decide cuál se ve y le hace fan-out del estado. El estado se arma
**una sola vez** en `TimerViewStateFactory` — ningún form formatea texto por su
cuenta.

La app elegida es un **filtro opcional**, no un requisito: `SelectedApp == null`
hace que el timer acumule siempre. Por eso `StopAsync` se guarda por `SessionId`
y no por `SelectedApp`, y por eso el picker devuelve `AppSelection` en vez de un
`string?` (donde `null` sería ambiguo entre "cancelé" y "sin app").

## Tests

```powershell
dotnet test PulseTrack.Taskbar.Tests -c Release
```

142 tests xUnit. Fakes en archivos propios (`FakeSessionRepository.cs`) o junto
al test (`FakeForegroundSource`, `ManualTickScheduler`, `FakeSessionStore`,
`FakeClock`, `FakeLogger`, `FakeTaskbarGeometry`, `FakeSurface`).

Unidades cubiertas: `ForegroundTimer` (foreground, case-insensitive,
pause/resume, stop, laps, y el modo libre sin app), `SessionCoordinator`
(start/lap/stop/flush), `ChannelSessionStore` (FIFO, drenado en dispose),
`TimerViewStateFactory` (glifos, lap text, flags), `SurfaceHost` (modos
exclusivos, estado cacheado al cambiar de modo, tolerancia a fallos de una
superficie), `TimerCommands` (arranque sin app, persistencia de `LastApp`),
`WindowPlacement` (defaults, posiciones fuera de pantalla, round-trip), config
(`FileConfigStore` en temp dir, `Update` preserva lo que no toca) y
`TaskbarLayout`.

## Archivos

| Archivo | Qué es |
|---|---|
| `Program.cs` | Composition root: arranque, `AppModeParser`, `TimerAppContext` (tray + DB + flush 60s + marshalling UI) |
| `Core/TimerViewState.cs` | Estado de render que consumen las tres superficies |
| `Core/ITimerSurface.cs` | Seam de superficie (`Mode` / `Render` / `SetVisible`) |
| `Core/AppSelection.cs` | Resultado del picker: `Confirmed` + `AppName` (null = sin app) |
| `App/TimerViewStateFactory.cs` | `TimerTick` + app → `TimerViewState` (puro) |
| `App/SurfaceHost.cs` | Modo activo + fan-out + cache del último estado |
| `App/TimerCommands.cs` | Pick / toggle / lap / stop, compartido por tray y las 3 superficies |
| `App/WindowPlacement.cs` | Restaurar y guardar bounds por modo (puro) |
| `AppConfig.cs` | Config JSON (modo, fuente, colores, posiciones, última app) |
| `ForegroundTimer.cs` | Timer manual (tick 500ms, laps); corre libre si `SelectedApp` es null. `IForegroundSource` + `ITickScheduler` inyectables |
| `WindowWatcher.cs` | Detección Win32 (`GetForegroundWindow`, `EnumWindows`); compara por `ProcessName` |
| `UI/NormalForm.cs` | Ventana normal: reloj, botones, lista de laps |
| `TaskbarOverlayForm.cs` | Overlay transparente en taskbar, anclaje a `Shell_TrayWnd` |
| `UI/PipForm.cs` | Mini ventana flotante arrastrable |
| `UI/TrayIconPresenter.cs` | NotifyIcon, menú, submenú `Mode`, auto-start |
| `UI/SettingsForm.cs` | Diálogo de fuente y colores del overlay |
| `AppPickerForm.cs` | Diálogo de selección de app (incluye la opción sin app) |
| `Database.cs` | SQLite propio (`Microsoft.Data.Sqlite`) |
| `ProbeCli.cs` | CLI `--probe` / `--log` |

## Agradecimientos

https://github.com/yaffalhakim1/nowplaying-windows
