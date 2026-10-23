# Site Media Specification

## Purpose

Makes every picture and loop the website shows from the repository itself: what is needed follows from what the product
ships, the files are rendered with sample data by the product's own preview tool, and a manual workflow proposes them in
a pull request.
## Requirements
### Requirement: The Media Matrix
The set of media files the site needs MUST be computed from the product's catalog (`urdeck-cli catalog export`) and the
site's overrides, never listed by hand:

- For every theme: a picture of the showcase page on the full panel (1100x3840) over the theme's own choice of
  background, and a loop of it when the theme or its background moves
- For every widget, every size it declares for previews, and every theme: a picture of the widget alone over the theme's
  own background, and a loop when the widget moves (its drawing depends on time, or its sample readings change)
- For every background: a poster and a loop, in the colours of the theme it is shown with: the first theme, by name, whose
  default background it is; else the first theme that pairs with it on both sides; else the first theme that lists it;
  else `default-dark`

Adding a theme, a background, a widget or a widget size adds its entries; removing one removes them.

#### Scenario: A new theme
- **WHEN** a built-in theme is added and the media are generated
- **THEN** its deck picture and loop and a picture of every widget size in it exist, with no site file edited

#### Scenario: A removed widget size
- **WHEN** a widget stops declaring 4x4 and the media are generated
- **THEN** the 4x4 pictures and loops of that widget are deleted in every theme

#### Scenario: A size left out of previews
- **WHEN** a widget declares a size with `Preview = false`
- **THEN** no media is made for that size

### Requirement: The Showcase Page
Every theme's deck picture MUST show the same page, `site/showcase/urdeck-config.json`, so that themes can be compared.
When `site/decks/<theme>/urdeck-config.json` exists, that theme's deck is drawn from it instead. Widget media use the
widget's default configuration unless `site/showcase/widgets.json` gives the site a configuration for that widget (for
all its sizes or for one size).

#### Scenario: Comparing themes
- **WHEN** the deck pictures of Carbon and Ember are generated
- **THEN** they show the same widgets in the same places

#### Scenario: A widget shown differently on the site
- **WHEN** `site/showcase/widgets.json` gives the clock the `flap` style
- **THEN** every clock picture and loop shows split-flap digits, and the clock's default configuration is unchanged

### Requirement: Rendering The Media
The media MUST be rendered by `urdeck-cli preview render` with sample data and the sample page's moment (loops start
there), and encoded with `ffmpeg` (H.264, no audio, playing forwards then backwards so a loop has no jump). Generation
MUST work on Linux and on Windows, MUST NOT need the host, the network (beyond installing its tools) or a display, and
MUST NOT read the machine's readings. Every loop's first frame is its picture (a deck loop is drawn at the size it ships at, so its first frame is the picture
scaled, not the same pixels). Loops respect the sizes and limits of
"Media Limits".

#### Scenario: On a Linux runner
- **WHEN** `npm run media` runs on a Linux machine with .NET, Node and ffmpeg installed
- **THEN** every file of the matrix is written and the run makes no request to a weather service

#### Scenario: A loop starts where its picture is
- **WHEN** the clock's loop and picture are generated in a theme
- **THEN** the loop's first frame is the picture (the same pixels for a widget or a background)

### Requirement: Media Limits
Generated files MUST stay within these limits, and generation MUST fail naming the file that does not:

- Deck pictures 1100x3840 lossless WebP; deck loops at most 540 pixels wide, 4 to 6 seconds, under 1.5 MB
- Widget pictures at the widget's cells on a 1100-pixel panel, lossless WebP; widget loops at most 800 pixels wide, 3 to 6 seconds,
  under 1 MB
- Background posters 540x960 lossless WebP; background loops 540x960, 4 to 6 seconds, under 1 MB

#### Scenario: A loop too large
- **WHEN** a background loop encodes to 1.4 MB
- **THEN** generation fails with the file's name, its size and the limit

### Requirement: The Generator Owns Its Output
Generated files MUST live only in `site/src/generated/` and `site/public/generated/`, with a manifest
(`site/src/generated/media.json`) that lists every file, what it shows (kind, theme, widget, size, background) and a key
of the inputs it was made from. The catalog the matrix came from is written beside it (`site/src/generated/catalog.json`)
for the site's build. A file in those folders that the matrix does not name is deleted. Nothing outside them is written.

A file's input key MUST change when anything that can change its pixels changes: the product code that draws
(engine, preview, widgets, SDK), the sample data, the theme, the background, the widget's configuration, the showcase page
and the render settings. A file whose key did not change is not rendered again unless everything is asked for.

#### Scenario: Nothing changed
- **WHEN** `npm run media` runs twice without a change in between
- **THEN** the second run renders nothing and changes no file

#### Scenario: A theme changed
- **WHEN** only Ember's theme file changes and `npm run media` runs
- **THEN** only Ember's files are rendered again

#### Scenario: Drawing code changed
- **WHEN** a file of the engine changes
- **THEN** every file is stale

### Requirement: Staleness Report
`npm run media -- --status` MUST print, without rendering, how many files of the matrix are missing, stale and
unneeded, and list them. The CI of a pull request that touches the product or the site MUST run it and show the counts in
its summary without failing the pull request.

#### Scenario: A pull request that changes a theme
- **WHEN** a pull request changes Paper's colours
- **THEN** its CI summary says that Paper's files are stale, and the checks pass

### Requirement: Refresh Workflow
A workflow started by hand MUST generate the stale media on a Linux runner and, when any file changed, open a pull
request titled `chore(site): refresh media` from a new branch, listing the files added, changed and removed. It MUST open
the pull request as a GitHub App, so that the pull request's required checks run as for any other. When nothing changed it
MUST open nothing and say so. It MAY be asked to render everything.

#### Scenario: A refresh
- **WHEN** the workflow is started after two themes changed
- **THEN** a pull request with their media is opened, its checks run, and its description lists the changed files

#### Scenario: Nothing to do
- **WHEN** the workflow is started and no file is stale
- **THEN** no branch or pull request is created and the run's summary says the media are current

### Requirement: Cookbook Pictures
The media matrix MUST include one picture per cookbook recipe (`site/cookbook/<recipe>/`), rendered from the recipe's
theme folder with what its `recipe.json` asks to show (the sample page, the dock, or one widget at a size), over the
background it names or none. A recipe added or removed adds or removes its picture, and a change to a recipe's folder
makes only its picture stale.

#### Scenario: A new recipe
- **WHEN** a folder `site/cookbook/hard-shadow/` with a theme and a `recipe.json` is added and the media are generated
- **THEN** its picture exists and the media manifest names it as the recipe's picture

#### Scenario: One recipe changed
- **WHEN** only the circular dock recipe's theme changes
- **THEN** only its picture is stale

