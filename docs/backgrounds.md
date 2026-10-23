# Adding a built-in background

**The user documentation for backgrounds is on the website**: [urdeck.app/docs/backgrounds](https://urdeck.app/docs/backgrounds/)
(the setting, theme backgrounds and the built-in list), [speed, colours and images](https://urdeck.app/docs/backgrounds/speed-colours-images/),
[writing a background](https://urdeck.app/docs/backgrounds/writing/) (the manifest reference is made from
[`docs/schemas/v1/shader-manifest.json`](schemas/v1/shader-manifest.json), the shader language, an example) and
[the shader tool](https://urdeck.app/docs/backgrounds/tool/). Its source is `site/src/content/docs/backgrounds*`. This page is for
the people who add a background that ships with UrDeck.

## What a built-in background is

A folder `src/UrDeck.Engine/Backgrounds/Builtin/<name>/` of the same shape as a user's: `<name>.sksl`, `manifest.json` and
`sample.png`, embedded in the engine (`UrDeck.Backgrounds.<name>/<file>`). The name is in `ShaderStore.BuiltInNames`
(`BuiltInShaderTests` asserts the list and the embedded folders agree).

## Checklist

1. **The shader** draws with `iColor0` to `iColor3` so it follows the theme, and takes its motion from `iTime`. A new built-in is
   **periodic**: every period of its motion divides 14,400 seconds, the time wrap (`BuiltInShaderTests`, `PeriodicNames`). Keep
   it cheap: it is drawn at half the panel's size about 30 times a second, and `docs/perf/` records what the others cost.
2. **The manifest** has a `title`, a `description`, a `description` for every parameter that is not `hidden`, `min`, `max` and
   `step` where they help, `tags`, `pairsWith.themes` (the themes it suits, best first; the pairing must be mutual with the
   themes' `meta.pairsWith.backgrounds`) and a sample. The built-ins produce none of the lint findings, and a test requires it.
   The meaning of the four palette colours goes into `site/src/data/site-copy.json` (`backgrounds.<name>.palette`), the one thing
   the manifest has no key for.
3. **The sample picture** (`sample.png`, 270x480, `iTime` 10) is made by the tool, not by hand; see below.
4. **Validation.** `urdeck-cli shader validate --all src/UrDeck.Engine/Backgrounds/Builtin --fail-on-warnings` is clean; CI runs it
   on the built-ins, renders four of them, and does a `shader init` on a copy.
5. **The site.** Nothing to type: the list on the backgrounds page is made from the catalog, and the site's build fails when a
   built-in is missing from it. The poster and the loop come from the media refresh (`npm run media -- backgrounds`).

## Regenerating the built-in samples

The pictures in `Builtin/<name>/sample.png` are made by `urdeck-cli`: the first four with the default theme, the other twelve with
the palette of the theme their manifest records in the sample's `settings` (pass it with `--colors`, since those themes were not
built in yet). Run it after changing a built-in shader, look at the picture, and commit it and the manifest with the change
(`--update-manifest` keeps each sample's `settings` current; it records the default theme when `--colors` is given, so set `theme`
back by hand):

```powershell
$cli = "dotnet tools/UrDeck.Cli/bin/Release/net10.0/urdeck-cli.dll"
foreach ($name in "plasma", "aurora", "gradient", "julia") {
    $dir = "src/UrDeck.Engine/Backgrounds/Builtin/$name"
    Invoke-Expression "$cli shader render $dir -o $dir/sample.png --fail-on-warnings --update-manifest"
}
```

CI renders every built-in into a temporary folder on every run, to prove the tool works; it never compares a picture with the
committed one.

## Tests worth knowing

- `BuiltInShaderTests`: the names and the embedded folders agree, the pairing data is mutual, the time wrap.
- `BackgroundPackageTests`: the manifest reader, the lint, and that the schema and the reader's key tables agree.
- `BackgroundPlanTests`: the order of candidates (the configuration's choice, the theme's default, its still background, its
  colour).

## Where the rest went

| Was in this file | Now |
|---|---|
| The `background` setting, Theme backgrounds | The site's "Choosing a background" |
| Built-in shaders (the table) | The same page, made from the catalog |
| Speed, scale and cost, Colours, Images | The site's "Speed, colours and images" |
| Authoring, the manifest reference, the shader | The site's "Writing a background" |
| Lint (the table of codes) | The site's "Troubleshooting", made from the catalog |
| The command line tool | The site's "The shader tool" and "The command line tool" |
| When something is wrong | The site's "Troubleshooting" |
