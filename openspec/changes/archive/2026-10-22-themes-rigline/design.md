## Context

The draft is `tests/UrDeck.Engine.Tests/Fixtures/themes/rigline/theme.json`; it drives `GoldenTests` (`rigline-page.png`,
`rigline-dock.png`) and several chrome tests. The folder has no font file, so its renders used the fallback face and
`theme validate` on it reports `THEME020` (the font is not in the folder). Built-ins are embedded resources
`UrDeck.Themes.<file>`; `ThemeStore.LoadBuiltIn` merges the file over `default-dark` and opens fonts by name from the same
resources (the pattern of `themes-carbon-paper`).

Rigline uses everything built since: a conic `borderGradient` with `borderAnimate` (`spin`, 7 s), `card.glow`, a `compact`
accent strip with glow, `pattern` scanlines, `typography.slant`, `valueFill`, `valueGlow`, a ramp-coloured ring gauge with
glow, outline icons with a ramp tint, and a `motion.level` of `full`. The `motion` change measured the border alone: a
compositor sprite stepped at 15 steps a second, +3.2 to +3.6 % of a core and +3.4 to +5.3 MB private for eight cards
(`docs/perf/motion.md`). Backgrounds `horizon`, `spectrum`, `equalizer`, `streams`, `warpgate`, `vortex` and `embers` all
exist in `src/UrDeck.Engine/Backgrounds/Builtin` and each lists `rigline` in its own `pairsWith`.

## Goals / Non-Goals

**Goals:**
- Rigline loads from the engine alone, in its own font, with no warning, and `urdeck-cli theme validate` is clean.
- The whole theme (not only its border) is measured on the panel and accepted before it ships.
- The engine and host are not changed; anything only they can fix is recorded as a follow-up. One exception, found on
  the panel: the Clock's seconds moved the time (decision 12), which only the SDK's `Readout` can fix.

**Non-Goals:**
- `card.compact.borderAnimate`, `valueFill.animate`, `pattern.scroll`, widget-level motion; a theme picker.

## Decisions

### 1. Chakra Petch Bold, static, with `slant: 10`

`ChakraPetch-Bold.ttf` and `ChakraPetch-OFL.txt` come from `spikes/theme-look-s3/fonts/` (untracked). Compare with the
upstream release (Cadson Demak, Google Fonts), confirm OFL 1.1, and record version and copyright in
`THIRD-PARTY-NOTICES.md`. The slant is the theme's `slant: 10` (a skew in the painter), not the italic file: one font file
serves value, unit, label, title and body. Flat layout, beside the other built-in fonts (as `themes-carbon-paper`, decision 1).
Not subset.

### 2. Weights: set the weight the file has

The draft's body weight is 500 on a Bold-only file, which logs one weight warning on load. All five roles in the built-in
are 700. A real 500 file (Chakra Petch Medium) was considered and rejected: a second file for a role that draws little
text in this theme. If the panel shows body text too heavy, Medium is the follow-up. The built-in must log nothing about
fonts or weights.

### 3. Drop `valueScale` above 1; deferred keys stay out

The draft's `valueScale` is 1, so there is nothing to remove (verify by search: a value is limited to its fitted size,
`STATUS.md`). `valueFill.animate` and `pattern.scroll` are already removed from the draft and stay deferred
(`docs/design/theme-format-2-deferred-keys.md`); the built-in must not carry them.

### 4. The animated border and the whole-theme cost

The border spin is the `motion` change's compositor sprite per card at 15 steps a second (accepted by the owner at 7 s a
turn). What was not measured is the sum: border, plus `card.glow`, `valueGlow` and `valueFill` (cost 2 to 3.7 times a
plain readout, cached), ramp gauge arcs with glow, scanlines (`PatternLayer`, +2 to +6 MB), and a moving `horizon`
shader. A measure-and-accept task covers Rigline at motion `full`, `reduced` and `off` on the panel (CPU, GPU, private
memory, as `docs/perf/motion.md` does), with the display off and the deck hidden to confirm the pause. `reduced` and
`off` must come to the cost of the still theme. If `full` is not accepted, the levers in order: the spin period, the glow
alpha and blur, the scanline spacing; never the 15 steps (decided on the panel).

### 5. Compact cards take the page card's border animation

The theme's `compact` group sets only an accent strip (four-colour, glowing, bottom). Tiles (1x1 and dock slots) therefore
inherit the page card's conic spin. Check on the panel that the strip and the turning border read together; record whether
`card.compact.borderAnimate` is needed (a follow-up, not built here). Eight spinning sprites is the measured basis.

### 6. Slant overhang and colour findings

A slanted value leans past its rectangle (about 50 px on a 4x2 clock, `STATUS.md`). Check the Clock 4x2 and a Stats
value against the card edge at both scalings; if it clips, shorten the lean (`slant`) before touching the engine, and
record whether the fit should count the lean as a follow-up. Every colour in the draft is checked for the hex-colour
finding: `#RRGGBB` and `#AARRGGBB` (alpha first), so `accentDim` `#2519e6ff`, `pattern.color` `#07ffffff`, `track.color`
`#12ffffff`, `well.fill` `#0affffff`, `indicator.inactive` `#40ffffff` and `indicator.backdrop` `#cc07060d` are alpha-first,
and `card.glow.color` `#aa7a5c` is a plain RGB (a purple-brown), not an alpha: confirm that is the colour intended (it is
unusual next to the spectrum; the panel decides).

