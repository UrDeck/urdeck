# 08 CLI, validation, documentation and website

## 1. `urdeck-cli theme ...` (new, mirrors `shader`)

`tools/UrDeck.Cli` already has `shader init|validate|render`. Add `theme` with the same ergonomics, output formats (text, JSON, SARIF) and exit-code
rules (`1` on error; with `--fail-on-warnings` also on warning). It references `UrDeck.Engine` (the CLI already does for shaders), so it validates with the **same** `ThemeDefinition.Sanitize`
the host uses; it must never have its own parallel rules.

| Command | Does |
|---|---|
| `theme validate <folder> [--all <dir>] [--fail-on-warnings] [--format text\|json\|sarif]` | schema + lint + asset checks below |
| `theme render <folder> -o out.png [--size 1100x1920] [--page sample\|<config.json>] [--background <name>] [--time <HH:mm:ss>] [--reduced-motion]` | renders the **sample page** through `PageRenderer` (cross-platform, no screen) |
| `theme init <name> [--from <builtin>]` | writes `themes/<name>/theme.json` with `format: 2`, a `$schema` line and commented-by-README starter values |
| `theme schema [--out file]` | prints the embedded JSON Schema |

`theme.json` gets an optional `"$schema": "https://urdeck.app/schemas/v2/theme.json"` for editor completion (ignored by the host, like the shader manifest).

### Finding codes (proposed; continue the SHADER### style)

| Code | Severity | Meaning |
|---|---|---|
| THEME001 | error, stops | `theme.json` missing or not valid JSON |
| THEME002 | warning | unknown key (with "did you mean X", edit distance <= 2) |
| THEME003 | warning | wrong JSON type for a key |
| THEME004 | warning | value outside its range (message carries the range) |
| THEME005 | warning | unknown enum value (lists the valid ones) |
| THEME006 | warning | invalid colour |
| THEME007 | warning | array length outside its limits |
| THEME010 | warning | `format` newer than the tool supports |
| THEME020 | error | font file missing, escapes the folder, or not `.ttf`/`.otf` |
| THEME021 | warning | font file > 3 MB, or no licence file next to it (`*-OFL.txt`, `LICENSE*`) |
| THEME022 | warning | font family not installed and not bundled (non-portable) |
| THEME030 | error | icon file escapes the folder, uses a forbidden element/attribute, or is > 4 KB |
| THEME031 | warning | icon set is missing canonical keys (lists them; info for `weather/` fallbacks) |
| THEME032 | warning | icon has no `viewBox="0 0 24 24"` |
| THEME040 | warning | `background.default.name` is not a known background (built-in or user) |
| THEME041 | warning | `meta.pairsWith.backgrounds` names an unknown background |
| THEME042 | warning | `background.default` is not in `pairsWith` |
| THEME043 | warning | static image missing, escapes the folder or > 8 MB |
| THEME050 | warning | the cross-field rules of `02 §5` (one code per rule: THEME050..THEME056) |
| THEME060 | warning | contrast: `text` on `cardFill` under 4.5:1 (composited over `background`) |
| THEME061 | info | contrast: `textMuted` under 3:1 |
| THEME070 | info | estimated animation cost class (none / light / heavy) from `motion`, `borderAnimate`, `valueFill.animate`, `pattern.scroll` |
| THEME080 | warning | theme has `meta` fields missing (name, description, mode) |

Rules: every message says what is wrong **and how to fix it**, one line, no stack traces (as the shader lint does).
`THEME002` needs the schema's key tree: build it from the embedded `theme.schema.json` once; do not hand-maintain a second list.
Each code has a unit test with a minimal failing theme folder fixture under `tests/UrDeck.Cli.Tests/Fixtures/themes/`.

### CI
Add to the workflow that already runs shader validation:
```
dotnet run --project tools/UrDeck.Cli -c Release -- theme validate --all src/UrDeck.Engine/Themes/Builtin --fail-on-warnings
```
and a test that every built-in theme renders the sample page without a logged warning.

## 2. Schema publication

- Copy `assets/schema/theme.schema.json` to `docs/schemas/v2/theme.json` (note the path: the shader schema is `docs/schemas/v1/shader-manifest.json`; keep the folder = the schema *file format version*,
  here `v2` because the theme format is 2). `$id` is `https://urdeck.app/schemas/v2/theme.json`.
