## Why

Nobody can run UrDeck without building it from source, and milestone 2 is "somebody else downloads a release and has a
working page in five minutes". The release decisions of 2026-10-24 (backlog item 12) settle the shape: 0.x versions
taken from the git tag, a self-contained per-user installer made with Velopack that updates itself from the GitHub
releases, and the user's files in `%APPDATA%\UrDeck` (the `per-user-data-folder` change, which this one needs). This
change builds the release and the in-app update. Signing and the Microsoft Store follow in `store-listing`, because
SignPath Foundation signs only a project that already has a release.

## What Changes

- **A release workflow.** Started by hand from the Actions tab with a version (`0.1.0`, `0.2.0-rc.1`). It checks that
  the version is new and SemVer, builds and tests on `windows-latest`, publishes the host with that version, packs it
  with Velopack (with deltas from the previous release), tags `main`'s commit `v<version>`, and creates a **draft**
  GitHub release with generated notes and the assets attached. The owner reads the draft and publishes it. A version
  with a prerelease label becomes a GitHub prerelease. No commit is made for a release.
- **The version comes from the tag.** A build without one is `0.0.0-dev`. The app's version is stamped on the host,
  the engine and the first-party plugins, never on the SDK, whose version follows its own changes. The log's first
  lines and the settings window show it.
- **Release assets:** `UrDeck-win-Setup.exe` (per-user, no administrator rights, installs into
  `%LocalAppData%\UrDeck` and starts the deck), `UrDeck-win-Portable.zip`, the Velopack packages and feed, and the
  licence and third-party notices inside the installed folder.
- **Installing and uninstalling.** The installer adds a Start menu shortcut and no desktop shortcut. Uninstalling
  removes the Start with Windows registration and the application folder and leaves `%APPDATA%\UrDeck`.
- **Updates inside the app.** An installed copy checks the GitHub releases at start and once a day, downloads a newer
  release in the background and offers it in the tray ("Restart to update to 0.2.0"). If the user does not restart, the
  update is applied when the deck quits. A copy on a prerelease also takes prereleases; a stable copy takes only
  stable releases. `"updates": "off"` in the configuration turns the check off. Development builds never check.
- **A portable release.** The portable zip keeps its data in the folder it was unpacked into, above the folder an
  update replaces.
- **The tray menu** gains the update entry, shown only while an update is ready.

## Capabilities

### New Capabilities
- `release`: how a release is made: the workflow, its checks, the version rule, the stamped version, the assets and
  the draft release.
- `app-updates`: what the installer does, how an installed copy finds, downloads and applies an update, the
  `updates` setting, and uninstalling.

### Modified Capabilities
- `distribution`: "Published Application Folder" (the licence and notices in the folder, the stamped version).
- `tray-icon`: "Tray Menu" (the update entry).
- `data-folder`: "Data Folder Location" (the portable release's data folder). This capability is added by
  `per-user-data-folder`, which must be archived first.

## Impact

- **Code:** `src/UrDeck.Host`: `Program` (`VelopackApp.Build().Run()` first, with the uninstall hook), an update
  service over Velopack's `UpdateManager` behind a small interface, `App` (the tray entry, applying on quit), the
  settings window (the version). `src/UrDeck.Engine`: the `updates` key, the version helper, the portable-release case of
  the data folder resolver.
- **Build:** `Directory.Build.props` (`0.0.0-dev` default; an `UrDeckAppVersion` property that only the app's projects
  take), `UrDeck.Host.csproj` (notices and licence in the publish output), `Directory.Packages.props` (the `Velopack`
  package), `dotnet-tools.json` (`vpk`).
- **CI:** `.github/workflows/release.yml` (new, `workflow_dispatch` only, `contents: write`), a tag ruleset on `v*`.
- **Network:** a new request: the installed app reads the GitHub releases of `UrDeck/urdeck` once a day. The README and
  the site's privacy text say so.
- **Docs:** README (installing, updating, uninstalling, the SmartScreen warning of an unsigned release), the site's
  user documentation (installing, the `updates` key), `CONTRIBUTING.md` (making a release), `AGENTS.md` (the release
  commands), `docs/BACKLOG.md`.
- **Not in this change:** signing, the code-signing policy page and the Microsoft Store (`store-listing`); the site's
  download button, which stays "coming soon" until a signed release exists (website, "Download State"); winget;
  release-please and a `CHANGELOG.md`; an MSI.
