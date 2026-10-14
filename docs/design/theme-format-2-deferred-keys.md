# Theme format 2: keys deferred from the first specs

Source: the look-and-feel handoff package (`urdeck-look-and-feel-handoff`, files `02-theme-schema.md` and `11-gap-audit.md`).
The package proposes about 160 new keys. Every key in a spec needs scenarios, a validator rule, a test and a docs entry,
so the first specs carry only the keys at least one of the eight draft themes uses (`11-gap-audit.md` section 5 lists
the rest). The keys below are **not dropped**: the definitions are kept here exactly as the package wrote them, so a
future change can restore one by adding it to the schema, the model, the validator and `docs/themes.md`.

Unknown keys are ignored at load (as in format 1), so a user theme can already contain a deferred key without error;
`theme validate` will warn that it has no effect yet.

## How to restore a key

1. Add a delta requirement (ADDED) to the `theme` capability naming the key, range, default and cross-field rules.
2. Add it to `ThemeDefinition` (merge, sanitize), the JSON Schema and the generated docs.
3. Add one test per behaviour (valid, out of range, wrong type) and one picture if it changes pixels.
4. Remove its row from this file in the same change.

## Deferred keys

| Key | Definition in the package | Why deferred | Restore when |
|---|---|---|---|
| `card.accentStrip.glow` | glow object on the lit strip | No draft theme glows a strip; `card.glow` and `icons.glow` cover glow | A theme wants a glowing scan line |
| `card.compact.padding` | number 0..1, padding override for compact cards | Decision D10 keeps compact chrome-only; no draft needs it | 1x1 tiles look cramped in a real theme |
| `card.compact.accentStrip.fade` | boolean, fade strip to transparent | Only Carbon fades, and Carbon's strip is on full cards | A compact tile wants a fading strip |
| `divider.inset` | number 0..0.5, indent from the section edges | All drafts run edge to edge | A theme wants an indented rule |
| `typography.fonts.body` | string, per-role font for body text | Only `value`, `label` and `unit` roles differ in the drafts; body inherits the default font | A theme wants a different body face |
| `typography.fonts.title` | string, per-role font for titles | As above | As above |
| `typography.case.unit` | `none\|upper\|lower`, default `none` | Units are never recased in the drafts | A theme wants uppercase units |
| `typography.tracking.body` | number -0.1..0.6, em | Only label and title tracking are used | A theme wants tracked body text |
| `gauge.sparkline.fillAlpha` | number 0..1, default 0, area fill under the line | Carbon's sparkline is a line only | A sparkline with a filled area |
| `background.default.parameters` | object, shader parameter overrides for the theme's default background | A default names a shader and the shader's own defaults apply; themes steer colour through `backgroundPalette` | A theme needs a tuned variant of a shader |
| `background.default.colors` | array, palette override for the default background | Same: `colors.backgroundPalette` already does this | As above |
| `background.static.fit` | `cover\|tile\|center`, default `cover` | `static.image` is kept and always covers, reusing `BackgroundImage.Prepare`; `tile` and `center` need new drawing code and no draft uses them | A theme picture that must tile or sit centred |
| `background.static.tileSize` | number 0.1..20, cells, when `fit` is `tile` | Only meaningful with `fit: tile` | With `fit: tile` |

`background.static.image` is **kept** (a theme can ship its own default picture, a file inside the theme folder). It
covers the panel like the `image` background type, and a user's `background` setting (shader, image or `none`) replaces
it like any other theme default. Its path must stay inside the theme folder (path-escape rejected, as for fonts).
| `card.blur` | reserved (decision D1): backdrop blur | Not implementable on the current surfaces; Prism ships frostless and its draft theme drops the key | A compositor backdrop brush exists |

### Deferred by the spikes (2026-10-13, `docs/perf/theme-look.md` on the spike branches)

| Feature | Definition in the package | Why deferred | Restore when |
|---|---|---|---|
| `typography.valueFill.animate` (`{mode, period}`) | The gradient fill of the value digits slides (Rigline). The gradient `valueFill{colors, angle}` itself is kept, static. | S2 did not test it. The digits are drawn inside the card's own surface, so the compositor-sprite trick that makes the animated border cheap does not apply; repainting every card each frame cost 11% of a core and +26 MB for 8 cards. | A spike finds a cheap way (for example a masked sprite over the value, or a low step rate) |
| `pattern.scroll` (`{dx, dy}`) | Pattern drifts (Rigline scanlines). | Needs the pattern surface to repaint or scroll; the pattern surface is full-size and static in the first chunk. | After motion lands and the pattern surface is measured |

### Decided by the spikes (so the requirement is not lost)

- Pop takes its numerals from Archivo at weight 900; the Archivo Black file is not bundled (S3: same advance, ink within 1%).
- `pattern` draws on a page-sized surface and accepts the memory cost (about +33 MB, S1) for the three themes that use it
  (Carbon, Blueprint, Ember); no tiling or sprite-line work for now.

### Enum values deferred

The key stays; the listed value is not accepted yet (an unknown value is a warning and falls back to the default).

| Key | Values deferred | Why |
|---|---|---|
| `card.borderStyle` | `dotted` (round-capped zero-length dashes spaced `borderDash.off + borderWidth`) | `dashed` covers Blueprint; dotted is a one-line addition on the dash path |
| `pattern.kind` | `lines` (parallel lines at `angle`; the same routine as `hatch` with a thicker default) | `hatch` with a larger `thickness` looks the same |
| `card.cornerMarks.style` | `cross` (small plus) | `bracket` and `tick` cover Carbon and Blueprint |
| `gauge.marker.shape` | `dot`, `line` | `diamond` and `needle` cover the drafts |
| `chip.shape` | `rect`, `bracket` | `pill` and plain text cover the drafts |

### Knobs kept although only one theme uses them

These are the signature features of a theme (accent strip, outside marks, redline, crosshair, conic border and so on)
and stay in. They are listed in `11-gap-audit.md` section 5 and are not deferred.

## Not knobs: things the package also set aside

From `11-gap-audit.md` section 6: `typography.numerals`, per-widget theme overrides, arbitrary SkSL on cards, per-card
animated backgrounds, audio-reactive anything, CSS-like effects, theme inheritance (`extends`), per-theme sounds, a
`dock.*` group, localised weather words. Reasons are in that file.
