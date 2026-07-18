# CLAUDE.md — Talisman

Guidance for Claude Code when working in this repository. These instructions
sit under the user's global `~/.claude/CLAUDE.md`; where they conflict, the more
specific rule here wins.

## What Talisman is

Talisman is a personal productivity app for Windows, written for the author's
own use. It renders a small always-on-top graphical element (a floating icon)
that watches the calendar and makes meetings and events hard to ignore.

Core behavior:

- **Extreme reminders.** When a meeting/event is approaching, Talisman throws
  up floating notification dialogs that drift around on top of everything else.
  Dismissing them requires a small puzzle / bit of cognition tied to the
  appointment, so they can't be reflexively swatted away — the point is that a
  meeting cannot be missed.
- **Timers & stopwatch.** One-click fixed-length or time-of-day timers, plus a
  stopwatch on the floating icon.
- **Hot keys.** Global hot keys for actions Windows makes hard or impossible
  (quick email, instant lock + screensaver, snipping tool, instant timer).

## Tech stack

- **Language:** C# — WPF desktop application.
- **Framework:** **.NET 8** (`net8.0-windows`, Windows Desktop SDK), `WinExe`
  output, `UseWPF` + `UseWindowsForms` (the screen/cursor helpers use
  `System.Windows.Forms`). Modern .NET, not .NET Framework — use current .NET
  APIs. (Migrated from .NET Framework 4.8; bump the TFM to net9/net10 once that
  SDK is installed.)
- **UI:** WPF (XAML + code-behind) with an MVVM-leaning structure — models
  derive from `BaseModel` which implements `INotifyPropertyChanged`.
- **Build system:** **SDK-style** `.csproj` with `PackageReference`. Source and
  XAML files are globbed automatically — no need to register `<Compile>`/`<Page>`
  items. Build/test/run with the `dotnet` CLI.
- **Key dependencies:** Microsoft.Office.Interop.Outlook (COM interop, embedded;
  the running instance is fetched via a P/Invoke `GetActiveObject` since
  `Marshal.GetActiveObject` is gone on modern .NET), Newtonsoft.Json,
  System.Configuration.ConfigurationManager (for `Properties.Settings`). NSIS
  builds the installer. **Note (spike):** the Outlook interop DLL is still
  referenced by `HintPath` into the old `packages/` folder — move it to a
  committed `lib/` or a `COMReference` before relying on a clean checkout.

## Layout

```
src/
  Talisman.sln                  — the solution (open this)
  WindowsClient/                — the one project
    App.xaml(.cs)               — application entry point
    MainWindow.xaml(.cs)        — the floating icon window
    Models/                     — AppModel, Calendar, TimerInstance, HotKey*, BaseModel …
    Controls/                   — WPF widgets: NotificationWidget, TimerDetailsWidget,
                                  SettingsForm, QuickMailSender, ReminderSummary …
    Helpers/                    — HotKeyHelper, OutlookHelper, ScreenHelper,
                                  DraggingLogic, TimeRelatedItem …
    Assets/                     — images, help text, HotKeyOptions.json
    Install.nsi                 — NSIS installer script (built post-build)
installs/  misc/                — installer output & scratch
```

## Diagnostics & logging

Talisman logs to `%LOCALAPPDATA%\Talisman\logs\talisman-<date>.log` (daily
rolling, 14-day retention). The logging code lives in
[Helpers/Logging/](src/WindowsClient/Helpers/Logging/).

- **Log through the ambient `Log` static** — `Log.Info/Warn/Error/Fatal(...)`.
  It is backed by `FileLogger` (thread-safe) and initialized first thing in
  `App.OnStartup` via `InitializeDiagnostics()`.
- **Global crash handlers** are wired in `App`: `DispatcherUnhandledException`
  (UI thread — logged, then kept alive), `AppDomain.UnhandledException`
  (background — logged Fatal), and `TaskScheduler.UnobservedTaskException`.
  Any new background thread/`Task` is covered by these, but prefer a local
  try/catch that logs context.
- `Debug.WriteLine`/`Trace.WriteLine` are bridged into the log by
  `LoggerTraceListener`. **Caveat:** `Debug.WriteLine` is compiled out of Release
  builds, so anything that must be visible in production must call `Log.*`
  directly, not `Debug.WriteLine`.
- **Don't silently swallow exceptions** — log before swallowing (see the pattern
  in [OutlookHelper.cs](src/WindowsClient/Helpers/OutlookHelper.cs)).
- The `CrashedLastTime` setting flags an unclean exit; `App` surfaces it on the
  next launch and offers "Open Log Folder" (also on the tray icon's context
  menu). Keep archived Release `.pdb`s per version so logged stack traces
  symbolicate.

### Crash recovery

- **Auto-restart** (`AutoRestartOnCrash` setting, default on): on a fatal
  (terminating) unhandled exception, `App` relaunches itself with the
  `--auto-restart` arg. `RestartPolicy` is a circuit breaker — it allows up to
  3 restarts within a 5-minute window, then stays down to avoid a crash loop.
  The successor waits for the dying process to exit before its single-instance
  check. `RestartPolicy` is pure logic and unit-tested.
- **Crash-report email** (`CrashReportEmail` setting, blank = off): after an
  unclean exit, the next launch offers to email the recent log to that address
  via `OutlookHelper`. On a seamless auto-restart it sends silently instead of
  prompting. Both settings are editable on the Settings → About tab.
