## 1. Version

- [ ] 1.1 `Directory.Build.props`: `Version` is `$(UrDeckAppVersion)` when given, else `0.0.0-dev`, for every project outside `sdk/` (design decision 2); `UrDeck.Sdk` and the analyzer keep their own
- [ ] 1.2 Check by building with `-p:UrDeckAppVersion=0.2.0-rc.1 -p:SourceRevisionId=abc`: the host, the engine and the plugins have file version `0.2.0.0` and product version `0.2.0-rc.1+abc`; `UrDeck.Sdk.dll` is unchanged from a build without the property
- [ ] 1.3 `Program`: log `UrDeck <informational version>` among the first lines; the settings window shows the version
- [ ] 1.4 A version check script (`scripts/Test-ReleaseVersion.ps1`): the form `^\d+\.\d+\.\d+(-rc\.\d+)?$`, higher than a given latest version by SemVer precedence; tests for `0.1`, `v0.2.0`, `0.2.0-rc.1` after `0.1.0`, `0.2.0` after `0.2.0-rc.1`, and a lower version

## 2. Velopack in the host

- [ ] 2.1 Add `Velopack` to `Directory.Packages.props` and the host, and `vpk` at the same version to `dotnet-tools.json`; a dependabot group for `Velopack*` with a comment that the tool is bumped with it
- [ ] 2.2 `Program.Main`: `VelopackApp.Build()` with the before-uninstall hook (remove this copy's Run value through `IAutostart`, quit a running deck) and `.Run()` as the first statement (design decision 4)
- [ ] 2.3 Pass Velopack's portable root to `DataFolder.Resolve` (design decision 6); engine tests for the order marker > portable release > per-user and for the `(portable release)` log line
- [ ] 2.4 `UrDeckConfig.Updates` (`auto` / `off`, lenient, warning on another value) with tests
- [ ] 2.5 `IUpdateService` and its Velopack implementation (design decision 5): not started when not installed; first check after 60 s, then every 24 h; prereleases when the running version has a label; download in the background; the states; each step logged; failures logged as warnings; `URDECK_UPDATE_SOURCE` override
- [ ] 2.6 Unit tests of the scheduling and state logic against a fake source (no network): off skips the check, a failure retries at the next tick, a ready update is reported once
- [ ] 2.7 Tray: entries can say whether they are shown and give their label when the menu opens; the "Restart to update to <version>" entry first; on Quit with an update ready, apply after exit without restarting

## 3. Published folder and notices

- [ ] 3.1 `UrDeck.Host.csproj`: `LICENSE`, `PLUGIN-EXCEPTION.md` and `THIRD-PARTY-NOTICES.md` in the publish output
- [ ] 3.2 Audit the published folder against `THIRD-PARTY-NOTICES.md`; add the .NET runtime, the Windows App SDK, SkiaSharp with Skia, and Velopack with their licence texts

## 4. Pack locally

- [ ] 4.1 `scripts/Pack-Release.ps1 -Version <v> [-Previous <folder>]`: publish with the version, `vpk pack` with the options of design decision 3 into `artifacts/release/`; the workflow calls the same script
- [ ] 4.2 On the owner's PC (after removing the `Install-Local` copy): install with the packed `Setup.exe` (no elevation, Start menu shortcut, no desktop shortcut, the deck starts with the sample page or the existing data folder); `Setup.exe --silent` on a second account
- [ ] 4.3 Pack a higher version into a folder, set `URDECK_UPDATE_SOURCE` to it, and check: the tray offers the update within the first check, choosing it restarts as the new version with the same page; again with Quit instead (the next start is the new version); `"updates": "off"` makes no check
- [ ] 4.4 Uninstall from Windows' installed apps with Start with Windows on: the Run value is gone, the application folder and the shortcut are gone, `%APPDATA%\UrDeck` is unchanged
- [ ] 4.5 Unpack the portable zip into a scratch folder: the data folder is that folder (log line), and an update from the local source keeps the edited page

## 5. Release workflow

- [ ] 5.1 `.github/workflows/release.yml`: `workflow_dispatch` with `version`, refuse a ref other than `main`; permissions `contents: write`; the version check against the latest release and existing tags and drafts; build and test on `windows-latest`; the Velopack and `vpk` version comparison; `vpk download github --pre` for the delta base; `Pack-Release.ps1`; `gh release create v<version> --draft --target <sha> --generate-notes --title "UrDeck <version>"` with `--prerelease` for a label, uploading the packed files
- [ ] 5.2 A repository ruleset on tags `v*`: creation, update and deletion limited to the owner and the GitHub Actions integration
- [ ] 5.3 A dry run with `0.0.1-rc.1` on the real repository: the draft has the assets and generated notes and is marked prerelease; delete the draft afterwards

## 6. Documentation

- [ ] 6.1 README: installing (Setup.exe, the SmartScreen "More info → Run anyway" path of an unsigned release), updating, uninstalling, the portable zip, the update check as a network request and `"updates": "off"`
- [ ] 6.2 The site's user documentation: installing, the `updates` key in the configuration reference; the privacy statement names the update check; `npm run build` passes
- [ ] 6.3 `CONTRIBUTING.md`: how to make a release (run the workflow, read the draft, install it, publish); `AGENTS.md`: `Pack-Release.ps1`, `URDECK_UPDATE_SOURCE`, the Velopack and `vpk` versions moving together
- [ ] 6.4 `docs/BACKLOG.md`: item 12 and current state

## 7. Verification

- [ ] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, `dotnet format --verify-no-changes`, the Linux test projects, `openspec validate --all --strict`
- [ ] 7.2 Release `0.1.0` with the workflow: install the draft's `Setup.exe` on a second Windows account, a working page within five minutes, then publish the draft
