# PulseTrack.Taskbar — WinForms foreground stopwatch

## Stack

- **.NET 9** WinForms (`net9.0-windows10.0.19041.0`), `OutputType WinExe`, STA
- **Microsoft.Data.Sqlite 9.0.9** (pineado, no flotante) — `%AppData%/reloj-svelte/sessions.db`, `journal_mode=WAL` + `busy_timeout=5000`
- **Win32 P/Invoke centralizado** — todo en `Infra/Win32/NativeMethods.cs` (`SetLastError` donde aplica); `WindowWatcher` y `Win32TaskbarGeometry` consumen de ahí, nada de `DllImport` suelto
- **Overlay** en taskbar (`TaskbarOverlayForm.cs` hostea `ITaskbarWidget`; `SetWidget()` + `SetTimer()`; scroll + fullscreen-hide + Z-bump 100ms)
- **xUnit** — 39 tests en `PulseTrack.Taskbar.Tests/` (net9 windows, referencia al csproj principal, `InternalsVisibleTo` para hooks `internal ...ForTest`)
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

Mismo proyecto, carpetas por capa. Regla: `Core` no referencia WinForms ni SQLite.

- **`Core/`** — dominio puro + seams: `ISessionRepository` (sync, SQLite), `ISessionStore` (async, `IAsyncDisposable`), `IClock`/`SystemClock`, `IAppLogger`/`NullLogger`, `IConfigStore`, `ITaskbarGeometry` (`TaskbarRect`, `OverlayPosition`), `LapInfo`/`TimerTick`, `IForegroundSource`/`ITickScheduler`
- **`Infra/`** — `Database` (implementa `ISessionRepository`), `ChannelSessionStore` (envuelve repo con `Channel<Func<Task>>` FIFO, loop de fondo, drena en `DisposeAsync`), `FileLogger` (mismo `log.txt`/formato), `FileConfigStore` (mismo `settings.json`), `Win32/` (`NativeMethods` + `Win32TaskbarGeometry`)
- **`App/`** — `SessionCoordinator`: `PickAppAsync/LapAsync/StopAsync/FlushAsync` + `ToggleStartPause` sync; `ConfigureAwait(false)` en todo await para que `Dispose` pueda bloquear sin deadlock
- **`UI/`** — tonta: `TimerAppContext` (tray + `ChannelSessionStore` + coordinator, handlers async, `Dispose` drena channel), `TaskbarOverlayForm` (host de `ITaskbarWidget`), `TaskbarLayout` (posición pura, testeable con fake geometry), `TimerWidget`, `AppPickerForm`
- **`ProbeCli.cs`** — CLI `--probe` / `--log`, usa `FileConfigStore().ProbeLogPath`

## Modos

La app tiene 3 modos según args en `Program.cs:Main`:

- **Sin args** → UI real: tray icon + overlay en taskbar + `AppPickerForm`. Click izq en overlay = start/pause.
- `--probe` → CLI diagnóstico 10s: loguea foreground 1 muestra/s + lista ventanas abiertas. No abre UI.
- `--log <Proceso>` → igual que `--probe` pero marca `match=True/False` contra ese `ProcessName` y resume `matched X/10`. Ej: `--log chrome`. Ejercita el `ForegroundTimer` real e informa `timer accumulated Xs`.
- Ambos modos escriben `%AppData%/PulseTrackTaskbar/probe.log` (además de consola) para diagnóstico sin UI.

```powershell
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --probe
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --log chrome
```

## Logs

- `IAppLogger.Log(scope, message)` → `%AppData%/PulseTrackTaskbar/log.txt` vía `FileLogger`, formato `[fecha] [scope] mensaje`.
- Scopes usados: `Overlay` (scroll/reposicion), `Timer` (excepciones de tick), `App` (flush, pick, stop, clicks), `Watcher` (errores de enum), `Config` (load/save).
- DB compartida: `%AppData%/reloj-svelte/sessions.db`. Solo tablas `app_sessions` + `time_blocks` con `source='manual'`; crash-recovery cierra `status='active'` a `'closed'` al arrancar.
- Config: `%AppData%/PulseTrackTaskbar/settings.json` vía `FileConfigStore` (fuente, colores, `LastApp`).

## Architecture rules

