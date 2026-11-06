# Design brief: the UrDeck editor

**For:** Claude Design, to make clickable mock-ups of the editor (milestone 3, backlog item 11). The design is the
baseline for the OpenSpec proposals that build it; it is not the spec, and the proposals decide behaviour.
**Date:** 2026-11-05.
**Read first:** `README.md`, `docs/ROADMAP.md` (milestone 3), `docs/BACKLOG.md` item 11, the user documentation in
`site/src/content/docs/` (start with `pages-and-dock.md`, `widgets/`, `themes.mdx`, `backgrounds.mdx`), and the sample page
`src/UrDeck.Preview/SamplePage/urdeck-config.json`.

## The problem

UrDeck is an open source widget dashboard for case displays and second screens. The first target is the HYTE Y70 Touch, a
1100x3840 portrait panel. Today a page is built by editing `urdeck-config.json` by hand: placing widgets by column and
row, typing reading ids such as `system:cpu/load`, and looking up each widget's settings on the website. The deck reloads
as soon as the file is saved. This works for the owner and is a poor experience for everybody else. The editor's goal,
from the roadmap: **a new user builds and changes a page without opening a file.**

Who uses it: somebody with a case display or a second monitor who has installed UrDeck from GitHub. They know their PC and
are not programmers. Most of them also have, or had, HYTE Nexus, whose editor splits work between a desktop app and the
touch screen. UrDeck deliberately does not do that (see below).

## What is fixed

These are decided. Please design within them.

- **All editing happens on the desktop, never on the panel.** Touch on the panel is for using the deck: swiping pages,
  tapping, scrolling. The editor is an ordinary window on the user's main monitor and shows its own preview of the
  page. Some screens have no touch at all.
- **Windows, WinUI 3.** The editor is built with WinUI 3 and Fluent controls (NavigationView, ListView, ComboBox,
  NumberBox, ToggleSwitch, Slider, ColorPicker, AutoSuggestBox, TeachingTip, InfoBar, ContentDialog). Please design with
  that vocabulary so the result can be built. The app follows the Windows light and dark settings. It must not look like a
  web page inside a window.
