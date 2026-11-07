## Context

- `IReadingBrowser.BrowseAsync` (SDK) returns all concrete descriptions at once; `ReadingHub.BrowseAsync(providerId)`
  (engine, `ReadingHub.Refinement.cs`) starts the provider, calls it with a time limit and returns a `ReadingListing`
  (descriptions or a message). Only Home Assistant implements it (one `get_states`). `urdeck-cli readings browse`
  prints it.
- `ReadingDescriptor`: path, kind, label, name, device, min, max, warning, critical, unit, display unit, depth unit,
  decimals, attribution, action (natural action path), icon. No area, no category.
- Every provider has a catalog (`ReadingHub.Catalog`): exact paths and `{name}` patterns.
- `ReadingHub.Subscribe(ids)` is how widgets subscribe; the backlog (item 3) foresaw "an editor that lists the catalog
  with live values is a subscriber that is not a shown widget".
- `editor-forms`: `IReadingChooser.ChooseAsync(filter, current)`, the reading button, `IEditorReadings` (Live/Sample).
- Design: `ReadingPicker.dc.html` (A: the dialog with 1,192 entities searched for "desk"; B1: the form's "not
  connected" InfoBar; B2: the Providers section for Home Assistant), and the picker in `Main.dc.html` (its `pk`
  view model: chips, groups, `more`, `none`).

## Goals / Non-Goals

**Goals:** find any reading in under a few seconds by what the user calls it; never block on the network while typing;
cost nothing when closed.

**Non-Goals:** editing provider settings in the picker (it links to Providers); search on the provider's side;
listing for Weather and Media (their readings are fixed and come from the catalog).

## Decisions

### D1. Data: load once per editor session

- On first need (the picker or the Readings section opens), for each provider: `BrowseAsync` if it lists, else the
  catalog's exact paths plus its patterns as templates. Results are kept in a `ReadingIndex` for the editor window's
  life; a "Refresh" button in the picker re-lists. Listing runs off the UI thread; the list shows a progress ring per
  provider tab until it arrives.
- Alternative considered: search on Home Assistant per keystroke, as the mock-up's caption suggests: needs a search API
  HA does not offer over WebSocket, adds latency and network chatter. A thousand descriptions are about 300 KB in memory.

### D2. ReadingIndex (engine, tested on Linux)

- Entries: provider id, full id (`provider:path`), label, name, area, category, kind, has-range (min and max known, or
  percentage), has-action (natural action, or `ActionsFor` not empty), template flag.
- `Search(query, filter)`: split the query on spaces; every word must match (case- and accent-insensitive) one of label,
  name, path, area, category. Rank: label starts with the query > a label word starts with a word > path match > others;
  ties by label. Return match ranges for bold text.
- Filters: provider, area (values from the entries), kind (Lights, Switches, Sensors… = categories; for providers
  without categories, the reading kinds), has-range, has-action.
- Grouping for display: category then area ("Lights · Office"), falling back to device, then "Other".

### D3. The dialog

- `ContentDialog` (wide: `ContentDialogMaxWidth` raised to 760 DIP), title by filter ("Choose a reading", "Choose what
  it controls" for has-action).
- Search `AutoSuggestBox`-styled `TextBox` (no suggestions), focused on open, placeholder "Search N readings: name, id
  or room".
- Provider tabs (`SelectorBar` or segmented buttons): All, then each provider with readings; a provider that is not
  connected shows a dot.
- Area ▾ and Kind ▾ `DropDownButton`s only when the selected provider's entries have areas/categories.
- Count line: "9 of 1,192 entities match." plus "Showing only readings that can be switched or set." for has-action.
- `ListView` with grouping (`CollectionViewSource` with groups), virtualised; row: label (bold matches), id
  (monospace, bold matches), "action" tag, value (right). Templates show "template" and open D6 with the path
  pre-filled. Enter or double-click chooses; the current reading is selected and scrolled into view.
- Footer: "Can't find it? Type an id by hand" (D6) and Cancel.

### D4. Live values for visible rows

- On `ContainerContentChanging` (row realised) subscribe its id through a lease (`IReadingLease`, disposed on recycle
  and on close); batch subscriptions every 100 ms. If `ReadingHub` lacks a non-widget subscription API, add
  `ReadingHub.Lease(ids)` returning an `IDisposable` that counts as demand (the provider starts and lingers by the
  normal rules).
- Values formatted with `ReadingFormatter` and the default options; Sample mode reads the sample hub.
- Updating text in place on the realised containers; no list rebuild.

### D5. Not connected

- A provider whose listing fails with "missing settings" or an error: the tab shows a dot; selecting it shows an
  `InfoBar` "Home Assistant is not connected. It needs your server's address and a long-lived access token." with "Set
  up" (closes the dialog and opens Providers on that provider). The list shows entries from `ReadingDescriptionCache`
  for that provider (D7), with "—" as the value.
- On the widget form (`editor-forms`), a reading setting whose provider is missing required settings gets the same
  InfoBar above it ("Until then this widget shows "Set up" on the deck.").

### D6. Type an id by hand

The minimal chooser of `editor-forms` (an id box, the resolved label and value, "No provider has this reading" when the
id matches neither a path nor a pattern), opened from the footer or from a template row with the pattern pre-filled
and `{name}` selected.

### D7. ReadingDescriptionCache

- `cache/readings.json` in the data folder (`DataFolder.CacheFolder`): `{ "<full id>": <description as JSON> }` for the
  readings the configuration uses (all reading settings in all widgets, found through the settings schemas' Reading
  kinds), refreshed after each successful listing. Small, rewritten whole.
- Never holds values or secrets.

### D8. Areas and categories

- SDK: `ReadingDescriptor.Area` and `.Category` (`string?`, plain words).
- Home Assistant at listing: `config/area_registry/list`, `config/device_registry/list`, `config/entity_registry/list`
  over the existing connection (three requests, besides `get_states`); an entity's area is its own `area_id`, else its
  device's. Category from the domain: light → "Lights", switch → "Switches", sensor/binary_sensor → "Sensors", climate →
  "Climate", cover → "Covers", fan → "Fans", lock → "Locks", media_player → "Media", scene → "Scenes", script →
  "Scripts", others → the domain capitalised. Registry failure (older HA, permissions): list without areas, log once.
  Tests with hand-written fixtures in the real message shape and fictional names (the fixtures-first rule).
- Shown entities (subscriptions) keep the same descriptions; area and category are only filled when listing (no extra
  requests on the deck's path).

### D9. System listing

`system` implements `IReadingBrowser`: the concrete readings its sampler reports on this PC (CPU total and per logical
processor, memory, each hardware GPU adapter with its readings), with categories "CPU", "Memory" and "GPU". It must not start extra sampling; it describes what the catalog's patterns resolve to on this machine
(read the provider's device enumeration). Windows-only tests are `[WindowsFact]`; the shape test runs with a fake
enumerator.

### D10. Readings section

A navigation item "Readings" after "Theme and background" (or after Pages when that section is not there yet): the same
search, tabs, filters and list as the dialog, full page; a row's context menu "Copy id"; no choosing. Values live while
the section is shown.

## Risks / Trade-offs

- [Thousands of rows in a grouped ListView are slow to group] → group in the index, feed the view one flat list with
  header items if `CollectionViewSource` grouping is slow; measure with 1,500 fixture entries.
- [Subscribing to many HA entities while scrolling] → leases are batched and only visible rows (~15) are leased.
- [Registry requests need an admin token in HA] → they work for any user token; failure degrades to no areas.
