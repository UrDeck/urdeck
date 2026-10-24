---
title: Checking a theme
description: Validate a theme folder, draw it without a deck, and read a finding.
section: Themes
order: 26
---

`urdeck-cli` ([the command line tool](/docs/command-line/)) checks a theme folder, draws it and starts one, without a deck: no
window, no GPU, no network, on Windows and on Linux. It reads a theme by the same code the deck does, so the deck's log and the
tool's output name the same findings in the same words. The tool adds the checks that need a judgement: keys the deck does not
read (with a suggestion), font files and their licences, backgrounds that do not exist, text contrast on the card fill and on
every tint, and the metadata. It never reads or writes the deck's configuration or log.

```
urdeck-cli theme validate <folder> [--background-dir <directory>]... [--format text|markdown|json|sarif] [--fail-on-warnings]
urdeck-cli theme validate --all <directory> [...]
urdeck-cli theme validate --builtin [...]
urdeck-cli theme render <folder> | --builtin <name> -o <file.png> [--page page|dock] [--size WIDTHxHEIGHT] [--fail-on-warnings]
urdeck-cli theme init <name> [--output <directory>] [--from <built-in theme>]
urdeck-cli theme schema [--output <file>]
```

- **`validate`** prints every finding of a folder, of every folder directly inside a directory (`--all`, in the order of their
  names; not recursive; no folders at all is a failure of the tool), or of the built-in themes as the product ships them
  (`--builtin`). The folder's name is the theme's name. `--background-dir` adds a directory of background folders that
  `background.default.name` and `meta.pairsWith` may name; the built-in backgrounds are always known. The formats are those of
  [`shader validate`](/docs/backgrounds/tool/#shader-validate); a JSON finding of a value also has `key`.
- **`render`** draws the sample page (a clock, two stats cards, a weather card and four shortcuts, with the data providers'
  sample readings and a fixed time) in the theme without a background, at 1100x1920 unless `--size` says otherwise. `--page dock`
  adds the dock and the page indicator. The same input gives the same picture on one machine; pictures from different machines can
  differ in the edge pixels of text. The findings go to the error stream and do not stop the picture; a theme that cannot be read
  at all gets no picture.
- **`init`** makes `<name>` (letters, digits, `-` and `_`) with a `theme.json` holding the `$schema` line, a `meta`
  for you to fill in and the default dark theme's colours; with `--from` it makes a self-contained copy of a built-in theme with
  the fonts and licence files it names. It never overwrites a folder.
- **`schema`** prints the JSON Schema of the format, the file published at `https://urdeck.app/schemas/v1/theme.json`.

## Seeing a theme at work

`preview render` draws a configuration's page or one widget, in a theme, over a background:

```
urdeck-cli preview render --theme-dir my-theme --background theme -o page.png
urdeck-cli preview render --theme ember --background embers -o page.png
urdeck-cli preview render --widget urdeck.widgets.stats --widget-size 4x2 --theme-dir my-theme -o stats.png
urdeck-cli preview render --theme-dir my-theme --background theme --frames 90 -o frames
```

`--background` is a name, `none`, or `theme` for the theme's own choice. Readings come from the data providers' sample files and
the moment is fixed (`--time` changes it), so the picture is the same every time and the command makes no network request. With
`--frames N` the output is a folder of `frame-0000.png` onwards, `--fps` apart (default 30): the widgets, the readings, a moving
background and an animated border move as on the panel, and frame 0 is the still picture. An unknown widget, a size the widget
does not declare or a background that does not exist is exit code `1` with the reason, and nothing is written.
`urdeck-cli --help` and `urdeck-cli preview render --help` list every option.

## Findings

Each finding says what is wrong and how to fix it, names the key by its path (`card.shadow.blur`) and the file. The codes never
change meaning, and a retired code is not reused.

- **error** means the deck cannot use the theme or the file.
- **warning** means it works and could be better.
- **info** is a remark.

The deck logs the findings it can act on when it loads a theme (the codes marked "also logged"); the rest only the tool reports.
Every code, with what it means and how to fix it, is in the [list of theme findings](/docs/troubleshooting/#theme-findings).

Exit codes: `0` nothing that fails the run, `1` an error (or a warning with `--fail-on-warnings`; an information finding never
fails), `2` the tool could not do its job (bad arguments, a folder that does not exist). The built-in themes check clean.

## In CI

To keep a repository of themes valid, run the check as a step that fails the build:

```yaml
- run: urdeck-cli theme validate --all themes --fail-on-warnings
```

and add `--format sarif` or `--format json` to feed code scanning or annotations, exactly as for
[backgrounds](/docs/backgrounds/tool/#using-it-in-ci).