- **It grows from the existing settings window.** A left click on the tray icon opens "UrDeck Settings"
  (`src/UrDeck.Host/SettingsWindow.cs`). That window has two sections today: the monitor picker (each display with its name,
  size, orientation and number, plus Identify and Choose automatically) and Providers (a form generated from each data
  provider's declared settings, with secrets such as a Home Assistant token). The editor can be more sections of this
  window or a window of its own; that choice is open (see below).
- **The user never sees resolutions or scaling.** There are no pixels, DPI or font sizes in the UI. Widgets take their
  whole look from the theme.
- **Forms are generated, not designed for each widget.** Plugins (widgets and data providers written by other people)
  add settings the editor knows nothing about in advance. A widget declares its settings in code, and the editor builds a
  form from that declaration. **Design a small set of form controls that every widget's settings are built from, not a
  custom panel for each widget.** The built-in widgets below are examples of what that set has to cover. The controls you
  need are what the SDK's settings metadata will have to express, so this is the most useful part of the design.
- **The file stays the source of truth.** The editor writes `urdeck-config.json` and the deck, which is a separate
  window (usually on the case display), reloads it. People who like editing JSON must still be able to.

## The model to design around

The data model is in `src/UrDeck.Engine/Config/ConfigStore.cs` (`UrDeckConfig`, `PageConfig`) and
`sdk/UrDeck.Sdk/Config/WidgetConfig.cs`. In short:

- **The grid.** Each page is a grid of **four square columns** that span the screen's width. On the 1100x3840 panel that
  gives about a dozen rows, after the dock and the page indicator. A widget has a position (`col`, `row`) and a size in cells. It can only take one of the
  sizes it declares: 1x1, 2x1, 2x2, 4x1, 4x2 or 4x4 (width x height). Widgets do not overlap.
- **Pages.** There are one or more pages, each with a name and a list of widgets. On the deck the user swipes between
  pages, and a page indicator (dots) shows which one is current. `pager.indicator` is `auto`, `always`, `fade` or `off`.
- **The dock.** A row of up to four widgets at the bottom of the screen, the same on every page. It holds any widget with
  a 1x1 size, drawn smaller. It has no positions, only an order.
- **Theme.** One for the whole deck, chosen from eleven built-ins (`default-dark`, `default-light`, `glass`, `carbon`,
  `paper`, `ember`, `blueprint`, `prism`, `obsidian`, `pop`, `rigline`) or a user's own theme folder. It sets colours,
  fonts, the card, gauges, icons and motion.
- **Background.** One for the whole deck (per-page backgrounds are not planned). The choices are: none; one of sixteen
  built-in moving shaders, which draw with the theme's palette and have a few parameters; an image file; or whatever the
  theme suggests. Some themes recommend particular backgrounds (`PairingIndex`). Moving backgrounds have a cost, so the UI
  should make "still" an easy choice.
- **Readings and providers.** Data comes from providers: `system` (CPU, GPU, memory), `weather`, `media` and
  `homeassistant`. Plugins can add more. A reading has an id like `system:gpu/temperature` or
  `homeassistant:light.desk_strip`, a label, a unit and a live value. Some providers can list their readings and be
  searched: Home Assistant has more than a thousand. Some readings have an action as well (toggling a light).
- **Monitor.** Which display the deck appears on. The settings window already has a picker for this.

### The built-in widgets and their settings

The full lists are on the site (`site/src/content/docs/widgets/*.mdx`). They are listed here so you can see what the
generic form controls have to cover.

| Widget | Sizes | Settings, and the kind of control each needs |
|---|---|---|
| Clock | 4x2, 4x1, 2x1, 1x1 | 12/24-hour (choice), style `simple` or `flap` (choice), show date, week and seconds, blinking separator (on/off), header and subtitle (text) |
| Stats | 1x1, 2x2, 4x2, 4x4 | a list of **slots**, one per position for the size (1 at 1x1, up to 7 at 4x4). Each slot has a reading (reading picker), label and unit (text), decimals (number), gauge style (choice with 9 values), min and max (numbers), and warning and critical levels (numbers) |
| Weather | 4x2, 4x4 | place (a search that resolves to coordinates), label, unit `celsius` or `fahrenheit` (choice, default from the region), forecast days 1 to 7 (range, 4x4 only), icon motion `periodic`, `full` or `off` (choice), animate the forecast icons (on/off, 4x4 only) |
| Shortcut | 1x1 | target: an application, file, folder or web address (a file picker or URL); arguments; icon (automatic, a file or a glyph); label |
| Now playing | 4x2 | show album, show app (on/off), text shown when nothing is playing |
| Control | 1x1, 2x1 | reading (picker, restricted to readings that have an action), action (picker), label, icon (glyph picker), style `switch` or `dimmer` (choice), level reading (picker) |

The Stats slots are the hardest case: a list whose length depends on the widget's size, where each item is a small form
with a reading picker in it. Shortcut and Control need pickers backed by the operating system (files) or by a provider
(readings, actions, icons).

## What to design

In priority order. Each is useful on its own and can ship separately.

1. **Edit a widget.** Select a widget on the page preview and change its settings in a generated form; the preview shows
   the change. This covers the widget palette (add a widget: the list of available widget types with their sizes and a
   picture), changing size, and removing.
2. **Theme and background.** Pick a theme from pictures of each one, and a background from moving thumbnails, with the
   theme's recommended backgrounds shown first. Shader parameters and the scrim are a small generated form.
3. **Arrange pages.** Move and resize widgets on the grid by dragging, with snapping to cells and the widget's allowed
   sizes. Add, rename, reorder and delete pages. Edit the dock: add, remove and reorder its slots.
4. **The reading picker.** Searchable, grouped by provider, showing the live value next to each reading. It has to work
   with a thousand Home Assistant entities and also with the few dozen `system` readings.

Also show these states:

- the first run, with an empty page or the sample page;
- a widget that does not fit where it was dropped;
- a reading whose provider is missing settings (a Home Assistant token that is not set);
- a widget whose plugin failed to load, or that is in the file but not installed;
- a landscape monitor (1920x1080), where the four-column grid gives very large cells. How the grid should behave there is
  still open (item 15), so it is fine to suggest something.

## Open questions the design may answer

- Is the editor a section of the settings window or a window of its own? Arranging a tall portrait page wants room.
- Does the preview show live readings or sample data? The sample data exists for every built-in provider.
- Are changes applied to the deck as you edit, or on Save? Is there undo?
- How is the preview of a 1100x3840 portrait page shown on a landscape desktop monitor (the whole page scaled down, a
  scrolling column, a zoom control)?
- Where does the monitor picker go once the editor exists?

## Material in the repo

- **Widget pictures and loops:** `site/public/generated/widgets/<widget>-<size>-<theme>.mp4` (clock, stats and weather at
  each size in all eleven themes). These are rendered by the app's own renderer, so they show what a widget really looks
  like.
- **Whole decks per theme:** `site/public/generated/decks/<theme>.mp4`.
- **Backgrounds:** `site/public/generated/backgrounds/<name>.mp4`, and a still `sample.png` in each built-in folder under
  `src/UrDeck.Engine/Backgrounds/Builtin/<name>/`.
- **Theme files:** `src/UrDeck.Engine/Themes/Builtin/*.json`, for colours and fonts if you want the preview to match.
- **Pictures of anything else** can be rendered with
  `dotnet run --project tools/UrDeck.Cli -c Release -- preview render --theme <theme> -o page.png`, or with
  `--widget <typeId> --widget-size <WxH>` for one widget.
- **The current settings window:** `src/UrDeck.Host/SettingsWindow.cs` and its sections. It is unstyled; a design pass was
  always planned, so feel free to restyle it.

## Out of scope

Editing on the touch panel. Writing a theme or a shader (there are separate tools for those). The marketplace for
third-party widgets. Per-page backgrounds. The web page widget, which is milestone 4 (leave room in the widget palette
for a widget kind that hosts a web page, but do not design it).
