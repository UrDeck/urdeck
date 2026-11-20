## MODIFIED Requirements

### Requirement: Provider Not Connected
When a provider is ready but cannot list its readings because it is not reachable, the picker MUST say so with a way to
open its settings, and still list the readings of that provider that the pages already use, without values. A reading
setting in the form whose provider is missing required settings MUST say so too.

#### Scenario: Home Assistant that does not answer
- **WHEN** Home Assistant has an address and a token, does not answer, and the user opens the picker's Home Assistant tab
- **THEN** the picker says Home Assistant is not connected, offers Set up, and lists the entities the pages use

## ADDED Requirements

### Requirement: Providers That Are Not Ready Are Not Offered
The picker MUST NOT show a tab, a filter or rows for a provider that is not available or needs setup (see
provider-readiness), except that a provider whose readings the pages already use stays, with a bar saying it needs setup
and a way to open its settings, and lists the readings in use without values. The tab "All" lists only the readings of
the providers shown. Opening the picker MUST NOT start a provider that is not ready.

#### Scenario: No Home Assistant
- **WHEN** Home Assistant has no address and no page uses one of its readings and the user opens the picker from a Stats widget
- **THEN** no Home Assistant tab, filter or row is shown

#### Scenario: Steam not installed
- **WHEN** the Steam client is not installed and no page uses a Steam reading
- **THEN** the picker offers no Steam readings

#### Scenario: A page uses a reading of a provider that is not ready
- **WHEN** a Stats widget has a Home Assistant reading and Home Assistant has no token
- **THEN** the Home Assistant tab is shown with a bar saying it needs setup, listing that reading without a value

#### Scenario: Becoming ready while the picker is open
- **WHEN** the user saves Home Assistant's token in the editor with the picker open
- **THEN** the Home Assistant tab appears when the picker next refreshes its providers, and the picker never shows a stale list of readings for it
