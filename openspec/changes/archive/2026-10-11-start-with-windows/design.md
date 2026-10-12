## Context

See `proposal.md` for the motivation. What shapes the approach:

- The host is an unpackaged, self-contained WinUI 3 app with a hand-written `Main` (`Program.cs`): `--snapshot` returns
  before the XAML application starts. The guard, `--quit` and the `--autostart` marker belong in the same place.
- Everything the app reads and writes is found through `AppContext.BaseDirectory` (configuration, `themes/`,
  `plugins/`, `cache/`, `urdeck.log`), so a start with any working directory already works, and a copy in another
  folder is a fully separate installation.
- The first-party plugins reach the app through a target that runs after `Build` and copies to `$(OutDir)plugins`
  (`UrDeck.Host.csproj`). Nothing in the repository has run `dotnet publish` so far.
- `TrayIcon` builds its menu from `TrayMenuEntry(Label, Run)` and appends every entry as a plain string.
- `UrDeckLog` appends with `File.AppendAllText` under a lock, with `[HH:mm:ss.fff]` and no size limit.
- Starting at sign-in is the sequence `display-recovery` was built for (the display, Explorer and the network arrive
  late): the tray icon is added again on `TaskbarCreated`, an absent display hides the deck until it is connected, and
  the weather provider's failed sample is retried by the engine's backoff. None of this was checked from a cold boot.
- The owner wants to reach the Microsoft Store eventually and to sign releases, at no cost if possible, and will
  research installer technology first. A Store package would change two things this change touches: start-up is
  declared in the package manifest and switched through the `StartupTask` API instead of the registry, and the
  application folder is read-only, which makes the per-user data folder (backlog item 15) a precondition.

## Goals / Non-Goals

**Goals:**

- The owner can install a copy that survives `dotnet build`, switch on "Start with Windows" and leave it for two weeks.
- No choice here has to be undone by the installer: the published folder is what a zip or an installer wraps, `--quit`
  is what an updater calls, and the registration sits behind one interface.
- The logic that can be wrong in interesting ways (states, paths, rotation) has no Windows API in it and is unit-tested.

**Non-Goals:**

- Choosing an installer, a package format or a signing route.
- Moving the user's files out of the application folder.
- Bringing an existing instance forward or passing it arguments (the editor will want this; `--quit` is the only
  message for now).
- Restarting after a crash. During the two-week run a crash should stay visible.

## Decisions

### 1. The Run key, behind `IAutostart`

The registration is the value `UrDeck` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, with the data
`"<full path of the exe>" --autostart`. The host talks to it through a small interface in the host project
(`IAutostart`: read the state, switch on, switch off) with one implementation, `RunKeyAutostart`.

- Over a Startup-folder shortcut: the same effect, but creating and reading a `.lnk` needs COM, and the path it holds
  is harder to compare.
- Over a scheduled task: only needed to start elevated or delayed. The elevated helper (item 3, fourth change) is a
  separate process and a separate decision.
