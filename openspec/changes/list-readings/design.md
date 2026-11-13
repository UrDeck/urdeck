## Context

`ReadingValue` is a 24-byte struct with four shapes (number, text, on/off, time), compared by value; the hub
(`ReadingHub`) stores the last value per subscribed path and repaints a widget when one of its readings changes. The catalog
(`ReadingDescriptor`) describes kind, label, name, unit, range, levels, action and icon. `TextList` (text-layout) draws
items with text, a muted second line and a leading mark, and scrolls, follows or rotates. Samples (`SampleFile`) give each
reading a fixed value or keyframes. Widgets reference only the SDK.

The first user is the Steam provider (`steam-provider`): friends playing, recent games, downloads. Others in sight are a
calendar, Home Assistant to-do lists and RSS headlines (the text list was built for headlines and chat already).

## Goals / Non-Goals

**Goals:** one SDK shape for "a few rows of a few fields" that any widget can draw without knowing the provider; no
tearing; cheap equality so an unchanged list costs nothing; works in samples, the picker and the stats widget on day one.

**Non-Goals:** a table or grid component; lists nested in lists; sorting, filtering or paging in the widget; huge lists
(an inbox); per-row actions beyond a link.

## Decisions

1. **A list is a value, not a family of paths.** `ReadingValue` gains a fifth shape, `List`, holding a reference to an
   immutable `ReadingList` (rows in order). The provider publishes the whole list with one `Publish(path, value)`; the
   hub swaps it atomically. Rejected: indexed paths (`friends/{n}/name`) with a count reading: rows tear between
   publications, the picker cannot offer "the friends list", and each widget reassembles rows. Rejected: a separate
   `PublishList` channel and `ReadList` call: every state rule (ok, pending, stale, unavailable, interruptions, samples)
   would be written twice.

2. **Rows have a key and named scalar fields.** `ReadingRow` = `Key` (text, unique in the list, stable across updates:
   a SteamID, an app id) and fields by name, each a scalar `ReadingValue` (a field holding a list is refused). The key lets
   a widget keep the scroll position on the same row, animate a row that moved, and remember which row was tapped. A
   field missing from a row reads as absent, not as an error, so a friend with no game simply has no `game`.

3. **The description declares fields and roles.** `ReadingDescriptor.Fields`: for each field its name, kind (any scalar
   kind), label and, where it applies, unit, display unit, decimals, range and levels. That is the reading description
   without a path, so the formatter formats a field with the rules it already has. Roles say what a field is for
   to a generic widget: `Title` (one), `Detail`, `Status`, `Image` (a local file path or an `http(s)` address, through the
   icon service), `Link` (a launch target). A list with no title role uses its first text field as the title. Roles are
   hints. The Steam widget reads the fields by name.

4. **Bounds in the hub, not in each widget.** At most 200 rows and 500 characters per text field. The hub cuts a larger
   publication to the bound (rows from the start, text with an ellipsis) and logs once per path, naming the provider. The
   numbers keep a misbehaving provider from feeding the UI thread megabytes, and they are far above what a deck shows.

5. **Equality is structural and cheap.** `ReadingList` computes its hash once at construction, and equality compares
   hashes before the rows. A provider that republishes the same friends every minute causes no repaint. That matters for
   polled lists, where the hub's existing "changed?" check is the only thing standing between a poll and a repaint.

6. **A list read as one value is its count.** `Reading.Value` for a list is the list. The formatter, asked to format a
   list reading as a value (stats, a readout), gives its row count as a whole number. A list description's
   `Unit` (for example "playing") is the count's unit. "Friends in a game" in the stats widget is then the list reading
   itself, and the provider does not publish a parallel count. Widgets built before lists get the count as a number,
   so nothing old breaks on a list.

7. **The text list draws images and takes a list directly.** An item MAY have a leading image (a decoded image the widget
   hands over, drawn as a square of the line height, rounded per the theme's image corner) instead of, or as well as, its
   mark. `TextList.SetItems(ReadingList, ReadingDescriptor, ...)` maps roles to item parts: Title to text, Detail to the
   second line, Status to the mark (its colour from the theme's status colours by the field's kind and level), Image to
   the image, and the row key to the item's key. Images arrive later than text, so an item draws without its image until
   the icon service has it, and never moves when it arrives.

8. **The List widget is thin.** One reading (lists only), a title (default: the reading's label), rows per size from
   the text list's own measure, `scroll` (manual, tap to page) or `rotate` (the text list's rotate mode) when the rows do not
   fit, and a tap on a row opens its Link through the launcher. Empty list: a muted line with the description's empty
   text (`ReadingDescriptor.EmptyText`, for example "Nobody is playing"), otherwise "Nothing to show". It draws no card and
   names no font.

9. **Samples.** A sample reading of kind `list` has `fields` (as the description) and a value that is an array of row
   objects (`key` plus fields). Keyframes hold, as text does: the rows swap at the keyframe's time and do not interpolate.

10. **SDK version.** All additive; one minor version. `ReadingValue`'s size does not grow: the list reference shares the
    text slot (`_text` becomes an `object?`).

## Risks / Trade-offs

- [A widget that switches on `ReadingValueType` with no default case misses lists] → the analyzer gains no rule; the
  SDK note and the site say a list reads as its count where a value is expected, and first-party widgets are checked
  in the tasks.
- [Images per row (avatars) cost decode and memory] → the icon service already caches by path and size; the text list asks
  only for visible rows; 200 rows is the bound.
- [Providers abuse lists for big data] → the bound and the log line; the site documents lists as "a few rows for a glance".
- [Roles are too few] → the five cover the known providers; a role is an enum that can grow without breaking old widgets
  (an unknown role is ignored).

## Open Questions

- Should the picker let the user pick a field of a list (`steam:friends` field `game`) as a scalar for a readout? Not now:
  a readout of one row needs a row choice too. Revisit with a real case.
