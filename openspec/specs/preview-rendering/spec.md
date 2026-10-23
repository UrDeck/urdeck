# Preview Rendering Specification

## Purpose

Draws a page of UrDeck, or one widget alone, in any theme, over any background, at any size and moment, with sample data,
as one picture or as frames, so that pictures of the product can be made by tools and CI on Windows and Linux and show
what the panel shows.

## Requirements

### Requirement: The Sample Page
The sample page MUST be an ordinary UrDeck configuration (`urdeck-config.json` format) kept with the preview code: one
page with a clock (4x1), two stats cards (2x1, CPU and GPU load), a weather card (4x2) and four shortcuts, and a dock of
four shortcuts. Its readings come from the provider samples (see `sample-data`); its moment is 13 October 2026, 21:47:32
local time. The picture tests, the tool's `theme render` and the previews all draw this one page.

The shortcuts of the sample page MUST draw without a network request and without the operating system's icons, so they
draw the same on every platform.

#### Scenario: One page for every picture
- **WHEN** the picture tests and `urdeck-cli theme render` draw a built-in theme at the same size
- **THEN** they draw the same page with the same readings

#### Scenario: Changing the sample page
- **WHEN** a widget is added to the sample page's configuration file
- **THEN** every picture made from the sample page shows it, with no code changed

### Requirement: What A Preview Is Made Of
A preview MUST be drawn from:

- A configuration (the sample page, or any `urdeck-config.json`) and which of its pages to draw, or its dock page
- Or, instead, one widget type at one of its sizes, drawn with its default configuration (see "One Widget Alone")
- A theme, by name or from a folder; the configuration's theme when none is given
- A background: by name, none, or the theme's own choice (its default background and still background as the deck would
  choose them); the configuration's background when none is given
- A size in pixels
- A moment, the sample page's moment when none is given
- A motion level, `full`, `reduced` or `off` (default `full`), with the effect it has on the deck (see `motion`)

The page MUST be laid out and drawn by the same code the deck and its snapshots use: cards, card chrome, underlay,
pattern, the dock, the page indicator, the background and its scrim. Readings come from sample data only.

#### Scenario: A theme over its own background
- **WHEN** the sample page is previewed in Rigline with the theme's choice of background at 1100x1920
- **THEN** the picture shows Rigline's cards over its `horizon` background

#### Scenario: No background
- **WHEN** a preview is drawn with the background `none`
- **THEN** the cards are drawn over the theme's background colour

#### Scenario: A user's configuration
- **WHEN** a configuration file with two pages is previewed with its second page
- **THEN** the picture shows that page with sample readings, and the file is not changed

### Requirement: Previews Are Deterministic
The same preview inputs MUST give the same pixels on one machine, every time. A preview MUST NOT read the clock, a sensor,
the network, the operating system's icons or a font installed on the machine. It runs on Windows and on Linux. Pictures
made on different platforms MAY differ where text is drawn (the edges of glyphs and the width of small text), and in
nothing else.

#### Scenario: Twice the same
- **WHEN** the same preview is drawn twice on one machine
- **THEN** both pictures have the same bytes

#### Scenario: On Linux
- **WHEN** the sample page is previewed in every built-in theme on a Linux machine with no fonts installed
- **THEN** every picture is drawn, with the theme's fonts, and differs from the Windows picture only at the edges of text

### Requirement: Frames
A preview MAY be drawn as N frames at a given rate: frame `i` is drawn at the preview's moment plus `i` divided by the
rate. Across the frames, as on the panel, the preview MUST move what the deck moves:

- The widgets are the same instances and are drawn at each frame's moment, so an animation that depends on time moves
- Sample readings take their value at each frame's moment
- A moving shader background is drawn at each frame's moment, starting at the time a still preview draws it
- An animated card border turns (`spin`) or breathes (`pulse`) in the steps the deck uses, starting where a still picture
  shows it

At the motion level `reduced` or `off` the background and the border are drawn still in every frame, as the deck draws
them at that level. A widget whose readings change gets a history as it would on the panel: before the first frame, the
readings are sampled over the time a widget's history spans, so a sparkline is full in the first frame.

#### Scenario: A turning border
- **WHEN** Rigline's sample page is previewed as 105 frames at 15 per second
- **THEN** the border's colours turn by one step per frame and frame 0 equals the still preview

#### Scenario: A moving background
- **WHEN** a preview over the `aurora` background is drawn as 90 frames at 30 per second
- **THEN** the background moves from frame to frame and frame 0 equals the still preview

#### Scenario: Reduced motion
- **WHEN** the same Rigline frames are drawn at the motion level `reduced`
- **THEN** the border and the background are the same in every frame

#### Scenario: A gauge between readings
- **WHEN** a stats card whose sample keyframes go from 41 to 68 over 2 seconds is drawn as 60 frames at 30 per second
- **THEN** the gauge's value rises across the frames

### Requirement: One Widget Alone
A preview MAY be of one widget type at one of its declared sizes: a page holding only that widget, at the top left, with
its default configuration (or a configuration given with it), drawn on a panel four cells wide. The picture MUST be the
widget's cells only (its width and height in cells times the cell's size in pixels), including the card's gap. A size the
widget does not declare MUST be refused with a reason.

#### Scenario: The stats widget at 4x2
- **WHEN** the stats widget is previewed alone at 4x2 on a panel 1100 pixels wide
- **THEN** the picture is 1100x550 and shows a stats card with the default readings

#### Scenario: A size the widget does not have
- **WHEN** the weather widget is previewed alone at 1x1
- **THEN** no picture is made and the reason names the sizes the weather widget declares

### Requirement: Animated Border Matches The Panel
An animated card border drawn in a frame MUST look like the border the deck shows at the same moment: the same ring (the
card's shape, corners and width), the same colours, the same angle or opacity for the same step. It is not required to
equal the compositor's pixels byte for byte. A still preview, and every picture test, draws the border exactly as before.

#### Scenario: Still pictures unchanged
- **WHEN** the picture tests run after this change
- **THEN** every reference picture still matches, including the themes with an animated border

#### Scenario: A quarter turn
- **WHEN** a `spin` border with a period of 8 s is drawn in the frame at 2 s
- **THEN** its colours have turned a quarter of the way round the card compared with frame 0
