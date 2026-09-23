# PulseTrack.Taskbar — WinForms stopwatch + timer para la taskbar

## Mapa mental

- **Stopwatch**: `Start` y corre. Elegir app es **opcional** — `SelectedApp == null` = corre siempre; con app elegida solo acumula cuando esa app está en foreground (comparación por `ProcessName`, **nunca** por título). `SelectedApp` sobrevive a `Stop()` (es preferencia, no parte de la sesión); `StopAsync` se guarda por `SessionId`.
- **Timer (cuenta regresiva) con alarma**: `Core/CountdownTimer` con scheduler propio (500ms) — corre siempre, ignora el filtro de foreground. **Elegir duración ≠ arrancar**: presets/inputs/Custom solo stagean (`StageCountdown`, label "ready"); arrancar es explícito (`StartCountdown` o `ToggleCountdown`). Al llegar a 0: `IAlarm` → `Infra/SystemAlarm` (`SystemSounds.Exclamation`) + balloon del tray, **una sola vez** (`Expired` se re-arma en `Set`). `Started` distingue "ready" de "paused".
- **Dos ejes de UI, no uno solo**: (1) **Modo** `AppMode` — Normal/Taskbar/Pip, exclusivos, decides `SurfaceHost`, persistido en settings; `--mode` es override solo-esta-corrida (por eso `ModeChanged` persiste solo en cambios reales). (2) **Foco** `FocusMode` — Stopwatch/Timer, global, lo define la tab seleccionada de Normal (`SyncFocusFromTab` → `TimerCommands.SetFocus` → `FocusChanged` → `RenderState`); gobierna qué reloj es protagonista en cada superficie y a qué apuntan los botones (`TogglePrimaryAsync`/`StopPrimaryAsync`).
- **Persistencia**: `sessions.db` (SQLite, `app_sessions` + `time_blocks`) = sesiones, `app_name` nullable = stopwatch libre, crash-recovery cierra `active → closed` al arrancar. `settings.json` = preferencias (modo, foco, `LastApp`, `LastCountdownSeconds`, fuente, colores, posiciones por superficie).
- **Estado**: se arma **una sola vez** en `TimerViewStateFactory` (`TimerTick` + app + countdown + foco → `TimerViewState`); las 3 superficies son tontas: `Render(state)` y comandos, nada de formatear tiempos (usar `ClockFormat`) ni escribir config/DB.
- **La X de Normal oculta, no cierra** (el timer sigue corriendo); `NormalForm.AllowClose()` es el flag que deja pasar el cierre real en el shutdown. Salir de verdad: `Tray → Exit`.

## Reglas de capas

| Capa | Contenido | Prohibido |
|---|---|---|
| `Core/` | dominio puro + seams (`IAppLogger`, `IConfigStore`, `ITickScheduler`, `ITimerSurface`, `IAlarm`, `IClock`, records del dominio) | WinForms, SQLite |
| `App/` | lógica testeable: `TimerCommands`, `TimerViewStateFactory`, `SurfaceHost`, `SessionCoordinator`, `WindowPlacement` | WinForms |
| `Infra/` | implementaciones: `Database`, `ChannelSessionStore`, `FileConfigStore`, `FileLogger`, `SystemAlarm`, `Win32/` | — |
| `UI/` | WinForms lo más tonta posible: NormalForm (tabs Stopwatch\|Timer), TaskbarOverlayForm, PipForm, TrayIconPresenter, diálogos | formatear, escribir config/DB directo |

Composition root único: `Program.cs` (`TimerAppContext`) — crea todo, captura el hilo UI para marshalling, flush cada 60s, arma el render dual (stopwatch + countdown) y dispara la alarma al expirar. CLI (`--mode` / `--probe` / `--log`) también vive ahí — ver `README.md` para el detalle de uso.

## Scars / invariantes (leer antes de tocar código)