### 7. `theme validate` clean, contrast by the validator

Run `urdeck-cli theme validate` (`docs/themes.md`, "Checking a theme") on the built-in; it must report no error and no
warning. `THEME020` on the fixture only reflects the missing font and goes away with the built-in. Expected fixes, to
confirm by running it: `THEME060` (text on the card fill, expected to pass), `THEME061` (muted `#9d96c9` on
`#141026`/`#0b0916`, expected above 4.5:1), `THEME062` (status colours `good`, `warning`, `critical` on the fill). The
conic border colours and the `ramp` colours, which carry meaning in the gauge, are checked by hand against the card fill,
since the validator checks text roles. Where a ramp end such as `#7a5cff` is under 3:1 on the fill (graphics need 3:1),
lighten it and record the new value. The table of final values goes in the pull request.

### 8. Pairings, background and icons

`meta.pairsWith.backgrounds` and the default `horizon` exist (checked against `Backgrounds/Builtin`). The theme's default
background is first in its own `pairsWith` list (the validator's rule). `ThemeStore.BuiltInNames` gains `rigline`;
`ThemeCatalog` and `PairingIndex` tests are extended; `icons.set` is `outline`, which ships.

### 9. Goldens and fixtures

The fixture folder is deleted and tests load the built-in (as carbon-paper, decision 2). `rigline-page.png` and
`rigline-dock.png` are redrawn at 1100x1920; they show the border at its start angle (a snapshot is a still, `motion`
changes no golden). The 3840 picture is the site deck. v1 and the other themes' goldens must not change.

### 10. Panel tuning, then the site

The owner looks at the theme on the 1100x3840 panel at 100 % and 150 %, over `horizon` and over the static gradient, with
the site deck. Each changed value is recorded (key, draft, final, reason). The site deck pictures are stills rendered by
`Render-SiteDecks.ps1`; loops through `npm run media` are optional (`render --frames` supports a spinning border).

### 11. Never-verified: a shader past the 4-hour wrap

`STATUS.md` lists a moving shader on the GPU past the 4-hour time wrap as never verified. Rigline pairs with moving
shaders, so an optional task runs `horizon` for more than four hours and checks for a jump or a stall.

### 12. The Clock's seconds moved the time

With `showSeconds` the value is fitted to the width that is left after the unit, and the unit's width was measured with
the font's own digit widths. In Chakra Petch the 0 is wider than the 8, so the fit (and so the size of the time) changed
from second to second (a height of 218 at `:41`, 213 at `:44`, 209 at `:00`). The Clock's `KeepSize` assumed `88` is the
widest and was not enough. `Readout` now measures and draws a unit that contains digits with the widest digit advance for
every digit (as it always did for the value) and has `ReadoutOptions.WidestUnit` for units that differ in more than
digits (`AM` and `PM`). The Clock passes its widest suffix and `KeepSize` is removed. Chosen over a Rigline-only
workaround (`showSeconds: false`) because any proportional font shows it. The change is to the MIT SDK; no theme golden
other than Rigline's moves.

### 13. Tuning record (owner, on the 1100x3840 panel)

| Key | Draft | Final | Reason |
|---|---|---|---|
| `typography.weights.body` | 500 | 700 | the file has only Bold; no weight warning |
| `card.fillGradient.colors` | `#141026`, `#0b0916` | `#73141026`, `#730b0916` (about 45 % opaque) | the animated background is mostly covered by cards; the owner asked for much more translucency |
| `colors.backgroundPalette[1]` | `#2a1a5e` | `#5a1a5e` | a warmer horizon glow under the sun |
| `colors.backgroundPalette[2]` | `#7a5cff` | `#ff6a3d` | the sun is this slot; violet read as a purple sun, the owner wanted an orange-red glow |
| `card.glow.blur` | 0.24 | 0.06 | translucent cards limit the glow to the gap (`THEME054`: 99 px limited to 25 px); fitted to the gap |
| `card.glow.alpha` | 0.35 | 0.45 | the tighter halo needs a little more strength |

Looked at and approved as is: the font, the conic seam at the start angle, the glow colour `#aa7a5c`, the body weight
(no real 500 file needed), the compact tile strips with the turning border (`card.compact.borderAnimate` is not needed),
and no clipping of the slant on the Clock (the slant stays 10). The contrast table (task 6.1) is against the card's
base colour; the cards are translucent, so the contrast over the background depends on the background (the same as
Prism).

## Risks / Trade-offs

- [The sum of effects costs more than the border alone] -> Decision 4 measures it; levers are named.
- [Ramp or conic colours fail contrast on the fill] -> Lighten and record (decision 7).
- [Slant clips on small cards or at 150 %] -> Decision 6.
- [A conic seam is visible at the start angle] -> The first and last stops are equal (`#ff3ea5`); verify on the panel.

## Migration Plan

Additive. Rollback is removing `rigline` from `BuiltInNames`; a config naming it falls back to the default theme with the
usual warning.

## Open Questions

- Whether the panel wants a real 500 weight for body text (decision 2) and whether `card.compact.borderAnimate` is needed (decision 5).
