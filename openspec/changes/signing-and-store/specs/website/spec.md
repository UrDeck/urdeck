## MODIFIED Requirements

### Requirement: Download State
Until a signed release exists, the landing page MUST NOT offer a file to download. The download control says that a
download is coming and leads to the repository.

Once a stable signed release exists:

- The download control offers `UrDeck-win-Setup.exe` of the latest stable release, through GitHub's latest-release
  download address, so the site does not change for each release
- Next to it, the page names the version line ("0.x preview"), what the installer needs (Windows 10 or 11, x64, no
  administrator rights) and a link to the portable zip and to all releases
- When the Microsoft Store listing is live (see store-listing), the page also links it
- An unsigned release and a prerelease are never offered by the site

#### Scenario: No release yet
- **WHEN** no signed stable release exists and a visitor uses the download control
- **THEN** no file is downloaded and the visitor arrives at the repository on GitHub

#### Scenario: Signed release
- **WHEN** signed `0.2.0` is the latest stable release and a visitor uses the download control
- **THEN** the browser downloads `UrDeck-win-Setup.exe` of `0.2.0`

#### Scenario: Store listing live
- **WHEN** the Store listing is published
- **THEN** the landing page links it next to the download control

## ADDED Requirements

### Requirement: Code Signing Policy Page
The site MUST have a page with the project's code signing policy (see code-signing, "Code Signing Policy"):

- It states that free code signing is provided by SignPath.io, with a certificate by SignPath Foundation, as the
  signing service asks
- It names what is signed, the roles and the people holding them, and the approval rule
- It links the app's privacy statement
- The landing page's footer and the README link it

#### Scenario: Policy page
- **WHEN** a visitor opens the code signing policy page
- **THEN** it names SignPath Foundation as the certificate's holder, the owner as the approver of every request, and
  links the privacy statement

### Requirement: App Privacy Statement
The site MUST have a page that says what the installed app sends over the network and to whom:

- Every request the app makes by itself is listed with its destination, what it carries and when it happens: the
  weather provider (Open-Meteo, the coordinates of the places the user typed), site icons (the site a shortcut or icon
  names), and the update check (GitHub, once a day, nothing about the user beyond what any web request carries)
- It says how to turn each off (`"updates": "off"`, removing the weather widgets, a local icon for a shortcut)
- It says that the app has no telemetry, no account, and writes its files only to its data folder
- A change that adds or changes a request the app makes updates this page in the same pull request
- The page is the privacy address of the Store listing

#### Scenario: Privacy statement
- **WHEN** a visitor opens the app's privacy statement
- **THEN** it lists Open-Meteo, the sites of site icons and GitHub's releases with what each request carries, and how
  to turn each off