- **Desacoplar para testear**: todo seam entra por ctor con default (`NullLogger`, `Win32TaskbarGeometry`, picker como `Func<string?, Task<AppSelection>>`); los tests usan fakes (ver `PulseTrack.Taskbar.Tests/`).
- **Todo control WinForms con `Font` explícita**: `NormalForm.Dispose` hace `c.Font?.Dispose()` por hijo; un control sin font propia disposea la fuente compartida del proceso y todo control construido después revienta con GDI+ "Parameter is not valid" (envenena otras clases de test).
- **Nada de propiedades setteables en subclases de `Control`**: WFO1000 es error (`TreatWarningsAsErrors`) — hooks get-only + métodos `...ForTest`.
- **`Button.PerformClick` y `TabControl.SelectedIndexChanged` no firean sin handle** (el índice sí cambia): extraer la lógica a un método nombrado y llamarlo desde el evento y desde el hook ForTest.
- **`ValueChanged` de numéricos se engancha DESPUÉS de sembrar los valores iniciales** — si no, la construcción del form dispara efectos a nivel boot.
- **`StageCountdown` ignora el set si está corriendo** — el sync render→inputs no puede matar un timer en marcha.
- **El foco y el modo tienen guard de mismo valor** (`SetFocus` / `ModeChanged`): sin él, render y evento se realimentan en loop.
- **Todos los handlers de timers con try/catch + `IAppLogger`** — un tick sin proteger cuelga la app sin mensaje.
- **UI solo desde hilo UI**: `TimerAppContext` marshalea con `SynchronizationContext` — nunca usar el `InvokeRequired` de un form como ancla (sin handle devuelve `false`). Red de seguridad: capturar `ObjectDisposedException` + `InvalidOperationException` en `Render`.
- **Nada de I/O en hilo UI**: SQLite solo vía `ChannelSessionStore` (Channel FIFO, background); `Dispose` drena con `.GetAwaiter().GetResult()` (seguro por `ConfigureAwait(false)` en `Infra/` y `SessionCoordinator`).
- **Config con dueño único**: escribir solo con `IConfigStore.Update(c => ...)`, nunca guardar una copia en memoria (pisa campos de otro dueño: bug de las coordenadas del PiP).
- **P/Invoke solo en `Infra/Win32/NativeMethods.cs`** — prohibido `DllImport` en otro archivo.
- **Nueva superficie = nuevo `ITimerSurface`** sumado al array de `SurfaceHost`: modo, visibilidad exclusiva, cache y logging ya resueltos.
- **Z-order**: bump de 100ms con `SetWindowPos(HWND_TOP, SWP_NOACTIVATE...)` — la taskbar reordena hijos.
- **El botón de start/pause nunca se deshabilita** (`ToggleStartPauseAsync` siempre sirve); `CanPause` es `Running`, no "se puede togglear".
- **Cancelar ≠ elegir "sin app"** en el picker: por eso `AppSelection(bool Confirmed, string? AppName)`.
- **Proyecto principal excluye tests**: `<Compile Remove="PulseTrack.Taskbar.Tests/**/*.cs" />` en el csproj.

## Cómo explorar (no hay inventario de archivos acá)

1. `codegraph status` → si no hay índice, `codegraph init` (una vez por repo).
2. `codegraph explore "símboloA símboloB" --max-files 8` para flujos y relaciones; `codegraph impact X` antes de romper algo con muchos llamadores; `codegraph files --format flat` para la estructura.
3. `glob **/*.cs` para archivos, `grep` para strings/literales (codegraph no indexa strings ni JSON).
4. La suite es la spec viva: los nombres de los tests describen el comportamiento esperado — `dotnet test` los lista.
5. `README.md` = lado usuario (uso, modos, args CLI, tabla de archivos, datos en `%AppData%`).

## Cómo verificar

```powershell
dotnet build PulseTrack.Taskbar.csproj -c Release   # 0 warnings, TreatWarningsAsErrors
dotnet test PulseTrack.Taskbar.Tests -c Release     # suite completa verde antes de cada commit
dotnet publish PulseTrack.Taskbar.csproj -c Release -o publish-single /p:PublishSingleFile=true --no-self-contained
dotnet run --project PulseTrack.Taskbar.csproj -c Release -- --probe   # CLI diagnóstico sin UI
```

Si el exe está corriendo, el build falla con `MSB3027` (archivo bloqueado): matar `PulseTrack.Taskbar.exe` antes de compilar. Requiere **.NET 9 Desktop Runtime**.

## Stack y convenciones

- **.NET 9** WinForms (`net9.0-windows10.0.19041.0`), STA; **Microsoft.Data.Sqlite** pineado; `global.json` + `Directory.Build.props` (`Deterministic`, `TreatWarningsAsErrors`, `ContinuousIntegrationBuild`) para builds repetibles.
- Logs: `%AppData%/PulseTrackTaskbar/log.txt`, formato `[fecha] [scope] mensaje` (`IAppLogger.Log`; los scopes en uso se descubren con `grep "_logger.Log"`).
- `namespace PulseTrack.Taskbar;` file-scoped; `_camelCase` privados; `record` para datos, `interface` para seams; `IDisposable`/`IAsyncDisposable` con `GC.SuppressFinalize(this)`.
- `ConfigureAwait(false)` en `Infra/` y `SessionCoordinator`; `ConfigureAwait(true)` en handlers UI y en `TimerCommands`.
- Hooks de test: `internal ...ForTest` + `InternalsVisibleTo`, nunca API pública solo-para-test.

## Commit & PR

- `type(scope): message` (conventional): `feat`, `fix`, `perf`, `refactor`, `chore`, `docs`, `test`.
- Un cambio lógico por commit; correr build + tests antes de commitear.
