## Context

Every user-owned path is derived from `AppContext.BaseDirectory` today, in six places:

- `HostContext` (the configuration, `themes/`, `backgrounds/`, `plugins/`, and `AppDirectory`, which a background
  picture's relative path starts from)
- `UrDeckLog.DefaultPath`
- `WidgetPluginLoader` (the default `cache/icons`)
- `Program` (the single-instance key and `--quit`)
- `SnapshotCommand` (through `HostContext.AppDirectory`)
- `BackgroundRender`

`ConfigStore` writes a default file when none exists, from its constructor, which `--snapshot` also runs. The host
build copies the tracked `src/UrDeck.Host/urdeck-config.json` over the build output's copy whenever the source is newer
(an `AGENTS.md` gotcha). The plugin loader scans one folder. `WidgetRegistry.Register` lets a later widget type replace
an earlier one, while the provider registry keeps the first claim. The installer that follows (`release-pipeline`,
Velopack) owns `%LocalAppData%\UrDeck`; see proposal.md and backlog item 12.

## Goals / Non-Goals

**Goals:**
- One place decides the data folder, and it is decided before the first log line.
- The engine's parts stay testable on Linux: the resolver, the configuration's versioning and the plugin precedence
  take folders as arguments and touch no Windows API.
- A development build behaves as it does today: everything in `bin/`, beside an installed copy.

**Non-Goals:**
- Changing `urdeck-cli` or `PreviewRenderer`. They take their folders as arguments and are not hosts.
- An override of the data folder by environment variable or command line. The marker covers development, and a test
  passes folders directly.
- Preserving unknown top-level configuration keys across a save. A newer file is protected by never writing it, which
  is enough until a second format version exists.

## Decisions

**1. A `DataFolder` value resolved once, in the engine.** `DataFolder.Resolve(applicationFolder, roamingAppData)`
returns the root, the application folder, the mode and the reason, and the paths derived from them (config, sample,
log, themes, backgrounds, user plugins, first-party plugins, icon cache). `Program.Main` calls it first with
`AppContext.BaseDirectory` and `Environment.SpecialFolder.ApplicationData`, sets the log path, and hands the value to
`HostContext`, `SnapshotCommand`, `SingleInstance` and `App`. `AppContext.BaseDirectory` then remains only where the
application folder is meant (the icon, the Run key's executable).
*Alternative:* a static `Paths` class read from anywhere. That is easier to reach, but it hides a global that tests
would have to reset, and it would let engine code depend on the host's startup order.

**2. Portable mode is a marker file, `urdeck.portable`.** It is explicit, survives copying the folder, and cannot
appear by accident.
*Rejected:* "a `urdeck-config.json` next to the executable means portable". A user who copied a config file into the
installed folder would silently flip modes, and the published folder would have to avoid shipping one forever.
*Rejected:* an environment variable. A copy started by the Run key or by Explorer would not see it.
The build output gets the marker through an item with `CopyToOutputDirectory` and `CopyToPublishDirectory="Never"`, so
`dotnet publish` produces a per-user copy with no extra step.

**3. Roaming (`%APPDATA%`), with everything in one folder.** `%LocalAppData%\UrDeck` belongs to Velopack and is removed
on uninstall. `%APPDATA%` is where users and support answers look for an app's settings, and OneDrive's folder
redirection does not move it. On a home PC nothing roams.
*Alternative:* the configuration in Roaming and `cache/` and the log in `%LocalAppData%\UrDeck.Data`. That is the
textbook split, but it means two folders to explain and a second name. The icon cache is small, and the log is capped
at 10 MB.

**4. The log path is set once at startup.** `UrDeckLog` gains a way to set the process-wide default path, used once by
`Program.Main` right after resolving and before the first line. The `AsyncLocal` redirect that tests use is unchanged.
Lines written before it is set (none in practice) go to the debug output only, never to the application folder.

**5. The configuration's version is read from the JSON before it is bound.** `ConfigStore` parses the text into a
`JsonNode`, reads `version`, runs the migrations from that version to the current one on the node, then deserializes
the result as today. `UrDeckConfig` gains `Version`, written first and always as the current version.
- The migration list is an ordered set of `(from, Action<JsonObject>)`. It is empty for version 1. Tests inject their
  own list through an internal constructor (the engine's `InternalsVisibleTo` already covers the test project).
- A migrated store remembers the version it came from. Its first `Save` copies the original bytes to
  `urdeck-config.v<from>.json`, then writes.
- A file newer than the host makes the store read-only: `Save` logs once and returns. The host's other writes (the
  remembered display, saving on exit) already go through `Save`, so one guard covers them.
*Alternative:* a typed migration per class version. The JSON node is what changes between versions, and a typed model
would need the old shape kept in code for each version.

**6. First run lives in `ConfigStore`, behind an option.** The constructor takes the sample's path and whether it may
create the file. The deck passes "create". `--snapshot` and `--demo` pass "do not create" and get the sample, parsed in
memory, when the file is missing. The current behavior (write a default file from the constructor) remains only as the
case where the sample is missing too.

**7. Plugins come from an ordered list of folders.** `ScanAndLoadPlugins` takes the folders in priority order
(application, then data; the same folder once in portable mode), creates only the user folder, and watches each folder
with one shared debounce. `WidgetRegistry.Register` keeps the first registration of a type id and logs the file of the
one it drops, so widget types follow the same "first wins" rule providers already follow. Within a folder the order
stays by file name.

**8. The single-instance key is the data folder.** `InstanceNames` keeps its scheme (a hash of the upper-cased full
path) and is given the data folder's path. Two per-user copies therefore exclude each other, which is right: they would
otherwise fight over one configuration file.

**9. The tray entry starts Explorer on the folder** through the shell (`explorer.exe "<root>"`), and creates the folder
first if it is missing. A failure logs a warning.

**10. The one-time move lives in `Install-Local.ps1`, not in the app.** Only the owner's daily copy has the old layout,
because there is no public release yet. The app keeps no code for a layout it will never ship. The script decides
which plugins are the user's by comparing against the published `plugins/`, which it already does.

## Risks / Trade-offs

- **The mutex name changes with this release.** An old host and a new host from the same folder do not see each other
  once. → `Install-Local.ps1` quits the running copy with the *installed* executable's `--quit` before replacing it,
  which still uses the old key. A copy started by hand during that window is the only case left.
- **Roaming profiles in a managed environment** would carry the icon cache and the log. → Both are small and capped.
  Revisit only if somebody reports it.
- **A newer configuration is never saved,** so a display chosen while an older build runs is not remembered. → The
  warning says why. Running an older build is a downgrade case.
- **Plugins in two places** can confuse someone who drops a newer first-party plugin into the data folder and sees the
  old one used. → The warning names both files. Developers use a portable build, where the folders are one.
- **The data folder cannot be written** (a broken profile or a policy). → The host exits with 1 and writes the reason
  to the debug output. Falling back to the application folder would hide the problem and break the first update.

## Migration Plan

1. Merge. Development builds are unaffected: the marker keeps them portable, and the first build seeds
   `urdeck-config.json` from the sample once.
2. The owner runs `pwsh scripts/Install-Local.ps1`. It quits the old copy, moves the configuration, log, themes,
   backgrounds, cache and user plugins to `%APPDATA%\UrDeck`, installs the new build and starts it. The first log line
   names the data folder.
3. Rollback: install the previous commit's build with `-Destination` set to a new folder and copy the files back from
   `%APPDATA%\UrDeck`. An older build never reads `%APPDATA%\UrDeck`.

## Open Questions

- Velopack's portable zip replaces its `current` folder on a portable update, so its data cannot live next to the
  executable. `release-pipeline` adds that case: the resolver then takes the portable root from Velopack (the folder
  above `current`). Nothing in this change depends on it, and the marker stays the rule for every other copy.