- Over the `StartupTask` API: it exists only for packaged apps. It is the reason for the interface: a packaged build
  adds a second implementation and nothing else changes. Its states map onto the four in the spec (its "disabled by
  user" is "Switched off in Windows"), with one difference a packaged build will have to specify: there the app cannot
  re-enable itself.
- No configuration field. A field would be a second copy of a fact Windows owns, and it would be wrong as soon as the
  user used Task Manager, moved the folder or ran a second copy.

`--autostart` has no effect on behaviour. It exists so the log says how the process was started, which is what one
needs to know when reading a log after a boot.

### 2. Reading the state, including Task Manager's switch

Task Manager and Settings do not remove a Run value when the user disables an app; they write a binary value of the
same name under `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` whose first byte is odd
(observed as `03`; enabled is `02`, and an absent value means enabled). `RunKeyAutostart` reads both.

The state is computed by a pure function in the engine (`UrDeck.Engine/Startup`): the Run data (or null), the
approval bytes (or null) and the running executable's path give On, Off, Another copy or Switched off in Windows. It
parses the quoted path, ignores arguments and compares paths without case. That function is what the tests cover; the
registry access around it is a few lines.

Switching on writes the Run value and deletes the approval value, so the result is On. Overriding the Task Manager
switch is deliberate: the user asked for it in the app's own menu a moment ago, and an entry that does nothing when
chosen would be worse. This is an undocumented registry layout, so task 1.1 confirms it on the owner's Windows build
before the code is written, and the reader treats anything it does not recognise as enabled.

A development build can be registered (the state then names `bin/`). That is allowed and logged rather than blocked:
blocking it would need the app to know what an "installed" copy is, which nothing else needs to know yet.

### 3. One instance per folder, with a named mutex and a named event

`Program.Main` handles, in this order: `--snapshot` (as today), `--quit`, then the guard, then the XAML application.

- The names are `Local\UrDeck.<hash>.instance` and `Local\UrDeck.<hash>.quit`, where `<hash>` is the first 16 hex
  digits of the SHA-256 of the upper-cased, full application folder path. The derivation is a pure function in the
  engine, next to the autostart state.
- The host creates the mutex and holds it for the life of the process. If it already exists, it logs
  `Another UrDeck is already running from this folder; exiting` and returns 0. A mutex is released by the system when
  its process ends, however it ends, so a crash never blocks the next start.
- The host also creates the event and waits on it from a thread-pool thread; when it is set, it closes the main window
  on the UI thread, which is exactly what the tray's Quit does.
- `--quit` opens the mutex. If there is none, it returns 0. Otherwise it sets the event and waits until it can acquire
  the mutex (the host has ended), up to 10 seconds, then releases it and returns 0; on timeout it logs and returns 1.

Keyed on the folder rather than one name for the whole session, because what the guard protects is a configuration
and a log that two processes would both write. Two copies in two folders share neither, and the owner needs a
development build beside the daily copy; with a session-wide name that would take an override variable, and `--quit`
from the install script could close the wrong copy. The cost: two copies on the same display stack on top of each
other. That is visible at once and only happens to someone who started the second copy on purpose.

`Local\` scopes both objects to the session, so they never reach another user's host.

Over a window message to the tray's message window: finding that window from another process means a class-name
search that cannot tell two folders apart. Over a pipe: more than one message is not needed yet.

### 4. Checked tray entries

`TrayMenuEntry` gains an optional `Func<bool>? IsChecked`. `ShowMenu` calls it while building the menu and adds
`MF_CHECKED`. `App` builds the list: `Start with Windows` (checked when the state is On, the action switches on or
off as the spec says) and `Quit`. Reading two registry values per menu opening costs nothing measurable and keeps the
entry right after a change in Task Manager; there is no cached state to go stale.

### 5. Publish: one more copy target

A target after `Publish` copies the same plugin assemblies to `$(PublishDir)plugins`, reusing the item list of the
existing `CopyBuiltInPlugins` target. The sample configuration and the icon are already publish items. The first task
runs `dotnet publish` once and compares the result with the build output before anything else is written, because
this is the first publish of a self-contained WinUI 3 host in this repository and the Windows App SDK's own files
(`resources.pri`, the native runtime) have to be confirmed, not assumed.

No single-file publish, no trimming, no ReadyToRun: each changes how plugins load or what the Windows App SDK
tolerates, and none is needed to run from a folder. They are measurements for the release change.

### 6. `scripts/Install-Local.ps1`

PowerShell 7, the shell the repository's commands already assume. Steps: publish to a temporary folder; if
`<destination>\UrDeck.Host.exe` exists, run it with `--quit` and stop on a non-zero exit code; copy; start the copy
if it was running or `-Start` was given; print the destination.

Copying is two `robocopy` passes, because the application's files are mirrored and `plugins/` is not:

- everything except `plugins/`: mirror (`/MIR`), excluding the directories `plugins`, `themes`, `cache` and the files
  `urdeck-config.json`, `urdeck.log`, `urdeck.log.1` on both sides, so they are neither overwritten nor purged;
- `plugins/`: copy without purging, so plugins the user added stay.

A first-party plugin that is removed from the repository therefore stays in an installed `plugins/` until the user
deletes it. That is accepted for a developer script; the installer needs a manifest of what it installed, which is its
own design. The sample configuration is copied afterwards only if the destination has none.

"Was it running" is taken from the processes whose main module is the destination executable, before `--quit`.

The script is a convenience for someone with the sources. It is not the installer and does not register autostart, so
that the installer can replace it without a user-visible setting changing hands.

### 7. Log: date in the line, rotate at 5 MB

The prefix becomes `[yyyy-MM-dd HH:mm:ss.fff]`. `UrDeckLog` keeps the size of the current file in memory (read once
when a path is first used, then increased by what it appends) and rotates under the existing lock when it passes the
cap: `File.Move(log, log + ".1", overwrite: true)`. An `IOException` or `UnauthorizedAccessException` from the move is
swallowed like the ones from the append, and the size check simply fires again on a later write.

5 MB is a guess at "weeks of normal lines, hours of a failure that logs every attempt"; the 6.x checks record the real
growth per day so the number can be corrected. One previous file rather than a dated series: there is no code that
would delete old files, and two files are enough to read back across one rotation.

The `LogPath` redirect used by tests keeps working: the tracked size is per path.

### 8. What is checked only on the panel

A cold boot cannot be scripted from the repository. Section 6 of the tasks is the owner's: sign-in starts, in what
order the log shows the display, the tray icon and the first weather values, and how long each took. If the deck is
not right after a cold boot, the fix belongs to `display-targeting` or the provider concerned and is a follow-up, not
a delay added here.

## Risks / Trade-offs

- [The StartupApproved layout is undocumented and could differ between Windows builds] → task 1.1 confirms it first;
  unknown data reads as enabled, so the worst case is a check mark on an entry Windows has disabled, corrected by
  choosing the entry twice.
- [A hash of the folder path treats two spellings of one folder (a junction, a `subst` drive) as two folders] → both
  copies would then write one configuration. Accepted: the installed copy is always started by the same path.
- [The first self-contained publish may miss a file the build output has] → task 2.1 compares the two folders and the
  published copy is started on the panel before the script is written around it.
- [The date makes every log line 11 characters longer and changes a format people grep] → the notes in `AGENTS.md`
  quote messages, not timestamps; tests that match the prefix are updated in the same change.
- [Registering a development build leaves a Run value pointing into `bin/`] → the state is visible in the menu from
  any other copy ("Another copy" shows no check mark), and choosing the entry in the installed copy repoints it.
- [`robocopy` exit codes below 8 mean success] → the script maps them, since PowerShell would otherwise treat a
  normal copy as a failure.
- [Sign-in starts the app before the display or the network is ready] → that is the absent-display and retry
  behaviour already specified; section 6 verifies it instead of adding a start delay.

## Migration Plan

Nothing to migrate: no configuration field and no file moves. The owner's working configuration is copied by hand
once from the build output to the installed folder (task 6.1), after which the build output is only for development.
Rolling back is switching the entry off (or deleting the `UrDeck` Run value) and deleting the installed folder.

## Open Questions

- Whether 5 MB is the right cap is answered by the growth measured in section 6.
- Whether the installer keeps `%LOCALAPPDATA%\Programs\UrDeck` is part of the installer research; the script takes
  `-Destination`, and nothing in the app knows the path.
