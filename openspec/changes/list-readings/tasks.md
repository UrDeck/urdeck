## 1. SDK

- [ ] 1.1 `ReadingValueType.List`, immutable `ReadingList` and `ReadingRow` (key, scalar fields by name, absent fields), structural equality with a hash computed once; `ReadingValue.FromList`; the list shares the text slot so the struct does not grow
- [ ] 1.2 `ReadingKind.List`; `ReadingField` (name, kind, label, unit, display unit, decimals, range, levels) and `FieldRole` (title, detail, status, image, link) on `ReadingDescriptor.Fields`; `ReadingDescriptor.EmptyText`
- [ ] 1.3 `ReadingFormatter`: a list read as one value formats as its count with the list's unit; `FormatField(row, field, ...)` with the scalar rules
- [ ] 1.4 Reading settings accept lists only, exclude them, or (default) accept everything; in the settings schema
- [ ] 1.5 `TextList`: leading image per item (square of the first line's height, theme corner, place kept while loading); `SetItems(ReadingList, ReadingDescriptor, formatter, icons)` by role, keyed by row key, keeping the scroll position on a surviving row
- [ ] 1.6 Doc comments stating the rules; one minor SDK version with the csproj note; `SdkContractTests` for the new members

## 2. Engine

- [ ] 2.1 `ReadingHub`: bounds (200 rows, 500 characters) with one log line per reading; refuse duplicate keys and list-in-list keeping the previous value; equality short-circuits repaints
- [ ] 2.2 `SampleFile`: list samples with `fields`, rows with `key`, keyframes that hold
- [ ] 2.3 Catalog export and the editor's picker data carry fields, roles and empty text
- [ ] 2.4 Hub and sample tests: whole-list swap, same list republished (no repaint), cut and logged once, duplicate keys, stale list keeps rows, interruption, list sample fixed and keyframed, absent field
- [ ] 2.5 Check every first-party widget against a list reading (count where a value is expected); stats test with a list slot

## 3. System provider

- [ ] 3.1 `system:processes/top` behind the existing platform seam: enumeration only while subscribed, shares from two samples, ten rows, names never logged
- [ ] 3.2 Fake-platform tests for the scenarios; a sample list of fictional processes in `sample.json`

## 4. Editor

- [ ] 4.1 Picker: list badge, row count as the live value, the lists-only and no-lists restrictions with their notice, fields shown under a chosen list
- [ ] 4.2 Readings section shows lists the same way; `PageEditing` tests (Linux) for a lists-only setting

## 5. List widget

- [ ] 5.1 Project `widgets/UrDeck.Widgets.List` (SDK only), settings per the spec, added to the host's `UrDeckPlugin` list
- [ ] 5.2 Drawing by role through `TextList.SetItems` at 1x1, 2x1, 2x2, 4x2 and 4x4; scroll and rotate overflow; states (empty text, pending, stale, unavailable with reason)
- [ ] 5.3 Taps: open a row's link through the launcher; page in scroll mode
- [ ] 5.4 Golden images from the system sample on the built-in themes; widget tests for taps and states

## 6. Tool

- [ ] 6.1 `preview render` and `catalog export` handle list readings and the List widget; `readings browse` prints a list's rows (key and fields)
- [ ] 6.2 Tool tests

## 7. Site and docs

- [ ] 7.1 Reading reference: list readings, fields, roles, bounds, the count rule; generated tables complete
- [ ] 7.2 The List widget's page with pictures (`npm run media`); the system provider's page gains `processes/top`; the SDK page for provider authors (publishing a list, choosing keys and roles)
- [ ] 7.3 README widget list; `docs/BACKLOG.md` entry
