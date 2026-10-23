---
title: The shader tool
description: "shader init, validate and render: make, check and draw a background folder without a deck."
section: Backgrounds
order: 33
---

`urdeck-cli` checks a background folder and renders it to a picture without a deck: no window, no GPU, no network, on Windows and
on Linux. It calls the same engine code the deck does (the lint that logs findings when a background loads, the renderer that
makes the samples), so it cannot disagree with the deck. See [the command line tool](/docs/command-line/) for how to get it.

To see a background behind a page of cards, as the deck shows it, use `preview render` (described with
[checking a theme](/docs/themes/checking/#seeing-a-theme-at-work)): `urdeck-cli preview render --theme ember --background embers
-o page.png` draws the sample page in Ember over the background, and `--frames 90` writes three seconds of frames.

## `shader init`

```
urdeck-cli shader init <shader.sksl | folder> [--output <directory>]
```

Makes a background folder with a manifest from a shader, so the first thing you see is a folder that loads and a list of what is
left to write. It reads the shader's declarations and does not compile it: a shader with an error still gets its manifest, and
`validate` reports the error next.

| Argument | Result |
|---|---|
| `x.sksl`, no folder `x` | Makes `x/`, **moves** the file in, writes the manifest. |
| `x.sksl`, folder `x` exists with neither `x.sksl` nor a manifest | Moves the file in, writes the manifest. |
| `x.sksl`, folder `x` already holds `x.sksl` or a manifest | Refuses; nothing is moved. |
| folder `x` holding `x.sksl` and no manifest | Writes the manifest in place. |
| folder `x` with a manifest, or without `x.sksl` | Refuses. |
| a name that is not a valid background name | Refuses; nothing is created. |

`--output <directory>` (an existing directory) makes the folder there instead of next to the file. The file is moved, not copied,
because a loose `.sksl` next to its folder is what the deck no longer loads. A refusal is exit code 2 and happens before
anything is created; `init` never overwrites a manifest.

The stub holds the `$schema` line, `"manifest": 1`, a `title` made from the name (`my-waves` becomes `My waves`), an empty
`description`, and for each parameter of the shader, in its order, a `label` made the same way (`bandWidth` becomes `Band width`)
and an empty `description`. The host's inputs, the setting's own keys and a uniform of a type that cannot be set get no entry
(the last are named in the output). `"kind": "color"` is written for a three- or four-component parameter when the shader
suggests a colour: a `layout(color)` declaration, a default written as `#rrggbb`, or a name that contains `color`, `colour` or
`tint`. **That is a guess**; delete the line if it is wrong. Nothing else is guessed.

`shader validate` right after `init` has no errors, and its warnings are your to-do list: the descriptions and the missing samples.

## `shader validate`

```
urdeck-cli shader validate <folder> [--format text|markdown|json|sarif] [--fail-on-warnings]
urdeck-cli shader validate --all <directory> [--format ...] [--fail-on-warnings]
```

Checks the folder and prints every finding; it never renders. The folder's name is the background's name (`.` is resolved first).
`--all` checks every folder directly inside the directory, in the order of their names; files and hidden folders are skipped, and
a folder without a `manifest.json` is a finding (`SHADER001`), not skipped. A directory with no folders is a failure of the tool,
not a clean run.

| Option | Meaning |
|---|---|
| `<folder>` | The background folder; not together with `--all`. |
| `--all <directory>` | Check every folder in the directory. |
| `-f`, `--format` | `text` (default), `markdown`, `json` or `sarif`. |
| `--fail-on-warnings` | A warning fails the run as an error does. |

A file in a finding is written relative to the working directory with `/` on every platform. A finding names a file, never a line.
Every code is in the [list of background findings](/docs/troubleshooting/#background-findings).

`text` has one line for each finding and a summary line (a clean run prints the summary only):

```
waves/manifest.json: warning SHADER031: the shader declares 'density' and the manifest does not annotate it.
waves/manifest.json: warning SHADER041: the manifest has no description.
waves/manifest.json: warning SHADER050: the manifest lists no samples.
1 background checked: 0 errors, 3 warnings, 0 remarks.
```

`markdown` prints a heading and a table for each background (or `No findings.`), then the summary line.

`json` prints one object and nothing else on the output stream, so it can be piped. `version` is 1; a later version adds keys and
never renames or removes these. `severity` is `error`, `warning` or `info`:

```json
{
  "version": 1,
  "tool": { "name": "urdeck-cli", "version": "0.1.0" },
  "backgrounds": [
    {
      "name": "waves",
      "folder": "waves",
      "diagnostics": [
        { "code": "SHADER041", "severity": "warning", "message": "the manifest has no description.", "file": "waves/manifest.json" }
      ]
    }
  ],
  "summary": { "errors": 0, "warnings": 1, "infos": 0 }
}
```

`sarif` is a SARIF 2.1.0 log with one run, the tool as the driver, one rule for each code that occurs and one result for each
finding (level `error`, `warning`, or `note` for `info`; the file as the location, with no region).

## `shader render`

```
urdeck-cli shader render <folder> -o <file.png> [--size WxH] [--time seconds] [--theme name]
                         [--colors #rrggbb,...] [--param name=value]... [--fail-on-warnings] [--update-manifest]
```

Draws one frame on the CPU and writes it as a PNG; an existing file is replaced. It renders the folder it is given, never a
built-in of the same name.

| Option | Meaning |
|---|---|
| `<folder>` | The background folder. |
| `-o`, `--output` | The PNG file to write (its folder must exist). |
| `--size` | The picture's size in pixels, `WIDTHxHEIGHT`, each 1 to 8192; default `270x480`. |
| `--time` | The shader's `iTime` in seconds; the default is a fixed time (10), so the same input gives the same picture on one machine. |
| `--theme` | A built-in theme whose colours the background uses; default `default-dark`. User themes are not read. |
| `--colors` | One to four colours as `#RRGGBB`, separated by commas, as the configuration's `colors`. |
| `--param name=value` | A parameter, as the configuration sets it; repeatable. A number or an array (`1.5`, `[0.5,0.2]`) is read as JSON, anything else as text (`#ff8800`). A value the configuration would refuse is refused the same way. |
| `--fail-on-warnings` | A blank picture fails the run. |
| `--update-manifest` | Record the picture in the folder's `manifest.json` (below). |

There is no `--scale`: it only sizes the layer in the deck, and the size given is the size rendered. Names the product owns
(`speed`, `scale`, `colors`, `iTime`, ...) cannot be set with `--param`.

When the background cannot be built (no manifest, a shader that does not compile) no file is written, the reason is printed on
the error stream and the exit code is 1. When the shader drew nothing, the picture is written and the output says `SHADER070` as a
warning: the exit code is 1 only with `--fail-on-warnings`. `render` reports only findings about the picture; the folder's other
warnings are `validate`'s. Pictures made on different machines are not guaranteed to be identical.

With `--update-manifest` the picture is also recorded in `samples`. The output must be inside the folder, and the entry's `file`
is its path relative to the folder with `/` separators. An entry that already names the file is updated in place and keeps its
`caption`, `order` and any other key; otherwise one with `"type": "image"` is added at the end. Its `settings` is replaced by how
this picture was made: `size`, `time` and `theme` always, and `colors` and `parameters` when you gave them.

The manifest is rewritten as indented JSON (two spaces, LF) with every key, value and their order kept; the only thing lost is
your own layout. When a rewrite would lose more, because the manifest has a comment, a trailing comma or the same key twice in one
object (or its `samples` is not an array), the update is **refused before anything is rendered or written**: exit code 2, the
reason on the error stream, and the entry to paste by hand.

## Using it in CI

```yaml
- run: urdeck-cli shader validate --all backgrounds --fail-on-warnings
```

For code scanning upload the `sarif` format:

```yaml
permissions:
  contents: read
  security-events: write
steps:
  - run: urdeck-cli shader validate --all backgrounds --format sarif > urdeck.sarif || true
  - uses: github/codeql-action/upload-sarif@v3
    with: { sarif_file: urdeck.sarif }
```

(The `|| true` keeps the upload running when there are findings; run the plain command above as the step that fails the build.)
Two caveats. A pull request from a fork gets a read-only token, so the upload is refused there; use `json` and `jq` to write
annotations or a summary instead, for example
`urdeck-cli shader validate --all backgrounds --format json | jq -r '.backgrounds[].diagnostics[] | "::warning file=\(.file)::\(.code) \(.message)"'`.
And a finding names a file, not a line, so a code scanning annotation lands on the file as a whole.
