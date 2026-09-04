# PulseTrack.Taskbar — WinForms foreground stopwatch

## Stack

- **.NET 9** WinForms (`net9.0-windows10.0.19041.0`), `OutputType WinExe`, STA
- **Microsoft.Data.Sqlite 9.*** — `%AppData%/reloj-svelte/sessions.db`, `journal_mode=WAL` + `busy_timeout=5000`
- **Win32 P/Invoke** — `GetForegroundWindow`, `EnumWindows`, `GetWindowText`, `GetWindowThreadProcessId`, `FindWindow("Shell_TrayWnd")`, `SetWindowPos`
- **Overlay** en taskbar (`TaskbarOverlayForm.cs`, `SetTimer()`; scroll + fullscreen-hide + Z-bump 100ms)
- **xUnit** — tests en `PulseTrack.Taskbar.Tests/` (net9 windows, referencia al csproj principal)
- Requiere **.NET 9 Desktop Runtime** (no el runtime común)

## Setup & dev

Todos los comandos se corren desde la raíz del repo (donde está este AGENTS.md):

```powershell
dotnet build PulseTrack.Taskbar.csproj -c Release
dotnet test PulseTrack.Taskbar.Tests
dotnet publish PulseTrack.Taskbar.csproj -c Release -o publish-single /p:PublishSingleFile=true --no-self-contained
```

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

- `OverlayConfig.Log(scope, message)` → `%AppData%/PulseTrackTaskbar/log.txt`, formato `[fecha] [scope] mensaje`.
- Scopes usados: `Overlay` (scroll/reposicion), `Timer` (excepciones de tick), `App` (flush, pick, stop, clicks), `Watcher` (errores de enum), `Config` (load/save).
- DB compartida: `%AppData%/reloj-svelte/sessions.db`. Solo tablas `app_sessions` + `time_blocks` con `source='manual'`; crash-recovery cierra `status='active'` a `'closed'` al arrancar.
- Config: `%AppData%/PulseTrackTaskbar/settings.json` (fuente, colores, `LastApp`).

## Architecture rules

- **Desacoplar para testear**: `ForegroundTimer` recibe `IForegroundSource` + `ITickScheduler` por ctor; la UI usa `SystemForegroundSource` + `FormsTickScheduler`, los tests usan fakes.
- **Comparar por `ProcessName`**, nunca por título — el título cambia, el tick compara proceso.
- **Todos los handlers de timers con try/catch + `OverlayConfig.Log`** — un tick sin proteger cuelga la app sin mensaje.
- **UI solo desde hilo UI** (`BeginInvoke`); `Overlay`/`Program` protegen `ObjectDisposedException` + `InvalidOperationException`.
- **Z-order**: mantener bump de 100 ms con `SetWindowPos(HWND_TOP, SWP_NOACTIVATE...)` — la taskbar reordena hijos.
- **Proyecto principal excluye tests**: `<Compile Remove="PulseTrack.Taskbar.Tests/**/*.cs" />` en el csproj (el glob del SDK si no compila los tests dentro del WinExe).

## Code conventions

- `namespace PulseTrack.Taskbar;` — file-scoped
- `_camelCase` private fields, `PascalCase` métodos/tipos
- `record` para datos (`WindowInfo`, `LapInfo`, `TimerTick`); `interface` para seams (`IForegroundSource`, `ITickScheduler`)
- `IDisposable` en timer/scheduler/DB; `GC.SuppressFinalize(this)`
- P/Invoke agrupado por clase con `[DllImport]`, `SetLastError` donde aplique

## Testing

- xUnit en `PulseTrack.Taskbar.Tests/` (un proyecto, fakes `FakeForegroundSource` + `ManualTickScheduler` en el mismo archivo de tests)
- `ForegroundTimer` es la unidad testeada: acumula solo en foreground, pause/resume/stop/lap, no duplica ticks en resume, eventos `Ticked`
- `dotnet test PulseTrack.Taskbar.Tests` antes de cada commit que toque `pulsetrack-taskbar/`

## Commit & PR conventions

- Format: `type(scope): message` (conventional commits)
- Types: `feat`, `fix`, `perf`, `refactor`, `chore`, `docs`, `test`
- Keep changes focused: one logical change per commit
- Always run `dotnet build -c Release` + `dotnet test` (el proyecto de tests) before committing
