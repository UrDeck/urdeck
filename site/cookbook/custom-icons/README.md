An icon set is a folder `icons/<set>/` next to `theme.json`, and `icons.set` names it. The folder needs a `manifest.json`, which
says which file shows which of the product's icon names, and the SVG files themselves. See [icon sets](/docs/themes/icons/) for
the full rules.

- This set draws only eight icons: sun, moon, partly cloudy, overcast, rain and snow, and two for shortcuts (`app/terminal` and
  `app/home`). Every other key is answered by the built-in `outline` set, so the weather card never shows a hole.
- Each file is a 24 by 24 outline made of a few `path`, `circle` and `line` elements. The deck strokes every shape with
  `icons.stroke` (0.075 of the icon's size) in `icons.tint`, so the colours and widths written in the files are ignored.
- `source` and `license` in the manifest say where the icons come from. These were drawn for this recipe and are given away under
  CC0, with the licence text in `LICENSE.txt` beside them, which keeps `theme validate` quiet about licences.
- A shortcut uses a glyph with `"icon": "theme:app/terminal"`.

Draw your own icons the same way, or take a set you like the licence of, name the keys in the manifest, and run
`urdeck-cli theme validate` to see which rule a file breaks.
