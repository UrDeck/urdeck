## Context

Today `BackgroundSettings.Parse` reads the configuration's `background` into one of `None`, `Shader` or `Image`, and
absence is `None`. `DeckBackground.RequestKey` and `Create` take those settings and the theme; the host's
`BackgroundLayer` keys on the request key, so an unrelated configuration save does not restart the motion, and a
background that failed is not tried again until its key changes. A failure shows the theme's background colour.
The four built-in shaders are embedded folders; `ShaderStore` finds user folders first.

The handoff (`07-pairing-and-editor.md`, `02` section 4.11, `05`) specifies the precedence, the pairing data and the
catalogs. `theme-format-2` has already put `background` (`default.name`, `static`, `scrim`) and `meta.pairsWith` on the
loaded theme as data. This change gives them behaviour. The twelve new shaders are the next change and need the
manifest key added here (the lint warns `SHADER004` on a key it does not know, and CI runs the built-ins with
`--fail-on-warnings`).

## Goals / Non-Goals

**Goals:**
- One place that decides what the deck's background is (`BackgroundPlan`), tested as a table.
- A theme can bring its own background, a still fallback and a scrim, with no change for themes that define none.
- Pairing data on both sides, an index that joins them, a ranked recommendation, and read-only catalogs.
- Snapshots draw exactly what the deck draws.

**Non-Goals:**
- New shaders, new themes, a settings UI, motion policy (`reduceMotion`, `UISettings`), `theme validate`.
- A theme's own default `parameters` and `colors` (deferred), picture `fit` modes other than cover (deferred).
- A built-in theme with a picture: built-ins are embedded and a picture would be an embedded resource. None needs one.

## Decisions

**A plan is an ordered list of candidates, not one answer.** `BackgroundPlan.Resolve(settings, theme)` returns the
candidates in precedence order: the configuration's choice (when it names one), the theme default, the theme's still
background, the colour. The host tries them in order and takes the first that builds; a failure is logged once and the
next is tried. This keeps "a failed shader falls through the same list" in one place instead of in each of the layer,
the snapshot and a future preview. Alternative: resolve to one background and fall back to the colour as today.
Rejected: it would drop the theme's still background exactly when it is useful (a shader that cannot compile).

**Absence needs to be distinguishable from `none`.** `BackgroundSettings` gains an `IsExplicit` flag (and `Kind.None`
with it means the user said `none`). Absent and `null` parse to "no choice". The existing `BackgroundSettings.None`
singleton stays for "nothing to show". The configuration writer never writes `background` back, so no file is
rewritten by this.

**The request key names where the choice came from.** `RequestKey` adds the source (`configuration`, `theme default`,
`theme still`) and, for a theme's choice, the theme's name and the palette, so changing the theme to one with another
default restarts the layer and changing an unrelated setting does not. A theme that defines nothing leaves the key
null, as today.

**Still backgrounds reuse what exists.** A picture goes through `BackgroundImage.Prepare` (decode once, cover-fit,
refuse above 8192 x 8192 pixels) with its path resolved inside the theme's folder and checked with the same
path-escape rule fonts use. A gradient or colour is drawn into the still `SKXamlCanvas` the layer already uses for a
still picture; no new surface type. Radial gradient values are fractions of the deck (centre as a fraction of width and
height, radius as a fraction of width), so they are resolution independent like every other theme size.

**The scrim is painted in the background's own surface, last.** For a shader the swap chain panel draws it after the
shader; for a still background the canvas draws it after the picture. No extra layer, no extra pointer surface, and
the snapshot path does the same two steps. It is skipped for the plain colour and for `none`.

**`motion.level` does not touch the background.** The handoff contradicts itself: `02` section 4.12 says the ceiling
does "not control the shader background" and `07` section 2 says an effective level of `off` swaps a shader for the
still background. This change follows `02`: the shader has its own `speed` and display-off handling, and the theme's
ceiling is for animation the theme itself drives. The theme's still background is therefore a fallback for failure and
for themes with no default, not a reduced-motion substitute. If a user setting for reduced motion arrives (the motion
change), it can decide this again with the user in the loop.

**Pairing is data joined at read time.** `PairingIndex` takes the catalogs' entries, builds two dictionaries once and
answers lookups; it never loads a shader or a font and never logs. The ranking is the five rungs of the handoff
(default, mutual, theme only, background only, tag match for backgrounds with no data); there are no scores to tune.
Names are compared exactly because theme and background names are folder names.

**Catalogs are cheap and separate from loading.** `ThemeCatalog.List` reads each theme's JSON and its `meta` and
swatch colours without resolving fonts; `BackgroundCatalog.List` reads manifests through the existing reader without
compiling. A broken entry is skipped with one warning. Both live in the engine and are what the future editor and the
command line tool call.

**`pairsWith` is a known manifest key, with its own lint.** `ManifestReader` reads `pairsWith.themes` (an array of
strings) and the lint reports a wrong type or an unknown key inside it as warnings with the existing manifest
diagnostics' code families; `docs/schemas/v1/shader-manifest.json` gains the key (additive; the manifest version stays
1, as unknown keys were always allowed).

**Built-in themes get `meta`, no background.** `default-dark`, `default-light` and `glass` gain name, description and
mode, and nothing else, so the "no change for existing decks" claim holds and a test proves it with snapshots.

## Risks / Trade-offs

- **Changing the meaning of an absent `background` (D7).** → Safe for the built-ins because they define none, covered by
  a scenario for each case, and the escape (`none`) is documented in the user docs and the site page. A user with a
  user theme that defines a default will see it appear; that is the point of the feature.
- **Restarting the layer on a theme change that alters the resolved background.** → The request key carries it; a test
  with a scripted theme sequence asserts "restart" and "no restart".
- **A still picture inside a theme folder is user-controlled input.** → Same decoder, same pixel cap and path-escape
  rejection as fonts and the existing image type; a test for `..\x` and an oversized file.
- **The plan hides which candidate was used.** → One log line per change with the kind, name and origin.
- **S1 found that the spike underlay needs a full-size surface for `pattern`; the scrim and still layer reuse surfaces
  that already exist.** → No new full-size surface is added here.

## Migration Plan

Deployed with the next release; no migration. A configuration without `background` behaves as before for the three
built-in themes and for any user theme without a `background` group. A user who later picks a theme with a default and
wants none writes `"background": { "type": "none" }`. Rollback is a revert.

## Open Questions

None that change what gets built. One for the editor later: whether "Use the theme's background" is shown as a choice
or as the absence of a choice. The spec supports both (removing the key is the former).
