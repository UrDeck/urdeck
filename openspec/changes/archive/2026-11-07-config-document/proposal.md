## Why

The editor writes the configuration as the user edits, with Undo and Redo, while the deck in the same process shows
every change (`docs/handoff/2026-11-06-editor-design.md`, "Apply as you edit"). Today's `ConfigStore` cannot carry that:

- `Save()` serialises the typed `UrDeckConfig` whole. A generated form edits keys of plugin types the engine does not
  know at compile time (and of widgets that are not installed), so the editor needs to edit the JSON by path, not
  through C# objects.
- The deck ignores file changes for 2 seconds after its own `Save()` (`_ignoreChangesUntil`), so an edit saved by the
  editor would never reach the deck in the same process. The file is storage, not the way the editor talks to the deck.
- Three places write the file independently (`HostContext.SaveConfiguration`, also on exit; `SecretStores.MoveFromConfigAndSave`;
  `ProviderSettingsEditor`), and the host rewrites the file on every exit.
- There is no undo, and a file changed outside while the editor is open would silently fight its edits.

The editor is the product's front door; hand-written JSON (comments, formatting, key order) is not a design constraint
(decided 2026-11-06). Data still must never be lost: keys of plugins that are not installed and keys of newer versions
survive every write.

**Order:** first wave of the editor batch, no dependencies (parallel with `settings-metadata` and `deck-session`).
`editor-shell` needs it.
**Model:** Sonnet. Pure engine code with fixture tests; the design pins the API.

## What Changes

- A **configuration document** in the engine: the configuration as a JSON tree in memory, the only writer of
  `urdeck-config.json`. Every change is an edit by path (set, remove, insert, move) grouped in a transaction with a
  label and an origin (user, host, outside).
- **Changes apply in-process first**: the document raises a change event; the typed configuration the deck uses is
  rebuilt from the tree and the deck reacts within half a second, without going through the file.
- **Writing**: half a second after the last change the whole file is written (consistent indentation, keys in the
  order the tree has them, nothing dropped), atomically. The document remembers what it wrote; a file event whose
  content equals that is its own write. **BREAKING** (behaviour): the host no longer writes the configuration on exit;
  a file with comments loses them on the next write (accepted).
- **Undo and Redo** of user transactions (snapshots of the tree, up to 100 steps); typing in one field merges into one
  step. Host edits (remembering the display, moving a secret out of the file) are not undoable and are carried into
  the history so an undo never brings them back.
- **Changes from outside**: a changed file is read; a valid one becomes an undoable "changed outside" transaction; an
  invalid one is reported (with the line) and the current configuration is kept.
- A file from a **newer version** opens read-only: the document refuses edits and never writes (as the host already
  does today).
- `ConfigStore` becomes the typed view over the document; `Save()` is removed and its three callers edit the document.

## Capabilities

### New Capabilities

- `config-document`: the in-memory configuration document: edits by path, transactions, change notification, writing
  the file, undo and redo, outside changes, invalid files, read-only files.

### Modified Capabilities

- `host-shell`: "Configuration Management" no longer saves on exit, writes through the document, and recognises its own
  writes by content instead of a time window.

## Impact

- `src/UrDeck.Engine/Config/`: new `ConfigDocument.cs`, `ConfigEdit.cs` (paths and operations), `ConfigHistory.cs`
  (undo); `ConfigStore.cs` reduced to loading, migration and the typed view; `UrDeckConfig` unchanged.
- `src/UrDeck.Engine/Secrets/SecretStores.cs`, `src/UrDeck.Engine/Settings/ProviderSettingsEditor.cs`,
  `src/UrDeck.Host/HostContext.cs`, `src/UrDeck.Host/MainWindow.cs`: use the document.
- `tests/UrDeck.Engine.Tests`: document tests with fixture files and a fake `TimeProvider` (Linux-runnable).
- No SDK change, no new package. `--snapshot` and `urdeck-cli preview render` keep reading the file and never write.
- `deck-session` (applied in parallel or before) subscribes to the same typed `ConfigChanged`; whichever lands second
  adapts the subscription.
