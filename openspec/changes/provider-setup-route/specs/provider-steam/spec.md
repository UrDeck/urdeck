## ADDED Requirements

### Requirement: The Steam Widget Leads To Steam's Settings
When a part of the Steam widget cannot be shown because Steam needs setup (not connected, expired, not installed), its
notice MUST say so in the provider's wording and a tap on the notice MUST open Steam in the Providers section (see
launcher). The tap MUST NOT start the Steam client. Where the widget shows real data, its taps keep their meaning: a
friend's profile, a game, the friends list.

#### Scenario: Not connected
- **WHEN** Steam is installed and not connected and the user taps the friends area
- **THEN** the editor opens on Steam in the Providers section, and the Steam client is not started

#### Scenario: Not installed
- **WHEN** Steam is not installed and the user taps the notice
- **THEN** the editor opens on Steam's page, which says Steam is not installed and links to the download

#### Scenario: Connected
- **WHEN** Steam is connected and the user taps a friend
- **THEN** that friend's profile opens, as before
