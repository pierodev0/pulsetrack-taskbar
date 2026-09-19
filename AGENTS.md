# PulseTrack.Taskbar — WinForms foreground stopwatch

## Stack

- **.NET 9** WinForms (`net9.0-windows10.0.19041.0`), `OutputType WinExe`, STA
- **Microsoft.Data.Sqlite 9.0.9** (pineado, no flotante) — `%AppData%/reloj-svelte/sessions.db`, `journal_mode=WAL` + `busy_timeout=5000`
- **Win32 P/Invoke centralizado** — todo en `Infra/Win32/NativeMethods.cs` (`SetLastError` donde aplica); `WindowWatcher` y `Win32TaskbarGeometry` consumen de ahí, nada de `DllImport` suelto
- **3 modos exclusivos** de UI (`AppMode`: `Normal` default / `Taskbar` / `Pip`), cada uno una `ITimerSurface`; `SurfaceHost` decide cuál se ve y le hace fan-out del estado. Cambio desde `Tray → Mode`
- **Overlay** en taskbar (`TaskbarOverlayForm.cs`): scroll + fullscreen-hide + Z-bump 100ms; timers pausados mientras está oculto
- **xUnit** — 117 tests en `PulseTrack.Taskbar.Tests/` (net9 windows, referencia al csproj principal, `InternalsVisibleTo` para hooks `internal ...ForTest`)
- Requiere **.NET 9 Desktop Runtime** (no el runtime común)
- **Builds repetibles**: `global.json` fija SDK, `Directory.Build.props` (`Deterministic`, `TreatWarningsAsErrors`, `ContinuousIntegrationBuild`), paquetes pineados

## Setup & dev

Todos los comandos se corren desde la raíz del repo (donde está este AGENTS.md):

```powershell
dotnet build PulseTrack.Taskbar.csproj -c Release
dotnet test PulseTrack.Taskbar.Tests -c Release
dotnet publish PulseTrack.Taskbar.csproj -c Release -o publish-single /p:PublishSingleFile=true --no-self-contained
```

## Estructura (Core / Infra / App / UI)

Mismo proyecto, carpetas por capa. Regla: `Core` no referencia WinForms ni SQLite; `App/` tampoco referencia WinForms (es lógica pura y testeable).

- **`Core/`** — dominio puro + seams: `ISessionRepository` (sync, SQLite), `ISessionStore` (async, `IAsyncDisposable`), `IClock`/`SystemClock`, `IAppLogger`/`NullLogger`, `IConfigStore`, `ITaskbarGeometry` (`TaskbarRect`, `OverlayPosition`), `LapInfo`/`TimerTick`, `IForegroundSource`/`ITickScheduler`, `AppMode`, `TimerViewState`, `ITimerSurface`, `ClockFormat`
- **`Infra/`** — `Database` (implementa `ISessionRepository`), `ChannelSessionStore` (envuelve repo con `Channel<Func<Task>>` FIFO, loop de fondo, drena en `DisposeAsync`), `FileLogger` (mismo `log.txt`/formato), `FileConfigStore` (mismo `settings.json`), `Win32/` (`NativeMethods` + `Win32TaskbarGeometry`)
- **`App/`** — `SessionCoordinator` (`PickAppAsync/LapAsync/StopAsync/FlushAsync` + `ToggleStartPause` sync); `TimerViewStateFactory` (`TimerTick` + app → `TimerViewState`, puro); `SurfaceHost` (modo activo + fan-out + cache del último estado); `TimerCommands` (pick/toggle/lap/stop, lo comparten tray y las 3 superficies); `WindowPlacement` (bounds por modo, puro)
- **`UI/`** — tonta: `NormalForm` / `TaskbarOverlayForm` / `PipForm` (las 3 implementan `ITimerSurface`: `Mode` + `Render(state)` + `SetVisible(bool)`), `TrayIconPresenter` (NotifyIcon + menú + submenú `Mode` + auto-start), `TaskbarLayout` (posición pura, testeable con fake geometry), `SettingsForm`, `AppPickerForm`
- **`Program.cs`** — composition root: `AppModeParser` + `TimerAppContext` (crea todo, arranca `SurfaceHost`, captura el hilo UI para marshalling, flush 60s)
- **`ProbeCli.cs`** — CLI `--probe` / `--log`, usa `FileConfigStore().ProbeLogPath`

