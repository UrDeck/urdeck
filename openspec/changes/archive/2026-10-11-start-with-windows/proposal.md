## Why

Milestone 1 ("daily driver") is done when the owner's PC runs UrDeck in place of HYTE Nexus for two weeks, and the run
cannot begin: the app has to be started by hand after every sign-in, and the only copy of it lives in the build output
(`src/UrDeck.Host/bin/Release/...`), where every `dotnet build` either fails on locked files or replaces the working
configuration with the sample. This is "start with Windows, in its simplest form" from backlog item 12, plus the three
things a copy that runs for two weeks needs: a stable folder to run from, a guard against a second copy, and a log
that says which day something happened.

## What Changes

- **Start with Windows.** The tray menu gains a checked entry, "Start with Windows", above Quit. It registers the
  running copy to start at sign-in for the current user, without elevation. Windows is the source of truth: there is
  no configuration field, and the entry shows what Windows will actually do, including when the user switched the
  app off in Task Manager or Settings.
- **A complete published folder.** `dotnet publish` of the host produces everything the app needs to run, including
  `plugins/` (today the plugins are copied only by the build, into the build output).
- **A local install script.** One command publishes and copies the app to a stable per-user folder
  (`%LOCALAPPDATA%\Programs\UrDeck`), closes a running copy first and starts it again afterwards. It never replaces the
  user's configuration, log, icon cache, themes or plugins it did not build.
- **Single instance.** A second copy started from the same folder logs one line and exits. A copy from another folder
  (a development build beside the daily one) still starts.
- **`--quit`.** `UrDeck.Host.exe --quit` asks the running copy from the same folder to close the way the tray's Quit
  does, and waits for it. The install script uses it; a later installer or updater can too.
- **A log for a long run.** Every line of `urdeck.log` carries the date, and the file is capped: when it passes 5 MB
  it becomes `urdeck.log.1` (replacing the previous one) and a new file starts.

## Capabilities

### New Capabilities
- `autostart`: starting UrDeck at sign-in: what is registered with Windows, the states the user can see, and how the
  tray entry changes them.
- `distribution`: what a published copy of the app contains and how a local install treats the user's files. The
  release zip and the installer will extend it.

### Modified Capabilities
- `host-shell`: new requirements "Single Instance", "Quit Command" and "Log File" (dated lines, the size cap). No
  existing requirement changes.
- `tray-icon`: "Tray Menu" (an entry may show a check mark that is read when the menu opens; the menu has two entries,
  "Start with Windows" and "Quit").

## Impact

- **Code:** `src/UrDeck.Host`: `Program` (the guard, `--quit`, the `--autostart` marker), `App` (the second tray
  entry), `TrayIcon` (checked entries), a new autostart class over the registry, `UrDeck.Host.csproj` (plugins in the
  publish output). `src/UrDeck.Engine`: `UrDeckLog` (date, cap) and the parts of the above that need no Windows API
  (reading the registered command, the instance name), so they are unit-tested.
- **New files:** `scripts/Install-Local.ps1`.
- **Registry:** one value, `UrDeck`, under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, written only when
  the user chooses the entry. Nothing is written at startup.
- **Config and SDK:** none. No plugin-visible change.
- **Dependencies:** none new.
- **Log format:** lines start with `[yyyy-MM-dd HH:mm:ss.fff]` instead of `[HH:mm:ss.fff]`. Anything that parses the
  log (tests, the notes in `AGENTS.md`) follows.
- **Docs:** README (install, start with Windows, `--quit`), `AGENTS.md` (the installed copy, the log format, the
  guard), `docs/BACKLOG.md` (current state, item 12, the distribution goals below), `docs/ROADMAP.md`.
- **Not in this change:** an installer, a release workflow or zip, the per-user data folder (item 15), auto-update,
  code signing, restarting after a crash, a delayed start, starting elevated. The owner wants to reach the Microsoft
  Store eventually and to sign releases at no cost if that is possible, and will research installer technology before
  one is chosen; this change picks none and keeps the registration behind one small interface so a packaged build can
  replace it.
