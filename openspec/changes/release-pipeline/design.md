## Context

The host is already self-contained and unpackaged (`WindowsPackageType=None`, `WindowsAppSDKSelfContained`), and
`dotnet publish` produces a complete folder with `plugins/` (distribution, "Published Application Folder").
`per-user-data-folder` moves the user's files out of that folder, which is what makes a replaceable application
folder possible. Nothing stamps a version today. `UrDeck.Sdk.csproj` sets its own `Version` and keeps `AssemblyVersion`
fixed on purpose, because plugins bind to it. CI (`ci.yml`) builds and tests on `windows-latest`. Velopack's layout:
`%LocalAppData%\{packId}\current\` is the application, replaced on update; the root also holds `Update.exe` and a stub
`UrDeck.Host.exe` that launches `current`. Velopack's own guidance puts settings that must survive an uninstall in
`%AppData%\{packId}`, which matches the data folder.

## Goals / Non-Goals

**Goals:**
- One manual action makes a complete draft release, and nothing reaches users before the owner publishes it.
- The update path can be tested end to end on the owner's PC before 0.1 is published.
- The engine takes no dependency on Velopack. The installer is the host's business.

**Non-Goals:**
- Signing. The workflow has one place where signing goes in (between publish and pack), and `store-listing` fills it.
- A channel switch in the UI, a "check now" entry, or release notes shown in the app.
- Changing `scripts/Install-Local.ps1` into a Velopack install. It stays the quick way to put the current sources on
  the owner's PC.

## Decisions

**1. Start by hand, end in a draft.** The workflow runs on `workflow_dispatch` with a `version` input, builds
everything, and creates a draft release with `gh release create v<version> --draft --target <sha> --generate-notes`.
*Rejected:* triggering on a pushed tag or on `release: published`. Either way the release is visible, and its feed is
read by installed copies, before its assets exist, and a failed build leaves a tag or a public release to clean up. A
draft is invisible to Velopack's GitHub source and to visitors, so the owner gets a review step for free. The SignPath
approval of `store-listing` also fits in the middle of the same run.
The draft creates the tag when published, from `--target`, so a later push to `main` cannot move what is released.

**2. The app's version travels in `UrDeckAppVersion`, not `Version`.** A property given on the command line is global
in MSBuild and flows to every referenced project, so `-p:Version=0.2.0` on the host's publish would also stamp
`UrDeck.Sdk` and break its deliberate versioning. `Directory.Build.props` sets `Version` to `$(UrDeckAppVersion)` when
it is given, and to `0.0.0-dev` otherwise, for every project outside `sdk/`. `UrDeck.Sdk.csproj` and the analyzer keep
their own values. The workflow also passes `SourceRevisionId=$GITHUB_SHA`, so the informational version is
`0.2.0+<sha>`. The version rule (`^\d+\.\d+\.\d+(-rc\.\d+)?$`, higher than the latest release by SemVer precedence) is
checked in the first step with a short script, before anything is built.

**3. Velopack, pinned in two places that must match.** The `Velopack` NuGet package (host only) and the `vpk` tool in
`dotnet-tools.json` at the same version. Dependabot updates the package; the tool is bumped in the same PR (a task adds
a note to the dependabot group). Pack options: `--packId UrDeck`, `--packTitle UrDeck`, `--mainExe UrDeck.Host.exe`,
`--icon`, Start menu shortcut only, `--packVersion` from the input. Before packing, `vpk download github --pre` fetches
the newest release so a delta can be made. The packed files are uploaded by `gh release create`, not `vpk upload`, so
generated notes and the draft come from one command.

**4. `VelopackApp.Build().Run()` is the first line of `Program.Main`.** Velopack runs the app with hook arguments at
install, update and uninstall, and expects to handle them and exit before the app does anything. It therefore runs
before the data folder is resolved, before `--snapshot` and before the instance guard. The hooks:
- **Before uninstall** removes the Run value when it points at this copy, through the existing `IAutostart` (state
  "On"), and closes a running deck through the existing `--quit` path.
- **After install / after update** do nothing. The installer starts the app, and first run is the data folder's job.

**5. The update service lives in the host, behind `IUpdateService`.** Its states are none, checking, downloading and
ready(version). It wraps `UpdateManager` with a `GithubSource` for `https://github.com/UrDeck/urdeck`, with prereleases
when the running version has a label.
- `UpdateManager.IsInstalled` false (a build output, a plain publish) means the service is never started.
- A dispatcher-independent timer runs the first check 60 s after start and then every 24 h. A check is skipped while
  `updates` is `off`.