- The site build already has a "schema copy" check (`site/` `npm run build`, see `site/README.md`): extend it to copy and verify the new file. Verify the `$id` matches the served URL.
- Add `"$schema"` to the eight built-in themes and to examples in docs.
- **Do not** make the engine depend on the schema at runtime. It is for editors, the CLI lint and tests.

## 3. Documentation to write or change (docs/ is user-facing, keep the existing voice: short, concrete, runnable)

| File | Change |
|---|---|
| `docs/themes.md` | Rewrite the settings section into: Quick start, the complete format-2 reference (generate the tables from the schema in a script `scripts/gen-theme-docs.ps1` or a docs test so they cannot drift), "Fonts and licences", "Icon sets", "Backgrounds and pairing", "Motion", "Writing a theme in 5 minutes" (start from a built-in, change 6 values), "Troubleshooting" (every THEME code). Keep the v1 text accurate: it still holds. |
| `docs/backgrounds.md` | Built-in shaders table (+12), `pairsWith`, the theme's `background.default/static/scrim`, precedence (copy from `07`), `"background": {"type":"none"}` |
| `docs/theme-authoring-cookbook.md` (new) | Recipes: chamfered cards, gradient border, hard shadow, LED bars, tach with redline, dotted page, circular dock, custom icons. Each recipe = 5-15 lines of JSON + a picture |
| `docs/perf/theme-look.md` (new) | Measurements, method, budget table (see `03 §5`) |
| `docs/schemas/v2/theme.json` (new) | The schema |
| `docs/BACKLOG.md`, `docs/ROADMAP.md` | Add the work items (one per PR in `09`), link to the OpenSpec changes |
| `README.md` | Replace the screenshot section with the new gallery; one paragraph on themes/backgrounds/pairing |
| `THIRD-PARTY-NOTICES.md` | Add every bundled font (name, version, copyright, OFL 1.1, URL) and the Meteocons entry stays. See `assets/fonts.md` |
| `src/UrDeck.Engine/Themes/Builtin/*-OFL.txt` | One licence text per bundled family |
| `openspec/specs/...` | Generated by Pat's skill from `09`; do not hand-write |

## 4. Website (`site/`, Astro)

Findings from the audit (`01`): decks are `site/decks/<theme>/urdeck-config.json`; pictures are found **by file name**; `themes.json` drives the gallery; `MEDIA-CHECKLIST.md` lists required files;
`scripts/Render-SiteDecks.ps1` renders decks; `npm run media` renders widget pictures and background posters/loops (loops need ffmpeg).

Work:
1. **Eight new deck folders**: `site/decks/<name>/urdeck-config.json` using the theme's name, the theme's default background, and the widgets listed in `06` (the "Site deck widgets" line of each theme).
   Use the sample-page fixture values (`07 §3`) so the pictures match the mocks.
2. **`themes.json`**: add the eight entries (name, tagline = `meta.description` first sentence, tags, pairs, default background). Make the gallery data-driven from each theme's `meta` rather than copying strings: a build script reads `src/UrDeck.Engine/Themes/Builtin/*/theme.json`.
3. **Backgrounds page**: add 12 backgrounds with poster + loop. Show "pairs well with" chips both ways (themes on a background card, backgrounds on a theme card).
4. **Pictures**: run `pwsh scripts/Render-SiteDecks.ps1` and `npm run media` (Windows). Tick `MEDIA-CHECKLIST.md`. Until real renders exist, the mock images in `assets/design-board/` are acceptable **interim** pictures, marked as such in the checklist (the checklist already has an "interim" convention).
5. **Copy:** headline for the new section, e.g. "Eight looks. Twelve moving backgrounds. They match.": audience split (gamers / IT & business) as two filters on the gallery.
6. **Trademark hygiene in public copy:** do not mention Stargate, Star Trek or HYTE Nexus's trade dress in site text for Warp Gate; describe it as "a wormhole warp tunnel". (The Nexus comparison already on the site stays as it is.)
7. Run `npm run build` (its checks: schema copy, internal links, third-party references). The third-party check will want every font listed.

## 5. Tests that keep this honest (summary; per-PR detail in `09`)

- Golden snapshots for all 11 themes (`03 §6`).
- Theme validator unit tests per code.
- Schema-vs-engine test: every leaf key in the schema is read by `ThemeDefinition` (reflection walk) and vice versa. This is the "no drift" test; it is cheap and catches forgotten keys.
- Built-in assets test: every built-in theme's fonts, icon sets and static images exist as embedded resources.
- Pairing symmetry test for built-ins.
- Docs test: every built-in background and theme is listed in docs.
