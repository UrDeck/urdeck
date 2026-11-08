Before starting: `editor-forms` must be applied (and therefore `editor-shell`, `settings-metadata`, `config-document`,
`deck-session`). Read `proposal.md`, `design.md` (D1–D10), the specs here, `docs/handoff/editor-design/ReadingPicker.dc.html`
and the picker part of `Main.dc.html`. Model: Sonnet.

## 1. SDK and providers

- [x] 1.1 `ReadingDescriptor.Area` and `.Category`; SDK minor version bump; `urdeck-cli readings browse` prints them
- [x] 1.2 Home Assistant listing (D8): area, device and entity registry requests over the existing connection, area resolution (entity, else device), categories by domain, degrade without registries (one log line); fixture tests with fictional names in the real message shape
- [x] 1.3 `system` implements `IReadingBrowser` (D9) without starting sampling; a shape test with a fake device enumeration; a `[WindowsFact]` that lists at least the CPU load and memory on the machine

## 2. Engine

- [x] 2.1 `ReadingIndex` (D2): entries from listings and catalogs (patterns as templates), search with ranking and match ranges, filters, grouping; tests including 1,500 fixture entries under 100 ms
- [x] 2.2 `ReadingHub.Lease(ids)` for non-widget subscribers if missing (counts as demand, released on dispose), with tests
- [x] 2.3 `ReadingDescriptionCache` (D7) in the cache folder: written after a successful listing for the readings the configuration uses, read when a provider cannot list; tests

## 3. Host

- [x] 3.1 `ReadingPickerDialog` (D3) implementing `IReadingChooser`; replaces the minimal chooser as the default (the "type an id" dialog stays for the footer and templates, D6)
- [x] 3.2 Visible-row leases and live values (D4), honouring Live/Sample
- [x] 3.3 Not connected InfoBar in the picker and above a reading setting in the form (D5), "Set up" navigates to Providers
- [x] 3.4 Readings section (D10) in the navigation, with Copy id

## 4. Verify

- [x] 4.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test`, coverage floor, `dotnet format --verify-no-changes`, Linux tests
- [x] 4.2 Owner, manual, with the disposable Home Assistant (`scripts/Start-HomeAssistantDemo.ps1`) and with the owner's own: search, filters, areas, choose for Stats and Control; scroll and watch values; disconnect HA (InfoBar, used readings still listed); system readings listed; Sample mode
- [x] 4.3 `openspec validate editor-reading-picker --strict`
