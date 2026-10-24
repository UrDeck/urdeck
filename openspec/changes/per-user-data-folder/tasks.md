## 1. Data folder resolver

- [ ] 1.1 `src/UrDeck.Engine/Startup`: `DataFolder.Resolve(applicationFolder, roamingAppData)` returning the root, the mode, the reason and the derived paths (config, sample, backups folder, log, themes, backgrounds, user plugins, first-party plugins, icon cache), as design decision 1 says; no Windows API, no file written
- [ ] 1.2 Tests (run on Linux): marker present (portable, root = application folder); marker absent (per-user, root = `<roaming>/UrDeck`); marker that is a folder rather than a file (not portable); user and first-party plugin folders are the same path in portable mode and different in per-user mode; the log line text for each mode
- [ ] 1.3 `UrDeckLog`: a process-wide default path set once at startup (design decision 4); before it is set, lines go to the debug output only. Tests: the `AsyncLocal` redirect still wins; nothing is written next to the executable

## 2. Configuration: version, migrations, first run

- [ ] 2.1 `UrDeckConfig.Version`, written first, always the current version (1) on save
- [ ] 2.2 `ConfigStore`: read `version` from the JSON before binding; treat absent as 1, and a non-positive or non-integer value as 1 with a warning; run the ordered migrations on the `JsonObject`; log each one that ran
- [ ] 2.3 `ConfigStore`: the first save of a migrated file copies the original bytes to `urdeck-config.v<from>.json` first; a file newer than the host makes `Save` a logged no-op for the rest of the run, the hot reload included
- [ ] 2.4 `ConfigStore` first run (design decision 6): with "create", copy the sample when it exists, otherwise write an empty-page default, and log `Configuration created at ...`; with "do not create", parse the sample in memory and write nothing
- [ ] 2.5 Tests: no version → loads, saved with `"version": 1`; `"version": "x"` and `0` → warning, read as 1; a test-only migration 1→2 with the current version raised to 2 → migrated content, backup holds the original bytes, log names the migration; `"version": 2` with an unknown key against a version-1 host → warning, the file is byte for byte unchanged after `Save` and after a remembered display; first run with a sample, without a sample, and with an existing file (never replaced); "do not create" leaves no file behind

## 3. Plugins from two folders

- [ ] 3.1 `WidgetRegistry.Register`: keep the first registration of a type id and log a warning naming the dropped plugin's file; check that the provider registry's message also names the file
- [ ] 3.2 `WidgetPluginLoader.ScanAndLoadPlugins` takes the folders in priority order: load the application folder's first, create only the user folder, scan one folder once when both are the same path, watch each folder with the shared 500 ms debounce; the icon cache folder comes from the caller (no default derived from the executable's folder)
- [ ] 3.3 Tests with two temporary folders and the test plugin assemblies: a type in both folders → the first folder's is used and the warning names the other file; a provider id in both → the same; a DLL added to the second folder while loaded → reload picks it up; the same folder passed twice → loaded once

## 4. Host wiring

- [ ] 4.1 `Program.Main`: resolve the data folder first, set the log path, write the `Data folder: ...` line, then `--snapshot`, `--quit` and the guard, all with that value; if the per-user root cannot be created or written, write the reason to the debug output and return 1
- [ ] 4.2 `SingleInstance` and `--quit` keyed on the data folder (design decision 8); update the `InstanceNames` doc comments
- [ ] 4.3 `HostContext` takes the `DataFolder`: config with the sample and the create option (the deck creates; `--snapshot` and `--demo` do not), themes, backgrounds, both plugin folders, the icon cache; `AppDirectory` is replaced by the data folder as the base of a background picture's relative path (`SnapshotCommand`, `MainWindow`, `BackgroundRender`)
- [ ] 4.4 Tray: the "Open data folder" entry between Settings and Start with Windows (design decision 9); a failure logs a warning
- [ ] 4.5 `UrDeck.Host.csproj`: rename the tracked `urdeck-config.json` to `urdeck-config.default.json` and copy it to the build and publish outputs; add `urdeck.portable` to the build output only (`CopyToPublishDirectory="Never"`); remove the copy that overwrote the working configuration
- [ ] 4.6 `grep` for `AppContext.BaseDirectory` in `src/`: every remaining use means the application folder (the icon, the Run key's executable), and a comment says so where it is not obvious

## 5. Local install script

- [ ] 5.1 `scripts/Install-Local.ps1`: stop protecting and seeding user files in the destination; add the one-time move from the destination to `%APPDATA%\UrDeck` when the destination has a `urdeck-config.json` and `%APPDATA%\UrDeck` has none (configuration, logs, `cache/`, `themes/`, `backgrounds/`, plugins the publish did not produce); when both exist, move nothing and name the old files; a failed move stops the script with the files where they were. Update the comment-based help
- [ ] 5.2 Try it against a scratch destination and a scratch roaming folder (a parameter for the roaming root, defaulting to `$env:APPDATA`): first install (no configuration written; the first start creates it); install over the old layout (files moved, same page); install over the old layout with a configuration already in the data folder (nothing moved, files named); update while running

## 6. Documentation

- [ ] 6.1 README and `site/src/content/docs/` (`index.mdx`, `themes.mdx`, `themes/quick-start.md`, `themes/motion.md`, `backgrounds.mdx` and any other page that says "next to the executable"): the data folder, portable mode, the tray entry, a relative picture path; `npm run build` in `site/` passes
- [ ] 6.2 `AGENTS.md`: where the log and the configuration are for a build (portable, `bin/`) and for the installed copy (`%APPDATA%\UrDeck`); replace the gotcha about the build overwriting the working configuration; deleting `display` from the configuration in the data folder
- [ ] 6.3 `docs/BACKLOG.md`: item 15 and current state

## 7. Verification

- [ ] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, `dotnet format urdeck.slnx --severity warn --verify-no-changes`, and the Linux test projects in WSL
- [ ] 7.2 From the build output: the host starts portable (log line), the deck shows the page, and nothing is created under `%APPDATA%\UrDeck`
- [ ] 7.3 Published copy in a scratch folder with no `%APPDATA%\UrDeck` (rename it aside first): `--snapshot` writes the sample page and leaves no configuration; a normal start creates the folder and the configuration from the sample and logs both; "Open data folder" opens it; a second copy from another scratch folder exits with "another copy is running"; `--quit` from either folder closes the host
- [ ] 7.4 With the published copy running, a portable build-output copy starts beside it, and `--quit` from the build output leaves the published one running
- [ ] 7.5 The owner's daily copy migrated with the script (task 5.2 on the real installation): same page, theme, backgrounds and log history; the deck survives a restart and a sign-in with Start with Windows on
- [ ] 7.6 `openspec validate --all --strict`
