## MODIFIED Requirements

### Requirement: Download State
The landing page MUST offer the installer of the latest stable release:

- The primary download control is a plain link to `https://github.com/UrDeck/urdeck/releases/latest/download/UrDeck-win-Setup.exe`,
  so the file served is always the latest stable release's and the site's content need not change for it
- Next to it the page shows that release's version and publication date, what the installer needs (Windows 10 or 11, x64,
  no administrator rights), and links to the portable zip, the release notes and all releases
- While the release is not code signed, the page says that Windows SmartScreen may warn and how to continue
- A prerelease and a draft are never offered
- The version, date and links come from the GitHub Releases API at build time, never from the visitor's browser
- The site MUST be rebuilt and deployed when a release is published
- When the API cannot be read, or no stable release exists, the build MUST still succeed and the download control MUST
  lead to the repository's releases page without claiming a version

The control MUST NOT claim a download that does not exist: the build fails if a release was read but lacks
`UrDeck-win-Setup.exe`.

#### Scenario: A stable release exists
- **WHEN** `0.2.0` is the latest stable release and a visitor uses the download control
- **THEN** the browser downloads `UrDeck-win-Setup.exe` of `0.2.0`, and the page showed "0.2.0" and its date

#### Scenario: A newer release is published
- **WHEN** `0.3.0` is published
- **THEN** the site is rebuilt and shows `0.3.0`, and the download link is unchanged

#### Scenario: A prerelease is newer
- **WHEN** `0.3.0-rc.1` is the newest release and `0.2.0` the latest stable one
- **THEN** the page shows `0.2.0`

#### Scenario: The API is unreachable
- **WHEN** the site is built without access to the GitHub API
- **THEN** the build succeeds, no version is shown, and the download control leads to the releases page

#### Scenario: Release without installer
- **WHEN** the latest stable release has no `UrDeck-win-Setup.exe` asset
- **THEN** the site's build fails, naming the release
