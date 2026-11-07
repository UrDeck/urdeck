## Context

`src/UrDeck.Engine/Config/ConfigStore.cs` (402 lines) today:

- `Load()` reads the bytes (shared, with retries), parses with comments and trailing commas allowed into a `JsonNode`,
  reads `version`, runs migrations on the `JsonObject`, keeps the original bytes for the `urdeck-config.v<N>.json`
  backup, then deserialises into `UrDeckConfig` (`Parse`, which also warns about odd values).
- First run (`LoadMissing`): copy `urdeck-config.default.json` or write an empty configuration.
- `Save()`: refuses a newer file, writes the migration backup once, sets `version`, serialises the typed object with
  `UrDeckJson.Options` (indented, camelCase, nulls omitted).
- A `FileSystemWatcher` on the file, a 1 s debounce, `_ignoreChangesUntil` (2 s after its own save) to skip its own
  writes, `ReloadFromDisk()` raising `ConfigChanged(UrDeckConfig)`; a parse failure keeps the last good config.

Writers: `HostContext.SaveConfiguration` (on exit and when a display is remembered), `SecretStores.MoveFromConfigAndSave`
(called from `MainWindow`'s reload handler), `ProviderSettingsEditor` (Providers section's Save). Reader of
`ConfigChanged`: `MainWindow` (line ~177): reloads theme, background, motion, page names, provider settings, then
`ScheduleRebuild(force: true)`.

Every UI in the host runs on one thread (the deck window and the settings window share the dispatcher created in
`Program.Main`).

## Goals / Non-Goals

**Goals:**
- One object that owns the configuration's content, applies edits by path, tells everyone in-process, and writes the
  file behind them.
- Undo/redo that is trivially correct.
- Never lose a key the host does not understand.
- Tests that run on Linux without a real file watcher where possible (inject the clock and the file system edges).

**Non-Goals:**
- Preserving comments, formatting or the user's key order beyond what the tree keeps (the tree keeps key order; that is
  free, not a promise).
- Partial deck rebuilds (only the changed widget). The deck still reloads everything on a change; coalescing (D6) keeps
  that cheap enough. A scoped rebuild can come later using the change's paths.
- Page ids and `startPage` (in `editor-shell`), the editor's UI.

## Decisions

### D1. The document holds a `JsonObject`; the typed config is a projection

```csharp
namespace UrDeck.Engine.Config;

public enum EditOrigin { User, Host, Outside, Load }

public sealed class ConfigDocument : IDisposable
{
    public JsonObject Root { get; }                 // read-only by convention: never mutate outside Edit
    public int Revision { get; }                    // +1 per applied transaction
    public bool IsReadOnly { get; }                 // newer version (or opened read-only: snapshot, preview)
    public UrDeckConfig Typed { get; }              // rebuilt lazily after each change (ConfigStore.Parse of Root)
    public ConfigHistory History { get; }

    public event Action<ConfigChange>? Changed;     // raised on the owner thread, after Root and Typed are current
    public event Action<ConfigProblem>? Problem;    // an outside change that could not be read, a write that failed

    public bool Edit(string label, Action<ConfigEditor> edit, EditOrigin origin = EditOrigin.User, string? mergeKey = null);
    public void Flush();                            // write now if a write is pending (called on exit)
}

public sealed record ConfigChange(EditOrigin Origin, string Label, IReadOnlyList<string> Paths, int Revision);
public sealed record ConfigProblem(string Message, int? Line, int? Column);

public sealed class ConfigEditor                     // only valid inside Edit's callback
{
    public JsonNode? Get(string path);
    public void Set(string path, JsonNode? value);     // creates missing objects on the way; null removes the key
    public void Remove(string path);                  // no-op when absent
    public void Insert(string arrayPath, int index, JsonNode value);
    public void Move(string arrayPath, int from, int to);
}
```

- Paths are JSON Pointers (RFC 6901): `/pages/0/widgets/3/slots/1/label`, `~1` for `/` and `~0` for `~` in keys. A
  path that goes through a missing array index throws `ArgumentException` (a programming error in the editor; tests
  cover it).