- **Desacoplar para testear**: `ForegroundTimer` recibe `IForegroundSource` + `ITickScheduler` por ctor; `SessionCoordinator` recibe `(timer, ISessionStore, IClock)`; `TaskbarLayout` recibe `ITaskbarGeometry`; todo lo Win32/IO entra por ctor con default (`NullLogger`, `Win32TaskbarGeometry`). Tests usan fakes (`FakeForegroundSource`, `ManualTickScheduler`, `FakeSessionStore`/`FakeSessionRepository`, `FakeClock`, `FakeLogger`, `FakeTaskbarGeometry`, `FakeWidget`).
- **Comparar por `ProcessName`**, nunca por título — el título cambia, el tick compara proceso.
- **Todos los handlers de timers con try/catch + `IAppLogger`** — un tick sin proteger cuelga la app sin mensaje.
- **Nada de I/O en hilo UI**: coordinator es async, SQLite solo vía `ChannelSessionStore` en background; `TimerAppContext.Dispose` drena (`StopAsync` + `DisposeAsync` con `.GetAwaiter().GetResult()`, seguro por `ConfigureAwait(false)`).
- **UI solo desde hilo UI** (`BeginInvoke`); `Overlay`/`Program` protegen `ObjectDisposedException` + `InvalidOperationException`.
- **Z-order**: mantener bump de 100 ms con `SetWindowPos(HWND_TOP, SWP_NOACTIVATE...)` — la taskbar reordena hijos.
- **P/Invoke solo en `Infra/Win32/NativeMethods.cs`** — prohibido `DllImport` en otro archivo.
- **Nueva función = nuevo `ITaskbarWidget`** (`Id/GetText/GetWidthHint/Refresh`) + `overlay.SetWidget()` — posición, fullscreen-hide, scroll y logging ya resueltos.
- **Proyecto principal excluye tests**: `<Compile Remove="PulseTrack.Taskbar.Tests/**/*.cs" />` en el csproj (el glob del SDK si no compila los tests dentro del WinExe).

## Code conventions

- `namespace PulseTrack.Taskbar;` — file-scoped
- `_camelCase` private fields, `PascalCase` métodos/tipos
- `record` para datos (`WindowInfo`, `LapInfo`, `TimerTick`, `TaskbarRect`, `OverlayPosition`); `interface` para seams (`IForegroundSource`, `ITickScheduler`, `ISessionStore`, `IAppLogger`, `IConfigStore`, `ITaskbarGeometry`, `ITaskbarWidget`)
- `IDisposable` en timer/scheduler/DB; `IAsyncDisposable` en stores async; `GC.SuppressFinalize(this)`
- Async con `ConfigureAwait(false)` en `App/` e `Infra/`; `ConfigureAwait(true)` solo en handlers UI que tocan controles
- `internal ...ForTest` + `InternalsVisibleTo` para hooks de test, nunca API pública solo-para-test

## Testing

- xUnit en `PulseTrack.Taskbar.Tests/` (39 tests): fakes en archivos propios (`FakeSessionRepository.cs`) o junto al test (`FakeSessionStore`, `FakeClock`, `FakeLogger`, `FakeTaskbarGeometry`, `FakeWidget`)
- Unidades: `ForegroundTimer` (foreground/case/pause/resume/stop/laps/eventos), `SessionCoordinator` async (pick/lap/stop/flush vía `FakeSessionStore`), `ChannelSessionStore` (ids de fondo, FIFO concurrente, drenado en dispose, close flow), logging/config (`FakeLogger`, `FileConfigStore` en temp dir), layout/widgets (`TaskbarLayout` con `FakeTaskbarGeometry`, `TimerWidget` con timer real + fakes)
- TDD: RED (test que falla) → GREEN (mínimo para pasar) → REFACTOR (migrar UI sin romper)
- `dotnet test PulseTrack.Taskbar.Tests -c Release` antes de cada commit que toque `pulsetrack-taskbar/`

## Commit & PR conventions

- Format: `type(scope): message` (conventional commits)
- Types: `feat`, `fix`, `perf`, `refactor`, `chore`, `docs`, `test`
- Keep changes focused: one logical change per commit
- Always run `dotnet build -c Release` + `dotnet test` (el proyecto de tests) before committing
