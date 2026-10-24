## Why

Everything the user owns lives next to the executable today: `urdeck-config.json`, `urdeck.log`, `themes/`,
`backgrounds/`, user plugins in `plugins/` and `cache/`. The installer chosen on 2026-10-24 (backlog item 12) is
Velopack, which installs per user into `%LocalAppData%\UrDeck`, replaces that folder on every update and removes it on
uninstall. With the user's files inside it, the first update would wipe the user's page. This change moves them to a
per-user data folder, `%APPDATA%\UrDeck`. It comes before the release workflow (`release-pipeline`) because the first
public release must already write its data where later releases look for it. The same moment is the last cheap chance
to give the configuration a format version, so that every later format change can carry a migration (item 15).

## What Changes

- **A data folder, resolved in one place.** A resolver decides where the user's files live: `%APPDATA%\UrDeck` (the
  per-user mode), or the application folder when a `urdeck.portable` marker file sits next to the executable (the
  portable mode). Every path the host uses for the user's files comes from it. The log says which folder and why.
- **What moves.** `urdeck-config.json`, `urdeck.log` and `urdeck.log.1`, `themes/`, `backgrounds/`, user plugins
  (`plugins/`) and `cache/` live in the data folder. A background picture's relative `path` starts there too.
- **Two plugin folders.** First-party plugins stay in the application folder's `plugins/` and update with the app;
  plugins the user adds go in the data folder's `plugins/`. Both are scanned and watched. When both folders register
  the same widget type or provider id, the application folder's wins and a warning names the other file. In portable
  mode the two folders are the same folder.
- **First run.** With no configuration in the data folder, the host creates the folder and copies the sample page
  (`urdeck-config.default.json`, shipped in the application folder) to `urdeck-config.json`, and logs where. `--snapshot`
  never creates the folder or the file: it draws the sample page from memory.
- **"Open data folder" in the tray menu**, between Settings and Start with Windows, opens the data folder in Explorer.
- **One host per data folder.** The single-instance guard and `--quit` are keyed on the data folder instead of the
  application folder: two installed copies sharing `%APPDATA%\UrDeck` cannot both run, and a portable development
  build still runs beside the installed copy.
- **A configuration format version.** The file gains `"version": 1`. A file without one is version 1. A file from a
  newer version is read as well as possible and never written, so its unknown settings survive. Migrations from one
  version to the next run in order on the file's JSON before it is read, with the original kept as a backup. Version
  1 has none yet; the mechanism is tested with a migration defined only in the tests.
- **The build output is portable.** The host build places `urdeck.portable` next to the executable, so a development
  build keeps its data in `bin/` as today. The publish output has no marker. The build no longer copies the tracked
  sample over the working `urdeck-config.json`: it ships the sample as `urdeck-config.default.json`, and a first run
  seeds the working file from it. **BREAKING** for the owner's daily copy: `scripts/Install-Local.ps1` moves an existing
  installation's user files to `%APPDATA%\UrDeck` once, when that folder has no configuration yet.

## Capabilities

### New Capabilities
- `data-folder`: where the user's files live: the per-user and portable modes, the marker, what lives in the data
  folder and what in the application folder, creating the folder and seeding the configuration on first run, and the
  log line that names the folder.

### Modified Capabilities
- `host-shell`: "Plugin Directory Setup" (two folders, which one wins), "Configuration Management" (location, the
  first run, the `version` key and migrations), "Application Entry Point" (the log's location, `--snapshot` without a
  configuration), "Single Instance" and "Quit Command" (keyed on the data folder).
- `theme`: "User Themes" (the `themes` folder is in the data folder).
- `backgrounds`: "Shader Backgrounds" (the `backgrounds` folder is in the data folder) and "Image Backgrounds" (a
  relative `path` starts at the data folder).
- `background-manifest`: "Background Folder" (in the data folder).
- `distribution`: "Published Application Folder" (ships `urdeck-config.default.json`, no marker) and "Local Install"
  (user files are in the data folder; the one-time move).
- `tray-icon`: "Tray Menu" (the "Open data folder" entry).

## Impact

- **Code:** `src/UrDeck.Engine`: a new data folder resolver (unit-tested with injected folders, so it runs on Linux),
  `ConfigStore` (the version, migrations, backup, never writing a newer file, seeding), `UrDeckLog` (its path set at
  startup instead of fixed to the executable's folder), `WidgetPluginLoader` (two folders, precedence),
  `WidgetRegistry` (a duplicate widget type no longer replaces the first). `src/UrDeck.Host`: `Program` (resolve the
  data folder before the guard and `--quit`), `HostContext`, `SnapshotCommand`, `App` (the tray entry),
  `UrDeck.Host.csproj` (the marker in the build output only, the sample renamed).
- **Scripts:** `scripts/Install-Local.ps1` (no longer seeds or protects user files in the application folder; the
  one-time move).
- **Not changed:** `urdeck-cli` and `PreviewRenderer` keep taking their folders as arguments; the `display` and every
  other configuration key keep their meaning; plugins and the SDK see no change.
- **Docs:** README and the site's user documentation (every "next to the executable" for the configuration, themes,
  backgrounds, plugins and the log), `AGENTS.md` (where the log and the configuration are, the build no longer
  overwriting the working configuration), `docs/BACKLOG.md` (item 15, current state).
- **Not in this change:** the installer, Velopack and its portable zip (`release-pipeline` decides how that zip
  selects the portable mode), a first page that suits a landscape monitor (item 15), a schema for the configuration
  file, moving data between machines.