## Modos

La app tiene 3 modos de UI **exclusivos**, más 2 modos CLI. Args en `Program.cs:Main`:

- **Sin args** → UI real, en el modo persistido en `AppConfig.Mode` (default `Normal`).
- `--mode normal|taskbar|pip` → fuerza el modo **solo para esa corrida**, sin tocar `settings.json` (`AppModeParser`).
- `--probe` → CLI diagnóstico 10s: loguea foreground 1 muestra/s + lista ventanas abiertas. No abre UI.
- `--log <Proceso>` → igual que `--probe` pero marca `match=True/False` contra ese `ProcessName` y resume `matched X/10`. Ej: `--log chrome`. Ejercita el `ForegroundTimer` real e informa `timer accumulated Xs`.
- `--probe` y `--log` escriben `%AppData%/PulseTrackTaskbar/probe.log` (además de consola) para diagnóstico sin UI.

Los modos de UI se cambian desde `Tray → Mode` y se persisten vía `SurfaceHost.ModeChanged` (solo en cambios reales, así el override de `--mode` no ensucia el config).

- **Normal** (default) → ventana estándar: reloj grande, botones, lista de laps y un pie `View:` con botones `Taskbar` / `PiP` (llaman a `NormalForm.SwitchTo`, que reenvía al `Action<AppMode>` inyectado desde el composition root). Mostrada con `Show()`, ocultada con `Hide()`. El cierre del usuario (X) se cancela y oculta a la bandeja; `NormalForm.AllowClose()` es el flag que deja pasar el cierre real en el shutdown.
- **Taskbar** → overlay anclado a `Shell_TrayWnd`.
- **Pip** → mini ventana `TopMost` arrastrable, 300x88. Grilla de botones: `⏸` pause/resume, `🏁` lap, `⏹` stop, más la `✕` de la esquina que vuelve a `Normal` (`RestoreToNormal` → `_onRestore`). La PiP no tiene un "cerrar" propio: modo y visibilidad son lo mismo.

```powershell
dotnet run --project PulseTrack.Taskbar.csproj -c Release
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --mode pip
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --probe
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --log chrome
```

## Logs

- `IAppLogger.Log(scope, message)` → `%AppData%/PulseTrackTaskbar/log.txt` vía `FileLogger`, formato `[fecha] [scope] mensaje`.
- Scopes usados: `Overlay` (scroll/reposicion), `Timer` (excepciones de tick), `App` (flush, pick, stop, clicks), `Watcher` (errores de enum), `Config` (load/save), `Host` (SurfaceHost), `Normal` (bounds), `Pip` (posición), `Tray` (menú/balloon).
- DB compartida: `%AppData%/reloj-svelte/sessions.db`. Solo tablas `app_sessions` + `time_blocks` con `source='manual'`; crash-recovery cierra `status='active'` a `'closed'` al arrancar.
- Config: `%AppData%/PulseTrackTaskbar/settings.json` vía `FileConfigStore` (modo, fuente, colores, posiciones por superficie, `LastApp`).

## Architecture rules

