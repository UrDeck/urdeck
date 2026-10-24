---
title: A theme in five minutes
description: Make a theme folder, change a few values, see it on the deck, and check it.
section: Themes
order: 21
---

A theme is a folder with one settings file in it. You can start from nothing, because everything you leave out comes from
`default-dark`, or from a copy of a built-in theme.

## 1. Make the folder

In the data folder (**Open data folder** in the tray menu), make `themes/my-theme/theme.json`. The smallest useful theme is a few lines:

```json
{
  "colors": { "background": "#0f0f1a", "accent": "#4a9eff" },
  "card": { "radius": 0.08 },
  "typography": { "font": "MyFont.ttf" }
}
```

Put `MyFont.ttf` in the same folder, or leave `typography` out to keep the default font. A font that is only named by family
is used where it is installed; a file in the folder works everywhere (see [fonts and licences](/docs/themes/fonts/)).

If you have the [command line tool](/docs/command-line/), it makes the folder for you, with a file that already validates:

```
urdeck-cli theme init my-theme                  # a starter: the default dark colours and a meta group to fill in
urdeck-cli theme init my-theme --from carbon    # a self-contained copy of a built-in theme, fonts and licences included
```

A copy of a built-in theme is the quickest way to learn the format: change one value at a time and watch what moves.

## 2. Choose it

In `urdeck-config.json`:

```json
{ "theme": "my-theme" }
```

Save the file. UrDeck re-reads the theme folder whenever the configuration is reloaded, so after editing `theme.json`, save the
configuration again (or just touch it) to apply the change. A theme with the same name as a built-in one replaces it.

## 3. Change something

Colours are `#RRGGBB` or `#AARRGGBB` (the alpha comes first). Sizes are fractions of one grid cell, which is a quarter of the
screen's width, so a theme looks the same on any display. Angles are degrees, times are seconds.

A value that does not parse is replaced by the default's, and a warning goes to `urdeck.log` naming the theme, the key and the
value. A file that is not valid JSON makes the theme unavailable and the default is used.

Add the `$schema` line and an editor with JSON Schema support (Visual Studio Code does) completes every key and shows its
description while you type:

```json
{ "$schema": "https://urdeck.app/schemas/v1/theme.json" }
```

All the keys (chamfered cards, shadows, patterns, gauge styles, icons, a background of its own) are in the
[theme reference](/docs/themes/reference/), and the [cookbook](/docs/cookbook/) shows the common ones at work.

## 4. See it without a deck

```
urdeck-cli theme validate my-theme               # every finding, with how to fix it
urdeck-cli theme render my-theme -o preview.png  # the sample page drawn in the theme
```

`validate` reads the theme by the same code the deck does, so the deck's log and the tool name the same problems, and it adds
checks a person would make: text contrast on the cards, fonts and their licences, keys the deck does not read. See
[checking a theme](/docs/themes/checking/) and the [list of findings](/docs/troubleshooting/#theme-findings).

## 5. Share it

A theme is just the folder. Zip it with its font files and licence files and anyone can unzip it into their `themes/` folder.
`meta` (name, description, author, licence, mode, tags) lets an editor list it, and `meta.pairsWith.backgrounds` lists the
[backgrounds](/docs/backgrounds/) that suit it.
