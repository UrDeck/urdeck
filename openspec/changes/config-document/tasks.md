Before starting: read `proposal.md`, `design.md` (D1–D7 pin the API), the specs in this folder, and
`src/UrDeck.Engine/Config/ConfigStore.cs` in full. No other change has to be applied first; `deck-session` may land
before or after (D2 explains why there is no dependency). Model: Sonnet.

## 1. Document core

- [ ] 1.1 `src/UrDeck.Engine/Config/ConfigEdit.cs`: JSON Pointer parsing (RFC 6901 escapes), `ConfigEditor` with `Get`, `Set` (creates missing objects; `null` removes), `Remove`, `Insert`, `Move`; recorded list of touched paths
- [ ] 1.2 `ConfigDocument` (D1): `Root`, `Revision`, `IsReadOnly`, `Typed` (lazy `ConfigStore.Parse` with `warn` only for Outside/Load), `Edit` with rollback on exception, `Changed`, `Problem`; owner-thread assertion and the `post` delegate (D2)
- [ ] 1.3 `ConfigHistory` (D3): snapshot stacks (limit 100), merge by key within 1 s via the injected `TimeProvider`, `Undo`/`Redo` as transactions, labels, `StateChanged`; Host transactions replayed on all snapshots; debug assertion that Host transactions only set/remove
- [ ] 1.4 Fixture tests in `tests/UrDeck.Engine.Tests/Config/` (D7 first bullet) with exact expected text, and the undo/redo tests (D7 second bullet)

## 2. File edges

- [ ] 2.1 `IConfigFileSystem` seam (read shared with retries, atomic write via `.tmp` + move, change trigger) with the real implementation over `FileSystemWatcher` (1 s debounce, as today) and a fake for tests
- [ ] 2.2 Writing (D4): 500 ms debounce on the `TimeProvider`, skip equal bytes, retries then `Problem`, `Flush`, `LastWrittenHash`; `version` set as a Host transaction before the first write; the migration backup kept as today
- [ ] 2.3 Outside changes (D5): ignore own or already-seen content by hash; valid → Outside transaction (undoable); invalid → `Problem` with line and column, content kept, today's warning text; newer version → read-only, history cleared; an outside change cancels a pending write
- [ ] 2.4 Tests for 2.2 and 2.3 with the fake file system and `FakeTimeProvider`; one real-watcher test for the wiring

## 3. ConfigStore and its callers

- [ ] 3.1 Reduce `ConfigStore` to loading (first run, migration, `Parse`) plus the document: expose `Document`, `Config` (= `Document.Typed`), `IsNewerThanHost`; raise `ConfigChanged` coalesced 150 ms after the last `Document.Changed` (D6); delete `Save()`, `_ignoreChangesUntil` and `ReloadFromDisk`
- [ ] 3.2 `HostContext`: remembering a display is a Host edit of `/display`; `SaveConfiguration()` becomes `Document.Flush()`; check `RememberedDisplay` serialises to the same JSON shape as before (property names, nulls omitted)
- [ ] 3.3 `SecretStores.MoveFromConfigAndSave` / `SecretMover`: remove the moved secret's key through a Host edit; tests in the existing secrets tests updated
- [ ] 3.4 `ProviderSettingsEditor.Save`: one User transaction setting the non-secret keys; provider restart as today
- [ ] 3.5 `--snapshot`, `--demo` and `urdeck-cli preview render`: confirm none of them creates a writable document or writes (the existing first-run and snapshot tests still pass)
- [ ] 3.6 `MainWindow`: unchanged handler on `ConfigChanged`; `Program`/`App` exit path calls `Flush()` instead of saving

## 4. Verify

- [ ] 4.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format --verify-no-changes`, the Linux test projects
- [ ] 4.2 Host from the build output, read `urdeck.log`: start and quit without changes → the file's bytes are unchanged (compare a hash before and after); edit a widget setting in a text editor → the deck reloads once; save invalid JSON → warning, deck unchanged; delete `display` from the file → the host remembers it again and the log has no reload caused by its own write
- [ ] 4.3 Providers section: save a Home Assistant address → written to the file, provider restarts, nothing else in the file changes value
- [ ] 4.4 `openspec validate config-document --strict`