- `Set(path, null)` removes the key: the editor never writes JSON `null` (absent and `null` mean the same everywhere
  in today's configuration, including `background`).
- `Edit` clones `Root` before running the callback (the snapshot the history keeps, see D3, and the rollback point):
  if the callback throws, it restores the snapshot and rethrows, so a failed edit changes nothing. Returns false (and does nothing) when the
  document is read-only.
- `Typed` is rebuilt with `ConfigStore.Parse(Root.ToJsonString(), warn: origin is Outside or Load)`, so warnings about
  odd values are logged once per outside change, not once per keystroke.
- **Why a tree and not typed objects:** the editor's forms are generated from settings metadata and edit keys of plugin
  configuration types loaded in collectible contexts, and of widgets whose plugin is not installed (raw JSON). Editing
  by path works the same for all three. Alternative considered: mutate `UrDeckConfig`/`WidgetConfig` and their
  `ExtensionData` through reflection: needs the concrete types, loses the unknown widgets' shape, and couples the editor
  to `System.Text.Json` binding details.

### D2. Threading

- The document belongs to the thread that created it (the UI thread in the host). `Edit`, `History` and the event
  handlers run there. A debug assertion checks it.
- The file watcher and the write timer run elsewhere; they post to the owner thread through a `post` delegate given to
  the constructor (`SynchronizationContext.Current.Post` in the host; immediate in tests). No dependency on
  `deck-session`'s scheduler, so the two changes can land in either order.
- Writing serialises `Root` to bytes **on the owner thread** (a few KB, well under a millisecond), then writes the bytes on
  the thread pool.

### D3. Undo by snapshots

```csharp
public sealed class ConfigHistory
{
    public bool CanUndo { get; } public bool CanRedo { get; }
    public string? UndoLabel { get; } public string? RedoLabel { get; }
    public bool Undo(); public bool Redo();          // each is a transaction with Origin User and label "Undo …"/"Redo …"
    public event Action? StateChanged;               // for the editor's Undo/Redo buttons
}
```

- Each User or Outside transaction pushes `(label, before: Root.DeepClone(), after: Root.DeepClone())` and clears the
  redo stack. Limit 100 entries (the configuration is a few KB; 100 × 2 × 10 KB is 2 MB at worst; drop the oldest).
- **Merging:** a transaction with a `mergeKey` equal to the top entry's, applied within 1 second of it, with no other
  transaction in between, replaces the top entry's `after` and keeps its `before`. The editor uses the setting's path
  as the merge key for text and number boxes, so typing "GPU temp" is one step.
- **Host transactions** (`EditOrigin.Host`) are not pushed. Their operations are replayed on every `before` and `after`
  snapshot in both stacks (the same `ConfigEditor` operations on each clone), so undoing a user step never brings back
  an old remembered display or a secret that was moved out of the file. Host edits only touch `display` and
  `providers.<id>.<secret key>` today; replay is exact for set/remove on those paths.
- Undo/Redo replace `Root` with a clone of the snapshot and raise `Changed` with `Paths = ["/"]`.
- Alternative considered: inverse operations per edit. Smaller memory, but every operation needs a correct inverse,
  including moves inside arrays that later edits shifted. Snapshots are trivially correct at this size.

### D4. Writing the file

- After a change, a write is scheduled 500 ms later; each further change restarts the timer. `Flush()` writes at once
  (the host calls it on exit and when the settings window closes).
- Bytes = `Root` serialised with `UrDeckJson.Options` (indented, as today) plus a final newline. `version` is set to the
  host's current version in the tree before the first write of a loaded file (as `Save()` does today), as a Host
  transaction.
- If the bytes equal the last bytes written or read, nothing is written.
- Atomic write: write `urdeck-config.json.tmp` in the same folder, then `File.Move(tmp, path, overwrite: true)`. On
  failure (locked, disk full): retry 3 times 200 ms apart, then raise `Problem("Could not save …")` and keep the
  pending state so the next change retries. The log says `Configuration written (N bytes)` at debug level only (no
  info line per write; the existing log has no per-save line either).
- The migration backup (`urdeck-config.v<N>.json`) is written before the first write of a migrated file, as today.
- Remember `SHA-256(bytes)` as `LastWrittenHash` and `LastSeenHash`.

### D5. The watcher: own writes and outside changes

- Same watcher as today (changed, created, renamed; 1 s debounce). On fire: read the bytes (shared, retries as
  `ReadShared`); if `SHA-256(bytes)` equals `LastWrittenHash` or `LastSeenHash`, ignore. This replaces the 2 s window.
- Otherwise parse (comments and trailing commas allowed, as today), migrate if older, then post to the owner thread:
  - **Valid:** an `Outside` transaction labelled "Changed outside the editor" whose callback replaces `Root`'s content
    with the parsed object. It is pushed on the history (undoable), so the editor's "Undo the reload" works.
    `LastSeenHash` = the hash of what was read.
  - **Invalid JSON:** raise `Problem(message, line, column)` from the `JsonException`; keep `Root`; log the warning
    the host logs today (`Config reload failed, keeping previous config: …`).
  - **Newer version:** the document becomes read-only, the content is taken (so the deck shows what it can), the
    history is cleared, and the warning is logged as today.
- A write the document has pending when an outside change arrives: the outside change wins (the pending write is
  cancelled), because the user's last act was editing the file. The editor shows the "changed outside" bar with Undo.

### D6. ConfigStore and the deck

- `ConfigStore` keeps: path resolution, first run, migration, reading bytes, `Parse`, `IsNewerThanHost`, and creates
  the `ConfigDocument`. It exposes `Document`, `Config` (= `Document.Typed`) and `ConfigChanged`.
- `ConfigStore.ConfigChanged` is raised **coalesced**: 150 ms after the last `Document.Changed` (a single timer on the
  owner thread), with the typed config. The deck's existing handler stays as it is; a burst of edits (dragging a slider)
  rebuilds the deck about six times a second at most. The editor's preview listens to `Document.Changed` directly
  (`editor-shell`).
- `Save()` is deleted. Callers:
  - `HostContext`: remembering a display = `Document.Edit("Remember the display", e => e.Set("/display", node), EditOrigin.Host)`;
    `SaveConfiguration()` on exit = `Document.Flush()`.
  - `SecretStores.MoveFromConfigAndSave`: the mover edits the document (remove `/providers/<id>/<key>`) as Host.
  - `ProviderSettingsEditor.Save`: one User transaction "Save <provider> settings" setting each non-secret key; then
    restart the provider as today.
- `--snapshot`, `--demo` without a file, and `urdeck-cli preview render` open the document read-only (no watcher, no
  writes) or keep using `ConfigStore.Parse` directly; nothing they do writes.

### D7. Tests

All in `tests/UrDeck.Engine.Tests/Config/`, Linux-runnable, using a temp folder and a fake `TimeProvider`
(`Microsoft.Extensions.Time.Testing.FakeTimeProvider`, add the package to `Directory.Packages.props` if the tests do not
already have one; check `ReadingHub` tests first):

- Fixture pairs `Fixtures/ConfigDocument/<case>.input.json` → operations → `<case>.expected.json` (exact text):
  set a nested widget key, remove a key, insert and move widgets, set `null` removes, unknown keys (a widget of an
  uninstalled plugin, a top-level key from a newer minor shape) survive a set elsewhere.
- Undo/redo: N edits, undo all, the root equals the original; redo all equals the final; merge within 1 s and not
  after; host edit replayed into history (remember display, edit a widget, undo: display still present).
- Writes: debounce (three edits in 400 ms make one write 500 ms after the last), no write when bytes are equal,
  `Flush`, atomic temp file not left behind, failure raises `Problem` and retries on the next change.
- Watcher logic with the file system edge abstracted (a test seam `IConfigFileSystem` with `Read`, `Write`,
  and a "changed" trigger), so most cases need no real watcher: own write ignored, outside valid change undoable,
  invalid JSON keeps content and reports line/column, newer version becomes read-only. One `[Fact]` with the real
  `FileSystemWatcher` checks the wiring end to end.

## Risks / Trade-offs

- [The deck rebuilds on every coalesced change, which may hitch on a busy page] → coalescing at 150 ms; measure while
  dragging a slider in `editor-shell` and record it; a scoped rebuild by `ConfigChange.Paths` is the follow-up if needed.
- [Comments and the user's formatting are lost on the first write] → accepted (GUI-first decision); said in the site's
  configuration page.
- [Host edit replay is only exact for set/remove of the paths the host touches] → the host's only edits are those;
  a debug assertion rejects a Host transaction that inserts or moves.
- [Two UrDeck processes on one data folder] → prevented by the single-instance guard (one host per data folder).

## Migration Plan

No file format change. Behaviour change: no save on exit. A file with comments loses them on the first change made in
the editor or by the host (remembering a display).
