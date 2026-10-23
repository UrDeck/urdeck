## 1. Groundwork

- [x] 1.1 `preview-renderer`, `site-media-pipeline` and `site-gallery` are archived; `catalog.json` has widget `settings` and `findings`; on `docs/docs-rewrite` `npm run build` passes
- [x] 1.2 Read `docs/themes.md`, `docs/backgrounds.md`, every page in `site/src/content/docs/`, `content.config.ts`, `Docs.astro`, and the two schemas; list each section of the two docs files and where it goes (design, decision 1), in the pull request's description

## 2. Generated reference

- [x] 2.1 Descriptions for every typed node of `docs/schemas/v2/theme.json` and `v1/shader-manifest.json` that has none (design, decision 5); `urdeck-cli theme schema` prints the new file; the existing schema tests pass
- [x] 2.2 `SchemaTable`, `SettingsTable`, `FindingsTable` and `BuiltinList` components (decision 2), each failing the build on a missing description, and writing what they rendered for the check
- [x] 2.3 Completeness check in `check-output.mjs` (decision 4); a deliberate gap (a fixture catalog with an extra widget) fails it

## 3. Pages

- [x] 3.1 Getting started and pages/dock from the site's `index.md` and the README's configuration parts
- [x] 3.2 Widget pages: settings tables with descriptions in front matter, sizes from media slots, examples
- [x] 3.3 Themes: choosing (gallery), quick start ("a theme in five minutes", starting from a built-in), the reference by group, fonts and licences, icon sets, motion, checking a theme
- [x] 3.4 Backgrounds: choosing (all sixteen, pairing), speed and cost, colours and images, writing a background (manifest reference, the shader, the tool, the example)
- [x] 3.5 Troubleshooting: theme and background findings tables, common problems from both docs files
- [x] 3.6 Every section of the list in 1.2 ticked as moved, merged or left for contributors

## 4. Cookbook

- [x] 4.1 Eight recipe folders (decision 6) with `theme.json`, `recipe.json`, `README.md`, and the icon set for custom icons; each validates clean
- [x] 4.2 The cookbook content collection and page layout (summary, raw JSON, picture, explanation)
- [x] 4.3 The media generator's cookbook rows (`site-media` spec, "Cookbook Pictures"); generator tests for an added recipe and a changed one
- [x] 4.4 `urdeck-cli theme validate site/cookbook --fail-on-warnings` in `ci.yml`'s `test-linux`
- [x] 4.5 Generate the recipe pictures (`npm run media -- cookbook` in the Linux container) and commit them

## 5. Contributor docs and links

- [x] 5.1 `docs/themes.md` and `docs/backgrounds.md` reduced to contributor pages that link to the site (decision 1)
- [x] 5.2 Links updated in `README.md` (configuration, themes, backgrounds point to the site; the screenshot section points to the gallery), `CONTRIBUTING.md`, `AGENTS.md`, `docs/BACKLOG.md`, and the comment in `BuiltInShaderTests.cs`

## 6. Verification

- [x] 6.1 `npm run build` with its checks; `openspec validate --all --strict`; `dotnet test tests/UrDeck.Cli.Tests -c Release` (schema)
- [x] 6.2 Read every docs page on a phone width and a desktop width, with JavaScript off once; internal links checked by the build
- [ ] 6.3 Owner's read-through of the themes reference and two cookbook pages (stop and ask; do not tick without them)
- [ ] 6.4 `docs/design/look-and-feel/STATUS.md`: PR 17 row done; delete `docs/handoff/2026-10-22-site-media-automation.md` (all four changes done)
