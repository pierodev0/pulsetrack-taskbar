# PulseTrack.Taskbar

**Cronómetro y timer con alarma para Windows** — un solo exe (.NET 9 WinForms),
sin instalador. Tres capacidades: un stopwatch que puede filtrar por la app que
tenés al frente, un timer que suena al llegar a 0, y tres superficies (ventana,
taskbar, picture-in-picture) para verlo todo el tiempo.

## Requisitos

- Windows 10 1809+ / 11
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
  (el runtime común no sirve, tiene que ser el Desktop)

## Primeros pasos

1. **Corré** `PulseTrack.Taskbar.exe` (o `dotnet run -c Release`) → arranca en
   **modo Normal**, con el icono en la bandeja.
2. **▶ Start** → corre como stopwatch libre. *Lo ves en la ventana y en el
   tooltip del tray.*
3. *Opcional:* **Select app...** → elegí el proceso → desde ahí solo acumula
   cuando esa app está al frente. `— Any app —` vuelve al modo libre y la app
   elegida **se mantiene** después de `⏹ Stop`.
4. **Vueltas:** `🏁 Lap` las lista en la ventana; `⏹ Stop` cierra la sesión.
5. **Modo:** el pie `View: [Taskbar] [PiP]` o `Tray → Mode` — la elección se
   persiste (ver tabla abajo).
6. **Salir:** la **X solo oculta** a la bandeja (todo sigue corriendo); para
   cerrar de verdad, `Tray → Exit`.

## Timer (cuenta regresiva)

La ventana **Normal** tiene dos tabs: **Stopwatch** y **Timer**.

- **Elegí la duración** en la tab Timer: presets `5 min` / `10 min` / `25 min` /
  `1 h`, los campos `Minutes` / `Seconds`, o `Tray → Timer → Custom...`.
- **Elegir solo carga el timer** (se muestra `Timer 2:00 — ready`) — no lo
  arranca.

| ¿Desde dónde lo arranco? | Acción |
|---|---|
| Tab Timer de Normal | botón `Start` |
| PiP (con foco Timer) | `▶` |
| Overlay de la taskbar | click izquierdo |
| Menú del tray | `Timer → ▶ Start` |

- **Al llegar a 0 suena la alarma**: sonido del sistema + notificación en la
  bandeja, **una sola vez**. El timer queda en `0:00 — time's up!` hasta
  reiniciarlo o cancelarlo.
- `⏸ Pause` / `▶ Resume` pausan y reanudan; `✕ Cancel` (o `⏹` en el PiP con
  foco Timer) lo limpia.

### Foco Stopwatch / Timer

La tab seleccionada en Normal define el **foco global** (persistido en
`settings.json`) — gobierna los tres modos:

| Foco | Qué manda | Efecto |
|---|---|---|
| **Stopwatch** (default) | el cronómetro | el timer aparece como dato secundario en el PiP y en el overlay |
| **Timer** | el countdown | el PiP muestra el reloj grande con el timer (`⏹` lo cancela, `🏁` queda deshabilitado) y el overlay muestra **solo** el timer; el estado del cronómetro sigue en el tooltip del tray |

Los botones de cada modo controlan lo que está en foco.

## Modos de UI

Tres modos **exclusivos** (uno a la vez), cambiados desde `Tray → Mode` o desde
el pie de la ventana Normal. Cada uno recuerda su posición (la Normal también su
tamaño) y el próximo arranque respeta el último usado.

| Modo | Qué es | Cómo salir |
|---|---|---|
| **Normal** | Ventana estándar con tabs **Stopwatch** \| **Timer**: reloj grande, botones, lista de laps, controles del timer y el pie `View:`. **Es el modo por defecto.** | Botones del pie o `Tray → Mode` |
| **Taskbar** | Overlay transparente sobre la barra de tareas. Click izquierdo = start/pause del elemento en foco, derecho = menú del tray. Se oculta solo con apps en pantalla completa. | `Tray → Mode` |
| **Picture in Picture** | Mini ventana flotante siempre visible (`TopMost`), arrastrable. Botones `⏸` `🏁` `⏹` `⤢` controlan lo que está en foco. | `⤢` o `✕` vuelven a Normal, o `Tray → Mode` |

## Argumentos de línea de comandos

| Argumento | Qué hace |
|---|---|
| *(sin args)* | UI real, en el modo persistido (default `normal`). |
| `--mode normal\|taskbar\|pip` | Fuerza el modo **solo para esa corrida**, sin tocar `settings.json`. Útil para el autostart o para probar. |
| `--probe` | CLI diagnóstico 10s: loguea foreground 1 muestra/s + lista ventanas abiertas. No abre UI. |
| `--log <Proceso>` | Igual que `--probe` pero marca `match=True/False` contra ese `ProcessName` y resume `matched X/10`. Ej: `--log chrome`. Ejercita el `ForegroundTimer` real e informa `timer accumulated Xs`. |

`--probe` y `--log` escriben `%AppData%/PulseTrackTaskbar/probe.log` (además de
consola).

## Comandos de desarrollo (desde la raíz del repo)

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
- `settings.json` — modo, foco Stopwatch/Timer, fuente, colores, posiciones de
  cada superficie, la última app elegida y la última duración de timer.