- The tray's update entry reads the state when the menu opens (the tray-icon delta adds "shown" and "label"
  callbacks). Its action calls `ApplyUpdatesAndRestart`.
- On Quit with an update ready, the host calls `WaitExitThenApplyUpdates(silent, restart: false)` before closing.
- `URDECK_UPDATE_SOURCE`, when set, replaces the GitHub source with a local folder or URL. It exists to test the path
  on the owner's PC without publishing a release, and is documented in `AGENTS.md`, not for users.
*Alternative:* auto-apply at the next start without asking. That is less visible, but the deck would restart by itself
right after sign-in, which on a case display looks like a crash.

**6. The portable release asks Velopack where its root is.** After `Run()`, `VelopackLocator.Current` says whether the
copy is portable and gives `RootAppDir`. `Program` passes that root to `DataFolder.Resolve` as an optional
portable-release root. The order is: marker, then portable release, then per-user. The engine sees a folder, not
Velopack.

**7. `updates` is a configuration key read leniently,** like `motion`: `auto` or `off`, anything else is `auto` with a
warning. It lives on `UrDeckConfig` in the engine so the configuration documentation and a later editor see it like
any other key.

**8. Notices and licence are content of the host project,** linked from the repository root with
`CopyToPublishDirectory`. `THIRD-PARTY-NOTICES.md` is audited against the published folder: the .NET runtime (MIT),
the Windows App SDK (its licence), SkiaSharp and its native Skia (MIT and BSD), Velopack (MIT), and what is listed
today.

## Risks / Trade-offs

- **Unsigned 0.1 meets SmartScreen**, and possibly antivirus heuristics on a self-extracting `Setup.exe`. → The README's
  install section shows the "More info → Run anyway" path and says signing follows. The site keeps "coming soon"
  (website, "Download State") until a signed release exists, so the unsigned build reaches testers through GitHub
  only.
- **A Velopack package and `vpk` of different versions** produce a package the app cannot apply. → The workflow
  compares the two versions and fails before packing.
- **Two per-user copies** (the owner's `Install-Local` copy in `%LOCALAPPDATA%\Programs\UrDeck` and a Velopack install)
  share `%APPDATA%\UrDeck`. Only one runs (single instance), but the Run key may point at either. → `CONTRIBUTING.md`
  says to uninstall the script's copy (`--quit`, delete the folder) before testing the installer.
- **GitHub's unauthenticated API limit** (60 requests an hour per address) is far above one check a day, but a shared
  address (an office) could hit it. → A failed check waits for the next one and the deck is never affected.
- **The delta comes from the newest release, prereleases included.** A stable copy two versions behind downloads the
  full package. → Acceptable at this size; Velopack falls back by itself.
- **Self-contained means a .NET security update needs a release.** → A Dependabot bump of the runtime is released as a
  patch.

## Migration Plan

1. Merge after `per-user-data-folder`, then archive that change first (this change's `data-folder` delta modifies a
   requirement that change adds).
2. Before 0.1: pack locally, install with `Setup.exe` on the owner's PC, then pack a higher version into a local folder
   and point `URDECK_UPDATE_SOURCE` at it to check the update, the apply on quit and the uninstall.
3. Add the tag ruleset, run the workflow with `0.1.0`, install from the draft's `Setup.exe` on a second account, and
   publish.
4. Rollback of a bad release: delete the release and its tag, as long as it was never submitted to the Store
   (`signing-and-store` forbids deleting one that was). Installed copies that already updated stay on it until a
   higher version is released (the update check only moves forward).
