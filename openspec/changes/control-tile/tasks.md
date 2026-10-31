## 1. Icons

- [ ] 1.1 Copy the Lucide files for the 21 `home` keys from the release the manifest names (README procedure), add the keys to the manifest and to the canonical key lists, and keep `theme validate` on a theme naming `outline` free of findings
- [ ] 1.2 Icon set tests: every `home` key resolves in the built-in set; a set without them falls back

## 2. Widget

- [ ] 2.1 Project `widgets/UrDeck.Widgets.Control` (SDK only), identity, sizes 1x1 and 2x1, category, `[RefreshOnData]`, added to the host's `UrDeckPlugin` list and the preview's widget set
- [ ] 2.2 Configuration with the absent / text / `""` rule for `action`, `label`, `icon` and `levelAction`, provider prefix for a bare action, unknown style as `switch`
- [ ] 2.3 State rules (on/off, the text list, numbers, neutral, pending, stale, unavailable) and colours from the theme
- [ ] 2.4 Tap and pending: `CanTap` false while pending or display-only, leaving pending on change, on done plus 1 s, on failed or the time limit (failed look 2 s); completions only set flags and request a repaint
- [ ] 2.5 `switch` drawing at 1x1 (stacked) and 2x1 (row) with the SDK components; dock placement
- [ ] 2.6 `dimmer` at 2x1: button plus bar gauge, tap-to-position with the 4 % ends, latest-wins while pending, falls back to `switch` at 1x1 or without `level` (logged once)
- [ ] 2.7 Repaint rules
- [ ] 2.8 Widget tests with fake readings and a fake invoker: every scenario of the widget-control spec

## 3. Pictures

- [ ] 3.1 Goldens (Windows): switch on, off, pending, failed, neutral and display-only; dimmer at 0, 40 and 100; in a dark and a light built-in theme
- [ ] 3.2 Sample page: control tiles on the Home Assistant sample entities; the site's media regenerated (`npm run media`)

## 4. Site and docs

- [ ] 4.1 Site widget page (generated configuration table, Home Assistant and media examples, the defaults rule, tap-to-position) and the catalog export
- [ ] 4.2 `README.md`, `docs/BACKLOG.md` (item 7, the widget; item 8, `IDragTarget` still open) and `docs/ROADMAP.md`

## 5. Verify

- [ ] 5.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, the Linux test projects, the site build, and `openspec validate --all --strict`
- [ ] 5.2 Manual on the panel: a light tile toggled against the owner's Home Assistant (pending, then on), fast taps, a dimmer set by tapping, a scene button with no reading, a tile in the dock, a failed action (wrong service)
- [ ] 5.3 Close #119 from the pull request description