- `sessions.db` — SQLite con las tablas `app_sessions` + `time_blocks`.
  `app_name` es **nullable**: `NULL` significa que esa sesión fue un stopwatch
  libre, sin app elegida. Crash-recovery cierra las sesiones `active` a
  `closed` al arrancar.
- `probe.log` — solo si corrés `--probe` o `--log`.

## Arquitectura

Cuatro capas dentro del mismo proyecto. Regla: `Core` no referencia WinForms ni
SQLite; `App` tampoco referencia WinForms.

```
Core/    dominio puro + seams (AppMode, FocusMode, TimerViewState, CountdownTimer, IAlarm, ...)
App/     lógica sin WinForms (TimerViewStateFactory, SurfaceHost, TimerCommands, SessionCoordinator, ...)
Infra/   implementaciones (Database, ChannelSessionStore, FileConfigStore, FileLogger, SystemAlarm, Win32/)
UI/      WinForms tonto (NormalForm con tabs, TaskbarOverlayForm, PipForm, TrayIconPresenter, diálogos)
```

Las tres superficies implementan `ITimerSurface` y `SurfaceHost` decide cuál se
ve; el estado se arma **una sola vez** en `TimerViewStateFactory` — ningún form
formatea tiempos. El timer tiene scheduler propio (corre siempre, ignorando el
filtro de foreground) y su alarma suena una sola vez. La app elegida es un
**filtro opcional**: las sesiones se guardan por `SessionId`, no por app.

**Las reglas de capas, los invariantes (scars) y el flujo de contribución
completo viven en [`AGENTS.md`](./AGENTS.md).**

## Tests

```powershell
dotnet test PulseTrack.Taskbar.Tests -c Release
```

199 tests xUnit — la suite es la **spec viva**: los nombres de los tests
describen el comportamiento esperado. Cubre stopwatch, timer (stage, alarma,
foco), coordinador y stores, comandos, las tres superficies con su layout, config
con round-trip y el overlay. Los fakes viven en archivos propios o junto a cada
test (`FakeSessionRepository.cs`, `ManualTickScheduler`, `FakeClock`, ...).

## Archivos

| Archivo | Qué es |
|---|---|
| `Program.cs` | Composition root: arranque, `AppModeParser`, `TimerAppContext` (tray + DB + flush 60s + marshalling UI + render dual stopwatch/timer + alarma) |
| `Core/TimerViewState.cs` | Estado de render que consumen las tres superficies (incluye `Countdown` y `Focus`) |
| `Core/CountdownTimer.cs` | Cuenta regresiva pura: `Set` (stage) / `Start` / `Pause` / `Cancel`, `Expired` una sola vez, scheduler propio |
| `Core/FocusMode.cs` | Enum `Stopwatch` / `Timer` — el foco global de la UI |
| `Core/ITimerSurface.cs` | Seam de superficie (`Mode` / `Render` / `SetVisible`) |
| `Core/AppSelection.cs` | Resultado del picker: `Confirmed` + `AppName` (null = sin app) |
| `Core/IAlarm.cs` + `Infra/SystemAlarm.cs` | Seam e implementación de la alarma (`SystemSounds.Exclamation`) |
| `App/TimerViewStateFactory.cs` | `TimerTick` + app + countdown + foco → `TimerViewState` (puro) |
| `App/SurfaceHost.cs` | Modo activo + fan-out + cache del último estado |
| `App/TimerCommands.cs` | Pick / toggle / lap / stop + countdown (`Stage`/`Start`/`Toggle`/`Cancel`) + foco (`SetFocus`, `Toggle/StopPrimary`), compartido por tray y las 3 superficies |
| `App/WindowPlacement.cs` | Restaurar y guardar bounds por modo (puro) |
| `AppConfig.cs` | Config JSON (modo, foco, fuente, colores, posiciones, última app, última duración de timer) |
| `ForegroundTimer.cs` | Timer manual (tick 500ms, laps); corre libre si `SelectedApp` es null. `IForegroundSource` + `ITickScheduler` inyectables |
| `WindowWatcher.cs` | Detección Win32 (`GetForegroundWindow`, `EnumWindows`); compara por `ProcessName` |
| `UI/NormalForm.cs` | Ventana normal con tabs **Stopwatch** \| **Timer**: reloj, laps, controles y stageo del timer |
| `TaskbarOverlayForm.cs` | Overlay transparente en taskbar, anclaje a `Shell_TrayWnd`; render según foco |
| `UI/PipForm.cs` | Mini ventana flotante arrastrable; render y rutas de botones según foco |
| `UI/TrayIconPresenter.cs` | NotifyIcon, menú, submenú `Mode`, submenú `Timer` (presets/Custom/pause/cancel), auto-start |
| `UI/SettingsForm.cs` | Diálogo de fuente y colores del overlay |
| `UI/TimerPickerForm.cs` | Diálogo Custom de duración (mm/ss → `OK` stagea el timer) |
| `AppPickerForm.cs` | Diálogo de selección de app (incluye la opción sin app) |
| `Database.cs` | SQLite propio (`Microsoft.Data.Sqlite`) |
| `ProbeCli.cs` | CLI `--probe` / `--log` |

## Agradecimientos

https://github.com/yaffalhakim1/nowplaying-windows
