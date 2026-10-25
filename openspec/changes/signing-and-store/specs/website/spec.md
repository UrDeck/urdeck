## MODIFIED Requirements

### Requirement: Download State
The landing page MUST offer the installer of the latest stable release (see the base requirement) and, once releases
are code signed, MUST NOT tell the visitor to expect a SmartScreen warning:

- The note about SmartScreen is removed from the landing page once the latest stable release is signed
- When the Microsoft Store listing is live (see store-listing), the page also links it next to the download control
- An unsigned release and a prerelease are never offered by the site once signing is in place

#### Scenario: Signed release
- **WHEN** signed `0.2.0` is the latest stable release
- **THEN** the download control offers its `UrDeck-win-Setup.exe` and the page carries no SmartScreen note

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
