## Why

A reading today is one value: a number, a text, an on/off state or a time. Much of what a desk display is for is a short
list: Steam friends who are playing (issue #162), recently played games, downloads in progress, calendar events, a Home
Assistant to-do list, headlines, open pull requests. Without a list shape every provider invents indexed paths
(`friends/0/name`, `friends/1/name`, ...), whose rows tear apart when an update lands half-way, which the editor cannot
offer as one thing, and which every widget has to reassemble. The Steam provider (`steam-provider`) is the first user;
it should not be the last one to build its own.

## What Changes

- **A list value and a list kind in the SDK.** A list reading's value is a list of rows. Each row has a stable key and
  named fields, and each field holds one of today's values (number, text, on/off, time). The description declares the
  fields: name, kind, label, unit and decimals as for a reading. Roles on fields tell any widget which field is the
  row's title, its detail line, its status, its image and its link, so a widget that has never heard of Steam can show
  the friends list. A list is replaced as a whole, so its rows never tear, and an unchanged list does not cause a repaint.
- **Bounds.** A list has at most 200 rows, and a text field at most 500 characters. A provider that publishes more is cut
  to the bound, and the cut is logged once.
- **A list read where one value is expected is its number of rows.** The stats widget shows "friends in a game" from
  the list itself, with no count reading from the provider.
- **The formatter formats a field** exactly as it formats a reading of that kind (units, region, decimals).
- **Samples can be lists**, including keyframes that swap the rows, for previews, the golden images and `--demo`.
- **The text list takes an image per item and is fed straight from a list reading**, using the roles.
- **The editor knows lists.** A reading setting may ask for list readings only. The picker marks a list reading and
  shows its row count as its live value.
- **A generic List widget** (`urdeck.widgets.list`) shows any list reading: rows with title, detail, status and image,
  sized by the theme, scrolled or rotated, and a tap on a row opens its link.
- **A first list in an existing provider:** `system:processes/top`, the processes using the most CPU (name, CPU %,
  memory), with a sample. It proves the shape outside Steam and gives the List widget its preview and golden images
  before `steam-provider` lands.

Not in this change: lists of lists (a field holding a list); sorting or filtering rows in the widget (a provider
publishes the order and the rows it means; filtering is a later setting); per-row actions other than opening a link;
editing a list from the deck (ticking a to-do).

## Capabilities

### New Capabilities

- `widget-list`: the List widget: its settings, how it draws rows by role at each size, scrolling and rotation, taps,
  empty and unavailable states, its sample.

### Modified Capabilities

- `data-providers`: a fourth value shape (list), the list kind with field descriptions and roles, bounds, whole-list
  replacement and equality, a list read as one value is its count, field formatting.
- `sample-data`: list readings in a sample file, fixed or as keyframes.
- `text-layout`: an optional image per text list item; feeding a text list from a list reading.
- `settings-metadata`: a reading setting may accept list readings only, or exclude them.
- `reading-picker`: lists marked, with their row count as the live value; the restriction to lists.
- `provider-system`: the `processes/top` list reading.

## Impact

- `sdk/UrDeck.Sdk` (MIT): `ReadingValueType.List`, `ReadingList` and `ReadingRow` (immutable, structural equality),
  `ReadingKind.List`, `ReadingField` descriptions and `FieldRole` on `ReadingDescriptor`, `ReadingFormatter` for a field,
  `TextList` item image and a `SetItems(list, descriptor, ...)` helper, `ReadingSetting` accepts or excludes lists. Additive;
  one minor version. A widget built before lists that reads a list gets its count, as a number.
- `src/UrDeck.Engine`: `ReadingHub` bounds and compares lists; `SampleFile` reads list samples; the catalog export and the
  editor's picker carry fields and roles.
- `src/UrDeck.Host`: picker badge and count; the form's reading setting filter.
- `providers/UrDeck.Providers.System`: `processes/top` (sampled only while shown) and its sample.
- `widgets/UrDeck.Widgets.List` (new, GPL, SDK only), added to the host's `UrDeckPlugin` list, with golden images.
- `tools/UrDeck.Cli`: `preview render` and `catalog export` handle lists; `readings browse` prints a list's rows.
- Site: the reading reference (list readings, fields and roles), the List widget's page, the SDK page for provider
  authors.
- No network, no new package.
