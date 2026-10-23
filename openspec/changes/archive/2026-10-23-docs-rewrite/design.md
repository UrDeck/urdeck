## Context

See proposal.md for why. Last of the four changes from the 2026-10-22 explore; it depends on `preview-renderer` (the
catalog, including widget settings and finding codes), `site-media-pipeline` (media manifest, generated `catalog.json`)
and `site-gallery` (product media slots that follow the picker).

Today: `docs/themes.md` (Settings file, Background palette, Format 2 with a section per group, Icon sets, Checking a theme
with Findings, Bundled fonts) and `docs/backgrounds.md` (the `background` setting, theme backgrounds, built-in shaders,
speed and cost, colours, images, authoring, the manifest reference, lint, the shader language, the command line tool,
a worked example, regenerating built-in samples, troubleshooting). The site has `index.md`, `themes.md`, `backgrounds.md`
and four widget pages, as Astro content with `section` and `order` front matter. The theme schema (`docs/schemas/v2/theme.json`)
has 215 typed nodes, 164 with a description. Links to the two docs files: `README.md`, `CONTRIBUTING.md`, `AGENTS.md`,
`docs/BACKLOG.md`, the look-and-feel design notes, and a test comment (`BuiltInShaderTests.cs`).

## Goals / Non-Goals

**Goals:**
- One place a user reads; contributors keep their notes in `docs/`.
- Any fact the product defines is shown from the product, and the build notices a gap.
- The cookbook's JSON and pictures cannot disagree.

**Non-Goals:**
- Rewriting the design notes in `docs/design/` (snapshots, left as they are).
- SDK API reference, search, translations.

## Decisions

### 1. Page layout of the docs section

```
Getting started        index (config file, where it is), pages and the dock
Widgets                clock, stats, weather, shortcut (each: what it shows, settings table, sizes in the chosen theme, example)
Themes                 choosing a theme (gallery from the catalog), writing a theme (quick start), theme reference
                       (generated, one section per group), fonts and licences, icon sets, motion, checking a theme
Backgrounds            choosing a background (all sixteen, pairing), speed and cost, colours and images,
                       writing a background (manifest reference generated, the shader, the command line tool, example)
Cookbook               one page per recipe
Troubleshooting        theme findings, background findings (generated), common problems
```

Content moves from `docs/*.md` into these pages and is edited for a reader who has not read the source (the site's voice:
short, concrete, runnable). Contributor-only parts stay: `docs/themes.md` keeps "adding a built-in theme" (fonts and
licences in the repository, goldens, the contrast rule, validation) and `docs/backgrounds.md` keeps "adding a built-in
background" (sample pictures, the time wrap, the lint, tests); each starts with a link to the site's reference.

### 2. Generated tables are Astro components over JSON

- `SchemaTable` reads a schema file at build time (`docs/schemas/**`, already copied by the build) and renders a group's
  keys as rows: dotted path, type, enum or range, default, description. Nested objects become sub-rows; `$ref`s are
  resolved. A page places one per group: `<SchemaTable schema="v2/theme.json" path="card" />`.
- `SettingsTable` reads a widget's settings from `catalog.json` (name, JSON type, default) and the descriptions from the
  widget page's front matter (`settings: { format: "…" }`), so prose stays with the page while names and defaults come
  from the product.
- `FindingsTable` reads the finding codes from `catalog.json`, with a `prefix` (`THEME` or `SHADER`).
- `BuiltinList` lists the catalog's themes, backgrounds or widgets with their product media slots.

Each component throws during the build on a missing description, which fails `astro build` with the key's name.

### 3. The catalog grows two lists (amends `preview-renderer`)

`catalog export` adds, per widget, `settings`: every public settable property of its configuration type, with its JSON
name, a JSON type (`string`, `number`, `integer`, `boolean`, `array`, `object`, or an enum's values) and its default from
`new TConfig()`; and a top-level `findings`: every theme and background finding code with its severity and the summary
the product's code table holds. These are recorded in `preview-renderer` (not built yet), so this change only consumes
them.

### 4. Completeness check in `check-output.mjs`

After the build: every theme, background and widget in `catalog.json` is linked from or named on a docs page, every
finding code appears on the troubleshooting page, and every schema leaf appears in a reference table (the components
write a list of what they rendered to a build file the check reads). A failure lists what is missing.

### 5. Schema descriptions

The 51 typed nodes without a description get one, written from `docs/themes.md` and the engine's comments; the manifest
schema likewise. `urdeck-cli theme schema` prints the same file, so the tool gains them too.

### 6. Cookbook recipes are theme folders the pipeline renders

`site/cookbook/<recipe>/theme.json` (a user theme: format 2, only the keys the recipe teaches, on top of the default
theme the loader starts from), any assets it needs (the custom icons recipe holds a small icon set folder), and
`recipe.json`: `{ "title", "summary", "order", "show": "page" | "dock" | { "widget": "<typeId>", "size": "WxH" },
"background": "<name>" | null }`. The cookbook pages are a content collection over these folders: the page shows
`summary`, the `theme.json` as a code block imported raw from the folder (`?raw`), the picture from the media manifest and
a short explanation in the folder's `README.md`. The media generator adds a matrix row per folder (`preview render
--theme-dir`, plus `--widget` when asked). CI runs `urdeck-cli theme validate site/cookbook --fail-on-warnings`
(the directory form validates every folder) in `test-linux`.

*Alternative:* recipes as JSON snippets in Markdown. Rejected: a snippet is never validated and its picture would be made by
hand.

### 7. Docs pictures follow the picker

Docs pages use `site-gallery`'s `ProductMedia` slots, so the picker (present on every page) changes them. The widget pages
show every previewed size; the themes page shows the gallery; the backgrounds page shows all sixteen with pairing.

## Risks / Trade-offs

- [Moving 1300 lines of prose loses a detail] → a task compares section by section; each moved section is ticked in the pull
  request's description.
- [Generated tables read worse than hand-made ones] → descriptions are written for the table (one sentence); a group's page
  keeps an introduction above its table.
- [The completeness check blocks a theme change that forgets docs] → intended; the failure names what to add.

## Migration Plan

Docs only, after the three earlier changes. External links to `docs/themes.md` anchors break; the contributor pages keep
their file names and link to the site, so a reader lands one click away.
