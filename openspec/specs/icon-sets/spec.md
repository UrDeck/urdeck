# icon-sets Specification

## Purpose
Defines icon sets: the folders of vector icons a theme can draw weather and shortcut glyphs from, the manifest that maps the
product's own icon names to files, the rules an icon file must follow, the set built into the product and how a request for
an icon is answered. A theme author or a plugin that ships a set relies on this format.
## Requirements
### Requirement: Icon Set Folder And Manifest
An icon set MUST be a folder holding a `manifest.json` and the SVG files it names. The manifest is required and is the only
source of truth for which icon a key shows: no file is found by its name alone.

- The manifest is a JSON object with `version` (the number 1), `name` (text), `source` (text: where the icons come from),
  `license` (text: the licence's name, such as `ISC`), an optional `upstreamVersion` (text) and `keys`, an object from a
  canonical icon key (such as `weather/rain`) to the path of an SVG file relative to the set's folder
- Several keys MAY name the same file, and a file MAY keep the name it has upstream
- A path that is absolute, leaves the set's folder (a `..` segment, on Windows or on Linux, a backslash counting as a
  separator) or does not end in `.svg` is not loaded; the key has no icon
- A folder with no manifest, or a manifest that is not valid JSON or has another `version`, is not a set: the host logs one
  line naming the folder and the file to make, and the set is treated as not named
- Keys the manifest omits have no icon in this set; the resolution chain answers them (see "Resolution Chain")
- A set's folder MAY hold a licence file; the manifest's `license` and `source` are what the product records

#### Scenario: A set with a manifest
- **WHEN** a folder holds `manifest.json` with `"keys": {"weather/rain": "cloud-rain.svg"}` and that file
- **THEN** the key `weather/rain` has the icon in `cloud-rain.svg`, and no other key has one from this set

#### Scenario: Two keys, one file
- **WHEN** `weather/thunderstorms-day` and `weather/thunderstorms-night` both name `cloud-lightning.svg`
- **THEN** both keys show that icon and the file is read once

#### Scenario: No manifest
- **WHEN** a folder holds SVG files named `weather/rain.svg` and no `manifest.json`
- **THEN** none of them is loaded, and one log line names the folder and says a `manifest.json` is needed

#### Scenario: Path leaves the folder
- **WHEN** a manifest maps a key to `..\other\x.svg`
- **THEN** the key has no icon and no file outside the set's folder is read

### Requirement: Canonical Keys
The keys an icon is asked for by MUST be those of the product's lists, so that a widget, a theme and any set agree. A key is
`<group>/<name>`.

- `weather` (13): `clear-day`, `clear-night`, `partly-cloudy-day`, `partly-cloudy-night`, `overcast`, `fog`, `drizzle`,
  `rain`, `sleet`, `snow`, `thunderstorms-day`, `thunderstorms-night`, `not-available`
- `app` (24): `terminal`, `folder`, `file`, `music`, `gamepad`, `browser`, `mail`, `chat`, `settings`, `code`, `camera`,
  `image`, `video`, `calendar`, `clock`, `home`, `power`, `lock`, `link`, `cloud`, `download`, `star`, `heart`, `bolt`,
  `chart`
- The lists only grow: a key is never renamed or removed, and a new key is added to the list before a widget asks for it
- A key outside the lists is allowed in a manifest and is never asked for by a built-in widget

#### Scenario: Weather keys are the widget's
- **WHEN** the weather widget maps a condition to its icon name
- **THEN** the name is one of the 13 `weather` keys

### Requirement: Icon File Rules
An icon file MUST be small, flat vector art that can be drawn with the theme's stroke, so that the host can restyle it
without a general SVG renderer.

- At most 4 KB, UTF-8, with a root `svg` element whose `viewBox` is `0 0 24 24`
- Only the elements `path`, `circle`, `ellipse`, `line`, `polyline`, `polygon` and `rect` (with `rx` and `ry`), and the
  `svg` and `g` containers; no `style`, `script`, `image`, `use`, `text` or `defs`, no `transform` attribute and no
  reference to another file
