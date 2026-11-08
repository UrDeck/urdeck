## Why

Typing reading ids such as `system:gpu/temperature` or `homeassistant:light.desk_strip` is the hardest part of editing a
page by hand, and Home Assistant alone can have more than a thousand entities. The editor's reading setting needs a
picker that finds a reading by name, room or id, shows its live value, and only offers what the setting accepts
(`docs/handoff/2026-11-06-editor-design.md`, artboard 4; `docs/handoff/editor-design/ReadingPicker.dc.html`; the
picker dialog in `Main.dc.html`). `editor-forms` ships a minimal "type an id" chooser; this change replaces it.

Found while designing it: only Home Assistant can list its concrete readings today; `system` (the readings every user
has) cannot, and no reading carries the area (room) it belongs to.

**Order:** after `editor-forms` (it implements the form's `IReadingChooser`). Independent of
`editor-theme-background`, `editor-arrange` and `editor-states`.
**Model:** Sonnet.

## What Changes

- **The reading picker**: a dialog with a search box, provider tabs (All and each provider), area and kind filters
  when the provider supplies areas, a count ("9 of 1,192 match"), a grouped, virtualised list (by category, then area)
  whose rows show the label and id with the matched text in bold, an "action" tag on readings that can be switched,
  and the live value; "Type an id by hand" for anything not listed. Opened from a reading setting, pre-filtered by
  what it accepts (has a range, has an action, a provider).
- **Loaded once, filtered in the editor**: each provider's listing is read once when the picker first needs it and
  kept for the editor session; search and filters run in memory (a thousand rows filter instantly). No search request
  goes to Home Assistant per keystroke.
- **Live values only for the rows on screen**, subscribed as rows appear and released as they scroll away; Sample mode
  shows sample values.
- **A Readings section** in the editor's navigation: the same list as a page, for browsing what the PC and the
  providers offer, with "Copy id".
- **Not connected**: when a provider cannot list (Home Assistant without address or token, or offline), the picker and
  the form say so with "Set up" (to the Providers section), and the picker still shows the readings the pages already
  use, from the last successful listing, without values.
- **Areas and categories**: a reading description may name its area (room) and a category (for example "Lights");
  Home Assistant fills them from its area, device and entity registries.
- **System lists its readings**: the `system` provider lists its concrete readings (the CPU and each logical
  processor, memory, each GPU adapter), so the few dozen readings every user has are browsable.
- Providers that cannot list show their catalog's fixed readings; a catalog pattern (`{n}` in the path) is offered as a
  template that opens "Type an id by hand" pre-filled.

## Capabilities

### New Capabilities

- `reading-picker`: finding and choosing a reading in the editor: search, filters, groups, live values, providers that
  are not connected, the readings section.

### Modified Capabilities

- `data-providers`: "Reading Catalog" lets a description carry an area and a category.
- `provider-homeassistant`: "Listing Entities" fills area and category from Home Assistant's registries.
- `provider-system`: gains a listing of its concrete readings.

## Impact

- `sdk/UrDeck.Sdk/Data/ReadingDescriptor.cs`: `Area`, `Category` (SDK minor bump).
- `providers/UrDeck.Providers.HomeAssistant`: registry requests over the existing WebSocket connection at listing time;
  fixtures for the registry messages (fictional names).
- `providers/UrDeck.Providers.System`: `IReadingBrowser`.
- `src/UrDeck.Engine`: `ReadingDescriptionCache` (`cache/readings.json`, descriptions of readings in use), a
  `ReadingIndex` (search and filter, Linux-tested), a subscription lease API for non-widget subscribers if `ReadingHub`
  lacks one.
- `src/UrDeck.Host/Editor/`: `ReadingPickerDialog`, `ReadingsSection`, the form's "not connected" InfoBar.
- `urdeck-cli readings browse` shows area and category.
