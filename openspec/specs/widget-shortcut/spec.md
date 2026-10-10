# widget-shortcut Specification

## Purpose
Defines the shortcut widget: a card that shows the icon of an application, a file, a folder or a web address and opens
it when tapped.
## Requirements
### Requirement: Shortcut Widget Identity
The product MUST ship a first-party widget with the type id `urdeck.widgets.shortcut`, the size 1x1 and the category
"Launch". It references only the widget SDK, uses the `[RefreshOnData]` strategy (no timer) and accepts taps.

#### Scenario: Registered
- **WHEN** the host starts with the first-party plugins
- **THEN** `urdeck.widgets.shortcut` is registered with the size 1x1

#### Scenario: No timer
- **WHEN** a page of shortcuts is shown and nothing is touched
- **THEN** no shortcut is refreshed or repainted

### Requirement: Shortcut Configuration
The shortcut's settings MUST be flat, typed properties on its widget object, each with a default, so that a form can be
generated from them later:

| Setting | Type | Default | Meaning |
|---|---|---|---|
| `target` | text | none | What to open (see launcher, "Launch Targets") |
| `arguments` | text | none | Arguments for a file target |
| `icon` | text | none | Where the icon comes from instead of the target: a local image file, or any other icon source (see icons, "Icon Sources") |
| `label` | text | not set | The label under the icon |

- `label` follows the label convention with three states: not set, a text, and empty. At 1x1 "not set" and empty both
  draw no label; a text is drawn under the icon
- Properties the widget does not know are preserved when the configuration is saved
- A missing or empty `target` is allowed: the card shows the placeholder and does not react to taps

#### Scenario: Minimal shortcut
- **WHEN** a widget object has `typeId` `urdeck.widgets.shortcut` and `target` `https://example.com` and nothing else
- **THEN** the card shows the icon for that address with no label, and a tap opens the address

#### Scenario: Unknown property survives
- **WHEN** a shortcut's object contains a property the widget does not know and the configuration is saved
- **THEN** the property is still in the file

### Requirement: Shortcut Composition
The shortcut MUST draw its content with the image tile component (see components, "Image Tile") inside the card's
content rectangle, and MUST NOT name a colour, font or size of its own.

- The icon source is `icon` when it is set and not empty, else `target`
- While the icon is loading, and when there is none, the tile's placeholder is drawn with the first letter or digit of
  the label text when one is set, else of the target's name: the host name of a web address without a leading `www.`,
  the file name of a file target, else the target text
- The widget draws no pressed state; press feedback is the host's (see widget-input, "Press Feedback")

#### Scenario: Icon ready
- **WHEN** the icon for the target is available
- **THEN** it is drawn centred in the card, fitted to the content rectangle

#### Scenario: Icon loading
- **WHEN** a shortcut to `https://homeassistant.example.com` is painted before its icon is ready
- **THEN** the placeholder with the letter `H` is drawn, and the icon replaces it when it is ready

#### Scenario: Icon override
- **WHEN** `icon` names a local PNG file and `target` is a web address
- **THEN** the PNG is drawn and no icon is requested for the web address

#### Scenario: Label text
- **WHEN** `label` is `Steam`
- **THEN** `Steam` is drawn under the icon at the theme's label size and the icon takes the remaining space

### Requirement: Shortcut Tap
A tap anywhere on the card MUST ask the host to launch `target` with `arguments` (see launcher, "Launching"). The
shortcut accepts a tap only when its target is valid.

- A shortcut with a missing or invalid target declines taps, so it shows no press feedback; the invalid target is
  logged once when the configuration is applied
- A launch that fails changes nothing on the card; the launcher logs it

#### Scenario: Tap launches
- **WHEN** the user taps a shortcut whose target is the path of an installed application
- **THEN** the application is launched once

#### Scenario: No target
- **WHEN** the user presses a shortcut that has no target
- **THEN** the card does not react and nothing is launched

### Requirement: Shortcut Repaint Rules
The shortcut MUST be repainted only when what it shows changes: on its first paint, when its configuration changes,
and when the icon it asked for becomes ready or is given up on. A tap MUST NOT cause a repaint.

#### Scenario: After a tap
- **WHEN** the user taps a shortcut
- **THEN** the shortcut reports that it needs no repaint

#### Scenario: Icon arrives
- **WHEN** the icon becomes ready after the first paint
- **THEN** the shortcut is repainted once and then no more

