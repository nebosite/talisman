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
- **Framework:** .NET Framework **4.8** (`TargetFrameworkVersion v4.8`),
  `WinExe` output. This is *not* .NET Core / .NET 5+. Use APIs available in
  .NET Framework 4.8 and C# language features supported by that toolchain.
- **UI:** WPF (XAML + code-behind) with an MVVM-leaning structure — models
  derive from `BaseModel` which implements `INotifyPropertyChanged`.
- **Build system:** Old-style MSBuild `.csproj` with an explicit
  `packages.config` NuGet restore (not PackageReference / SDK-style). New source
  files must be added to `WindowsClient.csproj` `<Compile>` items to be built.
- **Key dependencies:** Microsoft.Exchange.WebServices, Microsoft.Office.Interop
  .Outlook / NetOffice (calendar access), Newtonsoft.Json. NSIS builds the
  installer as a post-build step.

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

- Build the solution `src/Talisman.sln` (Debug|AnyCPU by default). From a
  developer shell you can use MSBuild, e.g.
  `msbuild src/Talisman.sln /t:Build /p:Configuration=Debug`.
- First build needs NuGet packages restored (`nuget restore src/Talisman.sln`
  or Visual Studio's "Restore NuGet Packages").
- Release build runs NSIS post-build to produce `TalismanSetup.exe`.

## Testing — required

Tests live in **`src/WindowsClient.Tests`** — an **xUnit** project targeting
`net48` (SDK-style csproj, `UseWPF=true` because the classes under test use WPF
types like `Visibility` and `Key`). It has a `ProjectReference` to
`WindowsClient` and is part of `Talisman.sln`. Current coverage: `BaseModel`,
`HotKeyAssignment`, `TimerInstance` (including the attention-word puzzle), and
`QuickMailItem`.

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

This machine has VS 2022 Community. The full VS MSBuild builds the legacy WPF
project correctly (the .NET SDK's `dotnet build` does not), and `vstest.console`
runs the resulting test assembly:

```
MSBUILD="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
VSTEST="/c/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TestWindow/vstest.console.exe"

# restore + build the test project (also builds WindowsClient)
"$MSBUILD" src/WindowsClient.Tests/WindowsClient.Tests.csproj -restore -t:Build -p:Configuration=Debug

# run the tests
"$VSTEST" src/WindowsClient.Tests/bin/Debug/net48/Talisman.Tests.dll
```

In Visual Studio, just use Test Explorer. Prefer building/running through VS
MSBuild + vstest over `dotnet build`/`dotnet test`, which struggle with the
legacy WPF `WindowsClient` project.

## Committing

- Use the `/prep-commit` command to compose commits.
- **Stage explicit paths — never `git add .` / `git add -A`** — so build output
  under `bin/` and `obj/`, installer artifacts, and scratch files don't get
  swept in.
- Do not commit until the build succeeds and tests pass.
