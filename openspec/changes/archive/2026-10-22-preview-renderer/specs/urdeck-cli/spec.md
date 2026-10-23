## MODIFIED Requirements

### Requirement: Rendering A Theme To A File
`urdeck-cli theme render <folder> -o <file>` MUST draw the sample page in the theme and write it as a PNG:

- `--builtin <name>` in place of the folder draws a built-in theme; the folder and `--builtin` MUST NOT be given together
- The sample page is the same one the tool's and the product's picture tests draw (see `preview-rendering`, "The Sample
  Page"): a clock, two stats cards, a weather card and four shortcuts, with the providers' sample readings and a fixed time
- It is drawn by the preview renderer without a background, as the picture tests draw it
- `--page <page|dock>` chooses the page or the dock; the default is `page`
- `--size <width>x<height>` is the picture's size in pixels; the default is 1100x1920
- The same input gives the same picture on one machine; pictures made on different machines are not guaranteed to be
  identical
- When the theme cannot be read (`THEME001`), no file is written, the reason is printed and the exit code is 1
- The theme's findings are printed to the error stream as warnings; they do not stop the picture, and only
  `--fail-on-warnings` makes them change the exit code
- An existing output file is replaced

#### Scenario: A picture
- **WHEN** `theme render mine -o out.png` is run on a valid folder
- **THEN** `out.png` is a 1100x1920 PNG of the sample page in that theme and the exit code is 0

#### Scenario: Same pixels as the tests
- **WHEN** a built-in theme is rendered with the size and page the picture tests use
- **THEN** the picture has the pixels of that test's reference picture, where the platform draws text the same way

#### Scenario: The dock
- **WHEN** `--page dock` is given
- **THEN** the picture shows the dock page

#### Scenario: Unreadable theme
- **WHEN** the folder has no `theme.json`
- **THEN** no file is written and the exit code is 1

#### Scenario: Warnings do not stop the picture
- **WHEN** a theme with one out-of-range value is rendered
- **THEN** the picture is written, the warning is on the error stream and the exit code is 0

## ADDED Requirements

### Requirement: Rendering A Preview
`urdeck-cli preview render -o <file-or-folder>` MUST draw a preview (see `preview-rendering`) and write it as PNG:

- What to draw: `--config <file>` (a `urdeck-config.json`; the sample page when absent) with `--page <N|dock>` (default:
  the configuration's active page), or `--widget <typeId> --widget-size <W>x<H>` for one widget alone (with `--config`, the
  widget's configuration is the first widget of that type in the file, otherwise its default configuration)
- `--theme <name>` (a built-in theme), or `--theme-dir <folder>` (a theme folder); default: the configuration's theme
- `--background <name|none|theme>`: a background by name (built-in, or in a folder given with `--background-dir`), none, or
  the theme's choice; default: the configuration's background
- `--size <W>x<H>` for a page (default 1100x1920); `--width <px>` for a widget alone, the width of the four-cell panel
  (default 1100)
- `--time <local date and time>`: the moment drawn (default: the sample page's moment)
- `--frames N` with `--fps F` (default 30): the output is a folder and N pictures `frame-0000.png` onwards are written to it
- `--motion <full|reduced|off>` (default `full`)
- Readings come from the providers' samples only, and the command makes no network request
- Exit codes are the tool's (`0` written, `1` an input that cannot be used, with the reason, and nothing written, `2` a
  usage error); the theme's findings go to the error stream as warnings, as for `theme render`
- An existing output file, or existing frames in the output folder, are replaced

#### Scenario: A page in a theme
- **WHEN** `preview render --theme ember --background theme -o out.png` is run
- **THEN** `out.png` is the sample page in Ember over Ember's own background, 1100x1920, and the exit code is 0

#### Scenario: A widget alone
- **WHEN** `preview render --widget urdeck.widgets.weather --widget-size 4x4 --theme paper -o w.png` is run
- **THEN** `w.png` is 1100x1100 and shows a weather card in Paper with the sample's forecast

#### Scenario: Frames
- **WHEN** `preview render --theme rigline --background theme --frames 90 -o frames` is run
- **THEN** `frames` holds `frame-0000.png` to `frame-0089.png`, in which the border and the background move

#### Scenario: An unknown widget
- **WHEN** `--widget example.unknown` is given
- **THEN** nothing is written, the reason lists the widget types the tool knows, and the exit code is 1

### Requirement: Exporting The Catalog
`urdeck-cli catalog export [-o <file>]` MUST write, as one JSON document (to the file, or to standard output), what the
product knows about what it ships:

- `version`: `1`
- `themes`: each theme's name, display name, description, author, mode, tags, swatch colours, default background and the
  backgrounds it pairs with
- `backgrounds`: each background's name, title, description, author, tags, parameters and the themes it pairs with
- `pairings`: for each theme, the backgrounds listed for it, each marked when the pairing is named on both sides
- `widgets`: each first-party widget's type id, name, description, category, declared sizes with their preview flag, its
  default configuration as it would appear in `urdeck-config.json`, and its settings: each setting's name in
  `urdeck-config.json`, its type (text, number, whole number, on/off, list, object, or the values it accepts) and its
  default for a widget written without it
- `providers`: each first-party provider's id, name and whether it carries a sample
- `findings`: every theme and background finding code with its severity and a one-line summary of what it means

Built-in themes and backgrounds are always listed; `--theme-dir` and `--background-dir` add a user's folders, as for
`theme validate`. Lists are sorted by name and the same input gives the same bytes, so the output can be compared
between runs.

#### Scenario: What the site reads
- **WHEN** `catalog export -o catalog.json` is run
- **THEN** `catalog.json` lists the eleven built-in themes, the sixteen built-in backgrounds, the four first-party widgets
  with their sizes, default configurations and settings, the two first-party providers and every finding code

#### Scenario: A widget's settings
- **WHEN** the catalog is exported
- **THEN** the clock's settings include `format` with the values it accepts and its default, and `showDate` as on/off

#### Scenario: Stable output
- **WHEN** `catalog export` is run twice with the same inputs
- **THEN** both outputs are byte for byte the same
