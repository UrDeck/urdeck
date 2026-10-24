## Purpose

Defines UrDeck's Microsoft Store listing: the same signed installer that GitHub releases carry, listed by address
under Store policy 10.2.9, and how each release reaches it.

## ADDED Requirements

### Requirement: Store Listing
UrDeck MUST be listed in the Microsoft Store as an installer the Store downloads from GitHub:

- The listing is an "EXE or MSI app" whose installer address is the versioned GitHub address of the latest stable
  release's `UrDeck-win-Setup.exe` (`https://github.com/UrDeck/urdeck/releases/download/v<version>/UrDeck-win-Setup.exe`)
- The install parameter is `--silent`, so installing from the Store shows no installer window
- A copy installed from the Store is the same as one installed from GitHub: per-user, in `%LocalAppData%\UrDeck`, with
  its data in `%APPDATA%\UrDeck`, and it updates itself (see app-updates, "Update Check"), as policy 10.2.9 allows
- The listing's privacy address is the app's privacy statement (see website, "App Privacy Statement")
- The listing's description says that UrDeck is meant for a secondary or case display, and runs on any monitor
- The listing's pictures come from the site's media (see site-media)

#### Scenario: Install from the Store
- **WHEN** a user installs UrDeck from the Microsoft Store
- **THEN** no installer window is shown, the deck starts with the sample page, and the copy is the one the listing's
  address points at

#### Scenario: Store copy updates itself
- **WHEN** a Store-installed `0.2.0` runs and `0.3.0` is published on GitHub
- **THEN** the copy offers the update as a GitHub-installed copy does

### Requirement: Releases Submitted To The Store
Each stable release MUST be submitted to the Store, and an installer address the Store has MUST keep working:

- After a stable release is published, its installer address is submitted to the listing; a prerelease is never
  submitted
- The certification notes say that the app updates itself from GitHub releases, that it installs and needs no driver,
  and how a tester sees it work without a case display
- A release whose installer address was submitted is never deleted, and its assets are never replaced. A bad release is
  followed by a higher one
- When the Store rejects a submission, the reason and the fix are recorded in the change or issue that handles it, and
  the release stays available on GitHub

#### Scenario: Stable release
- **WHEN** `0.3.0` is published on GitHub
- **THEN** the listing is resubmitted with the address of `v0.3.0`'s `UrDeck-win-Setup.exe`

#### Scenario: Prerelease
- **WHEN** `0.4.0-rc.1` is published on GitHub
- **THEN** the Store listing keeps pointing at the latest stable release
