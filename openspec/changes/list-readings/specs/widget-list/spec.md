## ADDED Requirements

### Requirement: List Widget Identity
The system MUST provide a first-party widget `urdeck.widgets.list` that shows any list reading of any provider. It is a
plugin like any other, references only the SDK, and needs no knowledge of the provider whose list it shows.

#### Scenario: A list from a provider it does not know
- **WHEN** a third-party provider publishes a list reading with a title and a detail field
- **THEN** the List widget shows it without any change to the widget

### Requirement: List Widget Settings
The widget MUST declare these settings:

- `reading`: a reading setting that accepts lists only
- `title`: a text shown above the rows; empty uses the reading's label; the title can be hidden
- `overflow`: `scroll` (the default: the user pages with a tap on the lower or upper part) or `rotate` (rows rotate by
  the text list's rotate mode) when the rows do not fit
- `maxRows`: at most this many rows are taken from the list, in the provider's order (default: all)
- `showImages`: on by default; off draws no image role

#### Scenario: Rotating headlines
- **WHEN** `overflow` is `rotate` and the list has more rows than fit
- **THEN** the rows rotate on the text list's timer

### Requirement: List Widget Presentation
The widget MUST draw rows by role with the text list (see text-layout, "Text List From A List Reading"): the title field
as the row's text, the detail field as its second line, the status field as its mark and the image field as its image.
How many rows show follows from the widget's size and the theme's text sizes; the widget draws no card, names no font and
takes its colours from the theme. At 1x1 the widget shows the title and the list's count (as a readout) instead of rows.

#### Scenario: 2x2 friends list
- **WHEN** the widget is 2x2 and shows a list of six friends
- **THEN** the title and as many friends as fit are drawn, each with its avatar, name and game

#### Scenario: 1x1
- **WHEN** the widget is 1x1
- **THEN** it shows the title and the number of rows

### Requirement: List Widget States
The widget MUST show an empty list with the description's empty text in a muted line ("Nothing to show" when the
description declares none), a pending reading as the text list's placeholder rows, a stale list with its rows and the
theme's stale treatment, and an unavailable reading with its reason, as other widgets show states.

#### Scenario: Nobody playing
- **WHEN** `steam:friends/playing` is empty and its description's empty text is "Nobody is playing"
- **THEN** the widget shows its title and "Nobody is playing"

#### Scenario: Not connected
- **WHEN** the reading is unavailable with the reason "Connect Steam in Settings"
- **THEN** the widget shows that reason

### Requirement: List Widget Taps
A tap on a row whose list has a link field MUST open that row's link through the launcher; a tap elsewhere, or on a row
without a link, pages the list in `scroll` mode and does nothing in `rotate` mode.

#### Scenario: Open a friend's profile
- **WHEN** the user taps the row of a friend whose link is `steam://url/SteamIDPage/76561190000000001`
- **THEN** the launcher opens that link

### Requirement: List Widget Sample
The widget MUST draw from sample data in the preview tool, the golden images and `--demo`, using a first-party provider's
list sample (the system provider's `processes/top` by default), so it is drawn without any service.

#### Scenario: Preview
- **WHEN** `urdeck-cli preview render --widget urdeck.widgets.list --widget-size 2x2` runs on Linux
- **THEN** a picture of a list with rows is produced from sample data
