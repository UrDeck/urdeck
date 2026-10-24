# Adding a built-in theme

**The user documentation for themes is on the website**, built from the product: [urdeck.app/docs/themes](https://urdeck.app/docs/themes/)
(choosing and writing a theme), the [theme reference](https://urdeck.app/docs/themes/reference/) (every key, made from
[`docs/schemas/v1/theme.json`](schemas/v1/theme.json)), [fonts and licences](https://urdeck.app/docs/themes/fonts/),
[icon sets](https://urdeck.app/docs/themes/icons/), [motion](https://urdeck.app/docs/themes/motion/),
[checking a theme](https://urdeck.app/docs/themes/checking/) and the [cookbook](https://urdeck.app/docs/cookbook/). Its source is
`site/src/content/docs/themes/`. This page is for the people who add a theme that ships with UrDeck.

## What a built-in theme is

A folder-less theme: `src/UrDeck.Engine/Themes/Builtin/<name>.json`, plus the font files it names and their licence texts, all
embedded in the engine (`UrDeck.Themes.<file>`). It is listed in `ThemeStore.BuiltInNames`. A user theme folder with the same
name replaces it.

## Checklist

1. **The file.** With `meta` (`name`, `description`, `mode`, `tags`, `pairsWith.backgrounds`, `license`). Leave out what
   you do not need: a value you omit comes from `default-dark` (the groups that are never inherited are listed in the theme
   reference). Describe the theme in a sentence or two; `meta.description` is what the site's gallery shows.
2. **Fonts.** Put the `.ttf` next to the JSON and the licence beside it (`<Font>-OFL.txt`), name the file in `typography`, and add
   both to the fonts table in [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md). Subset a large font (the limit is 3 MB, `THEME021`).
   A built-in theme never names a font by family: it must look the same on every machine.
3. **Contrast.** The text colour on the card fill (and on every gradient stop and tint) must be at least 4.5:1, the muted text colour
   at least 3:1 (`THEME060` to `THEME062`). A translucent fill is judged over `colors.background`, which is not what a moving
   background gives, so give a busy background a `background.scrim` or make the fill opaque enough.
4. **The background.** Name the default shader in `background.default`, a still `background.static` for where shaders are
   unavailable, and list the backgrounds that suit the theme in `meta.pairsWith.backgrounds` (the default first). Pairing is
   checked from both sides: a background's manifest names themes too (`BuiltInShaderTests` keeps them mutual).
5. **Validation.** `urdeck-cli theme validate --builtin --fail-on-warnings` must be clean; CI runs it. So does a copy made with
   `urdeck-cli theme init x --from <name>`.
6. **Tests.** Add the name to the theories of `GoldenTests` and `ThemeDraftTests` (which holds each built-in to the
   published schema and to a load without a warning), render the goldens on Windows and look at every picture before committing
   (`CONTRIBUTING.md`, "Golden images"). A theme with a feature of its own can have a test of it, as `BuiltInRiglineTests` has.
7. **The site.** Nothing to type: the theme list, the gallery, the picker and the accent rule come from the catalog. The theme
   appears after the media are refreshed (`npm run media -- decks`, widgets and backgrounds follow; see `site/README.md`,
   "Media"). A theme without media is named in the site's build log and left out of the picker, not failed.

## Changing a key of the format

A new key goes into the engine (the definition class and its sanitising; the lint's list of keys the deck reads is that model)
and into the schema ([`docs/schemas/v1/theme.json`](schemas/v1/theme.json)) **with a description**. The site's theme reference lists it after the next build with no page edited, and its build fails when the key has no
description. Add a sentence to the group's prose in `site/src/content/docs/themes/reference.mdx` if the key needs more than its
description says. A new finding code goes into `ThemeCodes`, `FindingCatalog` (code, severity and summary: the troubleshooting page
is made from it) and a test; a code never changes meaning and is never reused.

## Where the rest went

| Was in this file | Now |
|---|---|
| Settings file, the group tables | The site's theme reference, from the schema |
| Background palette | The theme reference (`colors`) and the backgrounds page "Speed, colours and images" |
| Icon sets | The site's "Icon sets" |
| Checking a theme, Findings | The site's "Checking a theme" and "Troubleshooting" |
| Bundled font | The site's "Fonts and licences"; the licence texts stay in `src/UrDeck.Engine/Themes/Builtin/` |
