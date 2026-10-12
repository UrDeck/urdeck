## 1. Autostart

- [x] 1.1 On the owner's machine, read `HKCU\...\Run` and `HKCU\...\Explorer\StartupApproved\Run` for an existing startup app before and after disabling it in Task Manager; confirm the layout in design decision 2 (first byte odd means disabled, absent means enabled) and note it in `docs/perf/start-with-windows.md`. If it differs, adjust decision 2 before going on
- [x] 1.2 `src/UrDeck.Engine/Startup`: the autostart state (On, Off, Another copy, Switched off in Windows) as a pure function of the Run data, the approval bytes and the running executable's path; parses the quoted path, ignores arguments, compares without case, treats unknown approval data as enabled
- [x] 1.3 Tests: no value; this copy; this copy in another case; another folder; data without quotes; data with arguments; approval bytes `02`, `03`, empty and absent; malformed data
- [x] 1.4 `src/UrDeck.Host`: `IAutostart` and `RunKeyAutostart` (read both values, switch on writes the Run value and removes the approval value, switch off removes the Run value); failures are logged as warnings and never thrown to the caller
- [x] 1.5 `Program`: recognise `--autostart` and log that Windows started the host at sign-in

## 2. Publish and local install

- [x] 2.1 Run `dotnet publish src/UrDeck.Host -c Release` once as it is and compare the folder with the build output; record what is missing (expected: `plugins/`) in `docs/perf/start-with-windows.md`
- [x] 2.2 `UrDeck.Host.csproj`: a target after `Publish` that copies the first-party plugin assemblies to `$(PublishDir)plugins`, sharing the item list with `CopyBuiltInPlugins`
- [x] 2.3 Copy the published folder elsewhere and run `--snapshot out.png --size 1100x3840` there: the page renders with all widgets and the log has no missing plugin or widget type
- [x] 2.4 `scripts/Install-Local.ps1` as in design decision 6: publish to a temporary folder, `--quit` a running copy, the two `robocopy` passes with their exclusions and exit-code mapping, the sample configuration only when none exists, restart or `-Start`, `-Destination`
- [x] 2.5 Try the script against a scratch destination: first install; update with an edited configuration, an added theme and an added plugin (all unchanged afterwards); update while running; a publish that fails leaves the destination untouched

## 3. Single instance and `--quit`

- [x] 3.1 Engine: the instance name derivation (the hash of the upper-cased full folder path) with tests for case, a trailing separator and two different folders
- [x] 3.2 `Program.Main`: `--quit` handling, then the guard (named mutex held for the process lifetime; a second copy logs one line and returns 0), both after `--snapshot` and before the XAML application
- [x] 3.3 The host creates the quit event and, when it is set, closes the main window on the UI thread; the wait ends cleanly on a normal exit
- [x] 3.4 `--quit`: returns 0 with no host running, signals and waits up to 10 s otherwise, logs and returns 1 on timeout
- [x] 3.5 Check by hand: second copy from the same folder exits and logs; a build-output copy runs beside an installed one; `--snapshot` works while the host runs; after killing the host from Task Manager a new one starts; `--quit` leaves the same shutdown lines in the log as the tray's Quit

## 4. Tray entry

- [x] 4.1 `TrayMenuEntry` gains the optional checked callback; `ShowMenu` reads it when the menu opens and adds the check mark
- [x] 4.2 `App`: the menu is "Start with Windows" (checked when the state is On; the action switches on or off and logs the state before and after, naming a replaced executable) and "Quit"
- [x] 4.3 Check by hand: the entry toggles; Task Manager lists UrDeck and follows it; disabling it in Task Manager removes the check mark at the next opening, and choosing the entry enables it again; choosing it in a second copy repoints the registration

## 5. Log

- [x] 5.1 `UrDeckLog`: the dated prefix, the tracked size per path, rotation to `.1` at 5 MB at startup and while running, a failed rename swallowed and retried on a later write
- [x] 5.2 Tests: the prefix format; rotation when the cap is passed, with no line lost; a second rotation replaces `.1`; a file larger than the cap at startup; a locked file does not throw; the redirect used by other tests still works
- [x] 5.3 Update tests and docs that quote or match the old `[HH:mm:ss.fff]` prefix

## 6. On the panel (owner)

- [x] 6.1 Run `scripts/Install-Local.ps1`, copy the working `urdeck-config.json` from the build output into the installed folder once, start the installed copy and switch on "Start with Windows". Switch off HYTE Nexus's own start with Windows
- [x] 6.2 Cold boot, three times: the deck is on the Y70 with the right page without any action; note in `docs/perf/start-with-windows.md` the time from the first log line to the placement being verified, to the tray icon and to the first weather values, and any warning
- [x] 6.3 Sign out and in again, and restart Explorer, once each: one deck, one tray icon
- [x] 6.4 Sleep and resume after an autostart: as in `display-recovery`
- [x] 6.5 Build and run a development copy while the installed one runs, then run `scripts/Install-Local.ps1` again: the installed copy restarts with the same configuration, the development copy is untouched
- [x] 6.6 After a few days, note the size of `urdeck.log` per day in the perf note and say whether 5 MB should change (Waived by the owner: the PC is shut down every night, so the log does not grow over long runs; every line is dated, so the growth can be read back from `urdeck.log` and `urdeck.log.1` at any time and 5 MB stays.)

## 7. Build checks and docs

- [x] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, `openspec validate --all --strict`
- [x] 7.2 README: installing with the script, "Start with Windows", `--quit`, one instance per folder, the log's date and cap
- [x] 7.3 `AGENTS.md`: the installed copy and the script (and that the build output is now only for development), the log format, the guard and `--quit` in the gotchas
- [x] 7.4 `docs/BACKLOG.md`: current state, item 12 (what is done; what the installer still needs: a manifest of installed files, the per-user data folder, the single-instance message for the editor), and under item 12 the owner's distribution goals: the Microsoft Store as an eventual target, signing at no cost if possible, and research before an installer technology is chosen. Correct the stale "Next: `display-recovery`" line
- [x] 7.5 `docs/ROADMAP.md`: tick "Start with Windows" in milestone 1 once section 6 passes
- [x] 7.6 Archive the change when everything above is verified