- **Desacoplar para testear**: `ForegroundTimer` recibe `IForegroundSource` + `ITickScheduler` por ctor; `SessionCoordinator` recibe `(timer, ISessionStore, IClock)`; `TimerCommands` recibe el picker como `Func<string?, Task<string?>>`; `TaskbarLayout` recibe `ITaskbarGeometry`; todo lo Win32/IO entra por ctor con default (`NullLogger`, `Win32TaskbarGeometry`). Tests usan fakes (`FakeForegroundSource`, `ManualTickScheduler`, `FakeSessionStore`/`FakeSessionRepository`, `FakeClock`, `FakeLogger`, `FakeTaskbarGeometry`, `FakeSurface`).
- **Comparar por `ProcessName`**, nunca por título — el título cambia, el tick compara proceso.
- **Todos los handlers de timers con try/catch + `IAppLogger`** — un tick sin proteger cuelga la app sin mensaje.
- **Nada de I/O en hilo UI**: coordinator es async, SQLite solo vía `ChannelSessionStore` en background; `TimerAppContext.Dispose` drena (`StopAsync` + `DisposeAsync` con `.GetAwaiter().GetResult()`, seguro por `ConfigureAwait(false)`).
- **UI solo desde hilo UI**: `TimerAppContext` captura el hilo en el ctor y marshalea con `SynchronizationContext` — **nunca** usar el `InvokeRequired` de un form como ancla (si el form no tiene handle devuelve `false` y te deja tocando controles desde otro hilo). Las superficies igual protegen `ObjectDisposedException` + `InvalidOperationException` como red de seguridad.
- **Z-order**: mantener bump de 100 ms con `SetWindowPos(HWND_TOP, SWP_NOACTIVATE...)` — la taskbar reordena hijos.
- **P/Invoke solo en `Infra/Win32/NativeMethods.cs`** — prohibido `DllImport` en otro archivo.
- **Nueva superficie = nuevo `ITimerSurface`** (`Mode` + `Render(TimerViewState)` + `SetVisible(bool)`) sumado al array que recibe `SurfaceHost` — modo, visibilidad exclusiva, cache del último estado y logging ya resueltos.
- **El estado se arma una sola vez** en `TimerViewStateFactory`; ningún form formatea texto por su cuenta (nada de `FormatHms` local, usar `ClockFormat`).
- **Config con dueño único**: para escribir usar `IConfigStore.Update(c => ...)`, nunca guardar una copia completa en memoria (pisaría campos que otro dueño escribió: fue el bug de las coordenadas del PiP).
- **Proyecto principal excluye tests**: `<Compile Remove="PulseTrack.Taskbar.Tests/**/*.cs" />` en el csproj (el glob del SDK si no compila los tests dentro del WinExe).

## Code conventions

- `namespace PulseTrack.Taskbar;` — file-scoped
- `_camelCase` private fields, `PascalCase` métodos/tipos
- `record` para datos (`WindowInfo`, `LapInfo`, `TimerTick`, `TaskbarRect`, `OverlayPosition`, `TimerViewState`); `interface` para seams (`IForegroundSource`, `ITickScheduler`, `ISessionStore`, `IAppLogger`, `IConfigStore`, `ITaskbarGeometry`, `ITimerSurface`)
- `IDisposable` en timer/scheduler/DB; `IAsyncDisposable` en stores async; `GC.SuppressFinalize(this)`
- Async con `ConfigureAwait(false)` en `Infra/` y en `SessionCoordinator` (para que `Dispose` pueda bloquear sin deadlock); `ConfigureAwait(true)` en handlers UI y en `TimerCommands`, que orquesta desde el hilo UI y toca el timer
- `internal ...ForTest` + `InternalsVisibleTo` para hooks de test, nunca API pública solo-para-test

## Testing

- xUnit en `PulseTrack.Taskbar.Tests/` (117 tests): fakes en archivos propios (`FakeSessionRepository.cs`) o junto al test (`FakeSessionStore`, `FakeClock`, `FakeLogger`, `FakeTaskbarGeometry`, `FakeSurface`)
- Unidades: `ForegroundTimer` (foreground/case/pause/resume/stop/laps/eventos), `SessionCoordinator` async (pick/lap/stop/flush vía `FakeSessionStore`), `ChannelSessionStore` (ids de fondo, FIFO concurrente, drenado en dispose, close flow), `TimerViewStateFactory` (glifos/lap text/flags, reemplaza los tests de `PipViewModel` y `TimerWidget`), `SurfaceHost` (exclusividad de modos, estado cacheado al cambiar de modo, tolerancia a fallos), `TimerCommands` (fallback al picker, persistencia de `LastApp`), `WindowPlacement` (defaults, posiciones fuera de pantalla, round-trip), `AppModeParser`, `NormalForm` y `PipForm` (wiring de botones, filas de laps, layout sin solapamientos), logging/config (`FakeLogger`, `FileConfigStore` en temp dir, `IConfigStore.Update` preserva lo que no toca), layout/widgets (`TaskbarLayout` con `FakeTaskbarGeometry`)
- TDD: RED (test que falla) → GREEN (mínimo para pasar) → REFACTOR (migrar UI sin romper)
- `dotnet test PulseTrack.Taskbar.Tests -c Release` antes de cada commit que toque `pulsetrack-taskbar/`

## Commit & PR conventions

- Format: `type(scope): message` (conventional commits)
- Types: `feat`, `fix`, `perf`, `refactor`, `chore`, `docs`, `test`
- Keep changes focused: one logical change per commit
- Always run `dotnet build -c Release` + `dotnet test` (el proyecto de tests) before committing
