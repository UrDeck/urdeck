## Purpose

Defines how a release of UrDeck is made: the workflow that builds it, the version rule, what is stamped into the
binaries, and the draft GitHub release with its assets that the owner reviews and publishes.

## ADDED Requirements

### Requirement: Version Numbers
A release MUST have a SemVer 2.0 version, `MAJOR.MINOR.PATCH` with an optional prerelease label (`0.2.0-rc.1`):

- Until 1.0.0 every release is `0.y.z`. A prerelease label is used only for testers before a minor release, as
  `-rc.N` with N counting from 1
- The git tag of a release is `v` followed by the version, on a commit of `main`
- The version is not stored in a file in the repository. A build that is given no version is `0.0.0-dev`
- The Windows file version of a release is `MAJOR.MINOR.PATCH.0`, without the label
- The SDK (`sdk/`) does not take the app's version: its package and assembly versions follow its own changes

#### Scenario: Release version
- **WHEN** release `0.2.0-rc.1` is built
- **THEN** `UrDeck.Host.exe` has file version `0.2.0.0` and product version `0.2.0-rc.1+<commit>`, and the tag is
  `v0.2.0-rc.1`

#### Scenario: Local build
- **WHEN** the host is built with `dotnet build` and no version
- **THEN** its product version starts with `0.0.0-dev`

#### Scenario: SDK untouched
- **WHEN** release `0.3.0` is built
- **THEN** `UrDeck.Sdk.dll` in it has the same assembly and file version as a local build of the same commit

### Requirement: Version Shown
The running app MUST say which version it is:

- The log's first lines of a run include `UrDeck <product version>`
- The settings window shows the version

#### Scenario: Version in the log
- **WHEN** an installed `0.1.0` starts
- **THEN** the log has a line `UrDeck 0.1.0+<commit>` among the lines of that start

### Requirement: Release Workflow
A release MUST be made by one workflow that the owner starts by hand with the version:

- It runs only when started from GitHub's Actions tab or API (`workflow_dispatch`) with a `version` input, and only on
  `main`
- It refuses, before building, a version that is not SemVer as "Version Numbers" says, a version whose tag already
  exists, and a version that is not higher than the latest release's
- It builds the solution and runs the tests on Windows. A failing test stops it before anything is tagged or uploaded
- It publishes the host with the version, packs the release (see "Release Assets") with deltas from the previous
  release when one exists, and creates a **draft** release named `UrDeck <version>` for the tag `v<version>` on the
  commit it built, with GitHub's generated notes and the assets attached. Publishing the draft creates the tag
- It also refuses a version that already has a draft release
- A version with a prerelease label makes a GitHub prerelease
- It writes nothing to the repository's branches
- Only the owner and the workflow can create `v*` tags

#### Scenario: First release
- **WHEN** the workflow is started with `0.1.0` and no release exists
- **THEN** a draft release `UrDeck 0.1.0` for `v0.1.0` with the assets exists, and nothing was pushed to `main`

#### Scenario: Draft published
- **WHEN** the owner publishes the draft `UrDeck 0.1.0`
- **THEN** the tag `v0.1.0` exists on the commit the workflow built, even if `main` has moved since

#### Scenario: Invalid version
- **WHEN** the workflow is started with `0.1` or `v0.2.0`
- **THEN** it fails before building and names the expected form

#### Scenario: Version already released
- **WHEN** the workflow is started with a version whose tag exists
- **THEN** it fails before building and creates nothing

#### Scenario: Failing test
- **WHEN** a test fails during the workflow
- **THEN** no tag and no release are created

#### Scenario: Prerelease
- **WHEN** the workflow is started with `0.2.0-rc.1`
- **THEN** the draft release is marked as a prerelease

### Requirement: Release Assets
A release MUST carry what a user needs to install or run UrDeck and what an installed copy needs to update:

- `UrDeck-win-Setup.exe`, the per-user installer (see app-updates, "Installer")
- `UrDeck-win-Portable.zip`, the portable copy (see data-folder, "Data Folder Location")
- The full package of the version, a delta package from the previous release when there is one, and the feed that
  installed copies read
- Every asset is built from the published folder of the release's commit (see distribution, "Published Application
  Folder"), so the installer, the zip and the packages hold the same files

#### Scenario: Assets present
- **WHEN** a release is published
- **THEN** its assets include `UrDeck-win-Setup.exe`, `UrDeck-win-Portable.zip`, the full package and the feed

#### Scenario: Delta from the previous release
- **WHEN** release `0.2.0` is made after `0.1.0`
- **THEN** its assets include a delta package from `0.1.0`