- The art is outlines: the host draws every shape as a stroke of the theme's width, with round caps and joins unless the
  theme's stroke cap is square. A shape with `fill="currentColor"` is also filled with the icon's colour
- The host reads only geometry and the `fill="currentColor"` mark; colours, stroke widths and other presentation attributes
  in the file are ignored
- A file that breaks these rules, or that cannot be read, gives its key no icon and is logged once

#### Scenario: Outline icon
- **WHEN** an icon of one path and two circles is drawn at 96 pixels with a stroke of 0.0667
- **THEN** each shape is stroked 6.4 pixels wide in the icon's colour

#### Scenario: Filled mark
- **WHEN** a circle in the file has `fill="currentColor"`
- **THEN** that circle is filled and stroked, and the other shapes are only stroked

#### Scenario: A transform
- **WHEN** an icon file has a `transform` attribute on a path
- **THEN** the key has no icon from that file and one line is logged

### Requirement: The Built-in Outline Set
The product MUST ship an icon set named `outline` inside the engine, covering the 13 `weather` keys and the 24 `app` keys.

- The icons are copied once from the Lucide icon library at a release the manifest names in `upstreamVersion`; each file
  keeps its upstream name and the manifest maps the product's keys to them
- The set carries the library's licence text and its manifest names the licences (ISC, and MIT for the icons derived from
  Feather); no credit is drawn anywhere in the running application
- Building the set needs no network access, and the product has no runtime dependency on the library
- The set passes every check of "Findings About Icon Sets" (theme-lint) with no finding

#### Scenario: Every weather key
- **WHEN** the built-in set is asked for each of the 13 weather keys
- **THEN** each gives an icon

#### Scenario: Provenance
- **WHEN** the built-in set's manifest is read
- **THEN** it names Lucide as the source, the release and the licences

### Requirement: Resolution Chain
A request for an icon by key MUST be answered by the first of these that has the key:

1. the set the theme names (`icons.set`): a folder `icons/<set>/` inside the theme's folder, else the built-in set of that name
2. the built-in set `outline`
3. nothing, in which case the widget draws its own icon (the real icon of a shortcut; the animation of weather)

- A theme that names no set, or a set that is not found, uses step 2 for glyphs a widget asks for by key and is marked as
  naming no set (see widget-sdk, "Theme Carries The Icon Values")
- A theme folder's own set is found only in that theme's folder
- The chain is built when the theme is resolved; answering a request does no I/O

#### Scenario: The theme's set lacks the key
- **WHEN** a theme names a set whose manifest has no `app/bolt`
- **THEN** a request for `app/bolt` is answered by the built-in `outline` set

#### Scenario: Unknown set name
- **WHEN** a theme names the set `nosuch`, which is neither in its folder nor built in
- **THEN** requests are answered by `outline`, one line is logged, and the theme is marked as naming no set

### Requirement: Icons Are Read Once
The host MUST read and parse an icon set when it resolves the theme that names it, and keep the parsed shapes for as long as
the theme is in use.

- Drawing an icon reads no file and parses no text
- A set is shared by every surface size, the compact cards and the dock of one theme, and is not read again for them
- The shapes of a whole set take no more than a few tens of kilobytes; a set holds no bitmap

#### Scenario: A resize
- **WHEN** the display scaling changes and the theme is resolved again for the new size
- **THEN** the set's files are not read again

#### Scenario: Paint
- **WHEN** a widget draws an icon during a paint
- **THEN** no file is opened

### Requirement: Media Keys
The product's canonical keys MUST include `media/play`, `media/pause`, `media/previous` and `media/next`, and the built-in
set MUST map them to icons (the play, pause, skip-back and skip-forward glyphs of the upstream set it already carries,
with their licence unchanged). A set that omits them is answered by the resolution chain like any other omitted key, and
a widget that draws them has a text fallback for the case where no set has them.

#### Scenario: Built-in set
- **WHEN** a theme uses the built-in set
- **THEN** all four keys have an icon

#### Scenario: A user set without media keys
- **WHEN** a theme's own set maps none of the media keys
- **THEN** the resolution chain supplies the built-in icons, as it does for any other omitted key
