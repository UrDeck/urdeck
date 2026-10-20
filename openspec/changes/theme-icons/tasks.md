## 1. SDK: icon types and the themed icon

- [x] 1.1 Add `IIconSet`, `IconGlyph` and `IconShape` to `sdk/UrDeck.Sdk/Icons`, and `IconTheme` (shared unowned set, `NamesSet`, stroke, tint and colour, glow, owned glow cache) with `Theme.Icons` and its defaults; `Theme.Dispose` disposes the cache only
- [x] 1.2 Add `ThemedIcon.Draw` (centred square, stroke in glyph space, cap, filled marks, tint resolution including `ramp` position and `none`, glow from a bounded cache) returning whether it drew
- [x] 1.3 Give `ImageTile.Draw` the glyph key parameter: the glyph in place of the placeholder when the lookup answers, the placeholder otherwise
- [x] 1.4 Bump the SDK to 0.13.0; add tests in `tests/UrDeck.Engine.Tests` (or the SDK's own project if one exists) for the scenarios of components "Themed Icon" and "Image Tile" and widget-sdk "Theme Carries The Icon Values", including the shared set surviving a disposed `Theme`

## 2. Engine: parser, sets and the chain

- [x] 2.1 Write `IconSvgParser` (whitelist elements, `fill="currentColor"`, DTDs off, size/element/path limits) producing `IconGlyph`; reasons for rejection are structured so lint can report them
- [x] 2.2 Write the manifest reader and `IconSetStore` (embedded resources and a theme folder, path confinement on Windows and Linux, one log line per bad set or file, parsed once per origin)
- [x] 2.3 Copy the Lucide icons for the 13 weather and 24 `app` keys at a pinned release into `src/UrDeck.Engine/Icons/Builtin/outline/` as embedded resources with the licence text, a `manifest.json` and a short `README`; check every file against the file rules and replace the seven handoff seeds
- [x] 2.4 Build the composite lookup in `ThemeResolver` (theme set, then `outline`), set `NamesSet`, resolve the glow blur to pixels, share the set across surface sizes and the compact and dock themes
- [x] 2.5 Tests for icon-sets: manifest cases, two keys one file, escaping paths, whitelist rejections, chain fallback, unknown set name, no file read on a second resolve

## 3. Theme format and lint

- [x] 3.1 Remove `icons.animated` from `ThemeIconsDefinition`, the schema `docs/schemas/v2/theme.json`, `docs/themes.md` and the test fixtures; update the `icons` section of `docs/themes.md` (the tint `none` meaning, the chain, the manifest)
- [x] 3.2 Replace the "no sets exist" `THEME044` check with the real one and add `THEME070` to `THEME076` over theme-folder sets and `outline` (the orphan scan is the tool's alone); add the codes to `ThemeFinding`, the log snapshot test and the lint fixtures
- [x] 3.3 Re-add `"icons": { "set": "outline" }` to `carbon.json` and `paper.json` and drop their `animated`; confirm both check clean and the built-in themes still check clean

## 4. Weather

- [x] 4.1 Add set mode to `WeatherWidget`: branch on `Icons.NamesSet` in `Render`, skip the Lottie and poster paths, release what a previous paint loaded, make the animation properties idle
- [x] 4.2 Draw the main icon and each forecast day with `ThemedIcon.Draw` using `weather/<key>`, the daytime variant for forecast days, and the `ramp` position from a Celsius value kept next to each temperature reading
- [x] 4.3 Tests for widget-weather "Weather Themed Icons" (keys, night and day variants, ramp position in both units, no `IsAnimating`, no posters, unavailable condition) and the "Theme without a set" scenarios proving Meteocons mode is unchanged

## 5. Shortcut

- [x] 5.1 Read `theme:<key>` before the icon service in `ShortcutWidget`, draw the glyph through the tile and request nothing
- [x] 5.2 Pass a glyph key chosen from the target's kind (link, folder, file) when the icon service has given up and not while it is loading; check how `LaunchTarget` classifies a folder
- [x] 5.3 Tests for widget-shortcut "Shortcut Theme Glyphs" (the glyph, unknown key, site with no icon, still loading, real icon untinted)

## 6. Goldens, panel, perf and docs

- [x] 6.1 Run the golden tests before redrawing anything; if a v1 golden changes because of the shortcut fallback, stop and decide; redraw Carbon and Paper (page and dock) and confirm the other themes are unchanged
- [x] 6.2 Render Carbon and Paper with `urdeck-cli theme render` and compare weather and shortcut cards with the design board; adjust keys or the weather glyph scale where a glyph or its size is off
- [ ] 6.3 Measure weather before and after (CPU with a card on the panel, private memory with the poster cache gone) and a glow render for the Rigline fixture; write `docs/perf/weather.md` and `docs/perf/theme-look.md` notes; look at the panel with Carbon and Paper at 100, 125 and 150 %
- [x] 6.4 Update `README`/`docs` and the site pages (`site/src/content/docs/`) for icon sets, `theme:` glyphs, the removed `animated` key and the Lucide notice (README and the licence file; no on-screen credit); update `STATUS.md` (PR 10, the Carbon and Paper follow-up, Prism's open icon question)
- [x] 6.5 Run `dotnet build urdeck.slnx -c Release`, `dotnet test urdeck.slnx -c Release`, `dotnet format urdeck.slnx --severity warn` and `openspec validate --all --strict`; tick these tasks only when verified