- **UI-bound collections** (`ActiveTimers`/`RecentTimers`/etc. in `AppModel`)
  must only be mutated on the Dispatcher thread — use the `_dispatch(...)`
  action. Mutating them from a background thread throws the WPF "CollectionView
  does not support changes ... from a different thread" exception (the original
  wild crash). Any new background work that touches these must marshal first.

## Pomodoro

A guided "Pomodoro day" runs four phases: **Short & Easy** (I), **Joy** (II),
**Admin** (III), and **Extra** (IV). Short phases show a per-task countdown from
"time per task"; the Joy/Admin blocks show a per-task elapsed count-up. Phase I
ends on the min/max-short-time + min-tasks rules and hands its leftovers to
Phase IV; the blocks end when their time budget is spent or their tasks run out.

- **All rules live in [`PomodoroSession`](src/WindowsClient/Models/Pomodoro/PomodoroSession.cs)** — a
  pure state machine where every clock-dependent method takes `DateTime now`, so
  it is fully unit-tested without timers or UI. Config is parsed by
  `PomodoroSettings` and persisted as one JSON blob in the `PomodoroConfig`
  setting.
- **[`PomodoroController`](src/WindowsClient/Controls/PomodoroController.cs)** is the thin
  WPF glue: it owns the session + a `DispatcherTimer`, drives the floating
  `PomodoroTaskWindow` (positioned under the talisman), and shows the
  `PomodoroPromptWindow` / `PomodoroSummaryWindow`. Started from the Settings →
  Pomodoro tab via `AppModel.StartPomodoro()`.
- When changing phase behavior, change `PomodoroSession` and its tests; keep the
  controller free of rules.

## Code conventions

- **One class per file**, file named for the class. Favor an OO structure that
  keeps the architecture easy to follow.
- **MVVM:** put logic and state in the `Models` (subclasses of `BaseModel`,
  raising `NotifyPropertyChanged`); keep `Controls` code-behind thin, mostly
  wiring XAML to the model. Bindable properties should notify on change.
- **Namespace** is `Talisman` throughout.
- Match the surrounding file's style (brace placement, `///` XML-doc comment
  banners with the dashed-line separators already used in the models).
- When adding a file, remember to register it in `WindowsClient.csproj`.

## Building

- `dotnet build src/WindowsClient/WindowsClient.csproj -c Debug` (restore is
  automatic). Or build the solution `src/Talisman.sln`.
- Run with `dotnet run --project src/WindowsClient/WindowsClient.csproj`, or
  launch `bin/Debug/net8.0-windows/Talisman.exe` (framework-dependent — needs
  the Windows Desktop 8 runtime installed).
- For distribution, `dotnet publish` (prefer self-contained single-file so users
  don't need the runtime). **The NSIS installer post-build step was dropped in
  the SDK migration** and needs reworking around `dotnet publish` output before
  cutting a real release.

## Testing — required

Tests live in **`src/WindowsClient.Tests`** — an **xUnit** project targeting
`net8.0-windows` (`UseWPF=true` because the classes under test use WPF types like
`Visibility` and `Key`). It has a `ProjectReference` to `WindowsClient` and is
part of `Talisman.sln`. Current coverage includes `BaseModel`,
`HotKeyAssignment`, `TimerInstance` (attention-word puzzle), `QuickMailItem`,
the logging stack (`FileLogger`/`Log`), `RestartPolicy`, and the whole Pomodoro
engine (`PomodoroSession`, `PomodoroSettings`, `TaskTimeoutTracker`).

Rules:

- **Every new feature must ship with unit tests.** When adding functionality,
  add or extend tests that cover it. Prefer putting testable logic in the
  `Models`/`Helpers` (plain classes) rather than in XAML code-behind, so it can
  be exercised without spinning up the UI. When a class is hard to test because
  it reaches straight into UI or Outlook interop (e.g. `QuickMailItem.Send`),
  introduce a seam rather than leaving it uncovered.
- **Always run the tests before committing.** Never commit with failing or
  unrun tests. If tests genuinely cannot be run in the current environment, say
  so explicitly rather than committing blind.
- **Test after each build, not at the end.** After a change that builds, run the
  relevant tests before moving on rather than stacking up changes.
- Unit tests passing is not proof the feature works. For behavior that touches
  the always-on-top UI, calendar polling, or hot keys, verify the real app
  behavior where feasible before declaring the work done.

### Build & run tests (from a shell)

Now that the project is SDK-style .NET 8, the `dotnet` CLI just works:

```
# build the app
dotnet build src/WindowsClient/WindowsClient.csproj -c Debug

# build + run the tests
dotnet test src/WindowsClient.Tests/WindowsClient.Tests.csproj -c Debug

# run the app (framework-dependent; needs the Windows Desktop 8 runtime)
dotnet run --project src/WindowsClient/WindowsClient.csproj
# or launch src/WindowsClient/bin/Debug/net8.0-windows/Talisman.exe
```

In Visual Studio, just use Test Explorer. (The old VS-MSBuild-only + `vstest`
workaround is no longer needed — that was a .NET Framework limitation.)

## Committing

- Use the `/prep-commit` command to compose commits.
- **Stage explicit paths — never `git add .` / `git add -A`** — so build output
  under `bin/` and `obj/`, installer artifacts, and scratch files don't get
  swept in.
- Do not commit until the build succeeds and tests pass.
