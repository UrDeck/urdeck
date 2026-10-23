## Why

Every picture on the site that shows a widget is drawn from live data (this machine's CPU, the real weather in Zurich, the
wall clock), so each one needs a human to check it for personal content, and none can be made by CI. The site also cannot
show anything in a theme other than glass, and the turning border of Rigline and Ember cannot be shown at all: it is drawn
by the window's compositor, not by the page renderer. The engine already has the seams the configuration editor will need
(`ThemeCatalog`, `BackgroundCatalog`, `PairingIndex`); the missing one is the preview renderer (`docs/design/look-and-feel/STATUS.md`,
PR 18, and `07-pairing-and-editor.md` §3). This change builds it first, because the site is its first user (the changes
`site-media-pipeline`, `site-gallery` and `docs-rewrite` follow) and the editor its second.

## What Changes

- **Sample data from data providers.** Each data provider carries a sample file: the readings it offers, each with a
  fixed value or values that change over time, so a page can be drawn with plausible, made-up data. A reading that
  belongs to a place (`weather:<place>/...`) answers for any place. Times and dates are given relative to the moment drawn.
  Both first-party providers ship one, with the five forecast days a 4x4 weather card shows.
- **The sample page is a configuration file.** The page every theme picture starts from (a clock, two stats cards, a
  weather card, four shortcuts and the dock) becomes an ordinary `urdeck-config.json`, kept with the preview code. Its
  readings come from the provider samples. **BREAKING** (internal): the hard-coded sample page in code goes away; the
  golden pictures are expected to stay byte-identical, and the change says why where they do not.
- **A preview renderer.** One routine draws a configuration's page (or one widget alone) in a theme, over a background,
  at a size and a moment, with sample data, and can draw a run of frames. In frames the widgets, the background and the
  animated card border all move with time, as on the panel. It runs on Windows and Linux.
- **Demo mode in the host.** `UrDeck.Host.exe --demo` (with or without `--snapshot`) replaces every provider that has a
  sample with its sample and makes no network request, so the live deck and its snapshots show the same data on any
  machine.
- **Defaults that show something.** A widget's default configuration (the one a future editor gives a widget it adds)
  MUST draw a meaningful card at every size the widget declares, with sample data. Stats, Weather and Shortcut get
  defaults; the Clock's already shows the time. Existing configurations are not changed.
- **A size can opt out of previews.** `[WidgetSize]` gains an optional flag that leaves a size out of generated pictures,
  for a future widget whose larger sizes only repeat rows.
- **Command line.** `urdeck-cli preview render` draws a configuration, the sample page or one widget at a size, as one
  picture or as frames. `urdeck-cli catalog export` writes what the engine knows about the themes, backgrounds, pairings,
  widgets and providers as one JSON document, for the site's build. `theme render` draws through the preview renderer.

Not in this change: the site's media pipeline, the gallery and the theme picker on the site, the docs rewrite (the three
changes that follow); loading third-party widget plugins into the command line tool; a theme picker in the app.

## Capabilities

### New Capabilities

- `sample-data`: the sample file a data provider carries (readings, values over time, place wildcards, relative dates and
  times), how a sample replaces its provider, and the checks that keep it in step with what the provider offers.
- `preview-rendering`: the preview renderer: the sample page as a configuration, what a preview is made of, frames in
  which widgets, background and animated border move, one widget alone, determinism and running on Linux.

### Modified Capabilities

- `data-providers`: a first-party provider must carry a sample file.
- `widget-sdk`: `[WidgetSize]` gains the preview flag (`Widget Metadata Attributes`); a new requirement that the default
  configuration draws a meaningful card at every previewed size.
- `host-shell`: a new requirement for demo mode (`--demo`).
- `urdeck-cli`: `theme render` draws the sample page from its configuration with sample data; new requirements for
  `preview render` and `catalog export`.

## Impact

- `sdk/UrDeck.Sdk`: `WidgetSizeAttribute` gains the preview flag (an SDK minor version).
- `src/UrDeck.Engine`: the sample file reader and the sample provider (`Data`); a way for a preview to sample every
  subscribed provider once at a given time; the page renderer draws an animated border at a given time when asked;
  the background painted at a frame's time; the plugin loader finds a provider's sample.
- `src/UrDeck.Preview`: the preview renderer, the sample page as an embedded `urdeck-config.json`, the first-party widget
  and provider registrations; references the two first-party provider projects (for their samples).
- `providers/`: a `sample.json` embedded in each provider.
- `widgets/`: `DefaultConfig` for Stats, Weather and Shortcut.
- `src/UrDeck.Host`: `--demo`.
- `tools/UrDeck.Cli`: `preview render`, `catalog export`; `theme render` moved onto the renderer.
- `tests/`: sample coverage, sample replay, default-config pictures for every widget and size, preview determinism, frame
  behaviour (border, background), the command line; every golden byte-identical or explained.
- Docs: `docs/themes.md` and `docs/backgrounds.md` (the tool's commands), the README's command list, the site's pages for
  the command line and the widgets (defaults), a section on sample files for provider authors, `AGENTS.md` (commands),
  `docs/design/look-and-feel/STATUS.md` (PR 18 row).
