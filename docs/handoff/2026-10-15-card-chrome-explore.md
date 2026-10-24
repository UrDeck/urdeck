# Handoff: explore `card-chrome`

**For:** a fresh Opus session that runs `/opsx:explore card-chrome` and ends with a design that can be proposed.
**Date:** 2026-10-15. **Read first:** `AGENTS.md`, `docs/design/look-and-feel/STATUS.md`.

This is an exploration, not an implementation. The output is a decision on each question below, written down, and a
list of the OpenSpec changes it splits into (a proposal per change comes after, in its own step). Do not write
production code. A throwaway prototype is fine when a question can only be answered by trying it; keep it on a local
branch (`spike/card-chrome-*`) and report the numbers, as the earlier spikes did.

## What card chrome is

A **card** is the rounded rectangle every widget sits on. Today it is a filled rounded rectangle with an optional
border, drawn by `WidgetPainter.DrawCard` (`src/UrDeck.Engine/Rendering/WidgetPainter.cs`) into the widget's own
surface, before the widget draws its content. Its look comes from the theme's `card` values: radius, border width, gap,
padding, fill colour. All three built-in themes are rounded translucent cards.

**Card chrome** is everything about the card that is not its content: its shape, fill, border, and what it casts or
wears around it. The eight drafted themes need it, and the theme format already holds the values (`theme-format-2`,
merged), but nothing draws them. The keys (`docs/design/look-and-feel/02-theme-schema.md` section 4.3):

| Group | Keys | Drawn where |
|---|---|---|
| Shape | `shape` (`round`, `chamfer`), `cut`, `cutCorners`; compact cards may be a `circle` | inside the card |
| Fill | `fillGradient`, `tint` (`cycle` through `colors.cardTints`), `sheen` (a soft radial light) | inside the card |
| Border | `borderStyle` (`solid`, `dashed`), `borderDash`, `borderGradient` (linear or conic), static | inside the card's edge |
| Edge light | `highlight` (rim lines top and bottom), `accentStrip` (a lit line along one side, with `glow`) | inside the card, the highlight over the content |
| Marks | `cornerMarks` (`bracket`, `tick`), with an `inset` that may be negative, so outside the card | inside or outside |
| Outside the card | `shadow` (soft or hard offset), `glow` | outside: the card's surface is exactly card-sized today |
| Small cards | `compact{...}` overrides for cards of one cell or less and for dock slots | all of the above |

The look-and-feel work is mostly this. Cards are what the eye sees first, and every theme but Prism and Paper changes
them in a visible way.

What each draft needs (from `tests/UrDeck.Engine.Tests/Fixtures/themes/*/theme.json`):

| Theme | Card keys it uses |
|---|---|
| Blueprint | `borderStyle` dashed, `borderDash`, `cornerMarks` (also compact) |
| Carbon | `cornerMarks`, `accentStrip` |
| Ember | `shape` chamfer, `cut`, `cutCorners`, `fillGradient`, `borderGradient` |
| Obsidian | `fillGradient`, `borderGradient`, `highlight`, compact `shape` (circle) |
| Paper | `shadow`, `highlight` |
| Pop | `tint`, `shadow` (hard), compact `radius` |
| Prism | `fillGradient`, `highlight`, `sheen`, `shadow` (soft), compact `radius` |
| Rigline | `fillGradient`, `borderGradient` (+ `borderAnimate`, which is the `motion` change), `glow`, compact `accentStrip` |

## What is already decided (do not re-litigate; challenge only with new evidence)

- **Outside the card is a compositor sprite per card, under the widgets, in `PageHost`** (spike S1,
  `docs/perf/theme-look.md`, "S1 page underlay"). One small Skia picture, a nine-grid, stretched around each card by a
  `CompositionNineGridBrush`. A page-sized canvas cost +33 MB and is out; sprites cost about +1.2 MB. The prototype is
  `src/UrDeck.Host/PageUnderlay.cs` on the local branch `spike/theme-look-s1`. `PageRenderer` (snapshots) draws the same
  bleed with Skia directly, and both paths must share one drawing routine.
- A nine-grid needs the card to look the same in every corner region: radius, shadow, glow, corner marks and a chamfer
  qualify; anything that varies along an edge does not (a dashed border does not stretch).
- **Animated borders are not this change.** `borderAnimate` is the `motion` change (spike S2: a sprite with a stepped
  rotation at 15 steps per second). Here the border is static, including `borderGradient` conic and linear.
- **`card.blur` (backdrop blur) is out.** `pattern` is its own change, `page-pattern`.
- **A v1 theme must stay byte-identical.** The goldens (`tests/UrDeck.Engine.Tests/Goldens`, `GoldenTests`) prove it;
  `URDECK_UPDATE_GOLDENS=1` is the only way to move them and a change to a v1 golden needs a reason.
- **CPU and memory matter; installer size and disk do not.** Budgets (`03-engine-work.md` section 5): static chrome per
  card repaint at most 1.5 times today's `WidgetPainter.Paint` (`docs/perf/theme-card-paint.md`: 0.28 ms for a 4x2 card);
  the underlay at most 20 ms to build and 2 MB; v1 themes no regression.
- **Widgets draw no card.** Chrome belongs to the host and the theme; the SDK API should not grow with host-only
  concepts (the package keeps `ResolvedCardStyle` engine-internal).
- **Where something is unmeasured, say so and measure once.** The owner prefers a look by eye over a long pixel loop;
  put the picture in front of them and say what to look at.

## Questions to settle (the explore must answer each, with evidence)

1. **The painter split.** `WidgetPainter.Paint` draws the card, clips to its shape, calls the widget. The package wants
   `DrawBack` (shadow aside: fill, sheen, strip, border) before the widget and `DrawFront` (highlight, inside corner
   marks) after it, so content never covers a rim light or a mark, with a fast path for a v1 theme. What is the right
   shape of that split in code (a `CardChrome` and a `CardShape`, `SKRoundRect` fast path versus an `SKPath`), what is
   engine-internal versus `Theme`, and how does the error card (`DrawError`) fit?
2. **Where each piece is drawn.** For every row of the table above, inside the widget surface or in the sprite under it.
   Corner marks and the glow are the hard ones: a mark with a negative `inset` is outside the surface, a mark with a
   positive one is inside. Does a bracket mark that sits half in, half out need both? What does a snapshot do?
3. **The press effect.** The compositor scales a pressed card to about 0.96 and dims it. In S1 the shadow stayed at full
   size and showed as a thin band around the pressed card. Should the sprite scale with the card, stay, or fade? Check
   `WidgetView` and the input path before deciding.
4. **Dashed borders on a chamfer.** A dashed border does not stretch, so it cannot live in a nine-grid sprite, and
   `SKPathEffect.CreateDash` on a chamfered path has its own corner behaviour. Inside the surface this is just a stroke;
   say so, or say what breaks (the dash phase at the corners, hairline snapping at 1 px).
5. **The dock.** The dock is a `PageHost` in an overlay with slots resolved from a smaller cell
   (`DockLayout.SlotSide`, `ThemeResolver.Resolve(loaded, side)`), and it got no underlay in S1. What does a dock
   underlay need, and does `compact` (circle slots, Obsidian) change the sprite?
6. **Translucent cards and reach.** With sprites a shadow or glow that reaches further than the gap lies under the
   neighbour and shows through a translucent fill (S1: 43 px reach against a 30 px gap, faint). The rule drafted for the
   validator is: reach at most the gap unless the fill is opaque. Prism, a translucent theme with a soft shadow, breaks
   it. Is a warning enough, or should the engine clamp the reach?
7. **Compact cards.** `card.compact` applies at one cell or less and in the dock. The package suggests a second SDK
   `Theme` for compact cards. Confirm the detection rule, what a widget sees (padding, radius), and `circle` needing a
   square card (fallback: round with an info log).
8. **Tint and index.** `tint: cycle` colours cards by their index in page order (and dock slot order). Where does the
   index come from, and what is the index of a card that moved to another page or a hidden widget?
9. **Hairline snapping.** Carbon, Blueprint and Obsidian rely on 1 px lines. The package says round line widths of 2 px or
   less to whole pixels at 1x and log when one resolves under 1 px. Where in the resolver, and does it change any v1
   border (it must not)?
10. **Cost of the drawn pieces.** Which pieces are expensive enough to need a cache (a gradient shader per size, a blurred
    shadow, a path per size)? The existing rule: nothing allocated per paint but the unavoidable paths, cached per size.
11. **Slicing.** What is the smallest set of changes that each leaves `main` working, passes the goldens and can be
    reviewed? The likely cut is shape and fill and border inside the surface first, then the sprite underlay for
    shadow, glow and outside marks, then the dock and compact cards. Say if the explore finds a better cut, and which
    needs Opus and which Sonnet.

## What to read

- `docs/design/look-and-feel/03-engine-work.md`, sections 1.1 to 1.3 and 3 to 5 (the proposed architecture; the package is a
  snapshot and the specs win where they differ).
- `docs/design/look-and-feel/02-theme-schema.md`, sections 4.3 and 5 (the keys, the draw order, the cross-field rules).
- `docs/design/look-and-feel/06-themes.md` and the mocks for what the cards should look like. The mocks are **not in the repository**: they are in the original package at
  `C:\Users\patri\source\repos\urdeck-look-and-feel-handoff\assets\design-board\` (`themes-1-carbon-rigline-prism-paper.png`,
  `themes-2-ember-blueprint-pop-obsidian.png`; add the folder with `/add-dir`, and do not edit it). The eight drafts are in `tests/UrDeck.Engine.Tests/Fixtures/themes/`.
- `docs/perf/theme-look.md` (S1, S2) and `docs/perf/theme-card-paint.md`.
- Code: `src/UrDeck.Engine/Rendering/WidgetPainter.cs`, `PageRenderer.cs`, `src/UrDeck.Engine/Themes/ThemeResolver.cs`,
  `ThemeDefinition.Card.cs`, `src/UrDeck.Host/WidgetView.cs`, `PageHost.cs`, `MainWindow.cs`
  (the dock overlay), `src/UrDeck.Engine/Layout/DockLayout.cs`.
- `openspec/specs/widget-card/spec.md`, `theme/spec.md` ("Theme Value Groups"), `dock/spec.md`, `widget-input/spec.md`
  ("Press Feedback"); the deferred keys are in `docs/design/theme-format-2-deferred-keys.md`.
- Archived changes with the recent pattern for a change of this size: `openspec/changes/archive/2026-10-15-theme-typography`
  and `2026-10-13-theme-format-2`.

## Ground rules

- Branch for any prototype: `spike/card-chrome-<topic>`, off `main`, local only. Never commit to `main`, never merge.
- To run the host on the panel: build, copy the Release output to a scratch folder (one host per application folder), start it
  detached, read `urdeck.log`, quit with `UrDeck.Host.exe --quit`. Screen capture and the panel coordinates are in
  `AGENTS.md` and the memory note `display-setup`. Prefer one capture, or ask the owner to look.
- `--snapshot` renders without a screen and is the way to compare pixels of the engine's path.
- Record anything you measure in `docs/perf/` in the style of `theme-look.md`, on your branch.

## Deliverable

1. A short design note, `docs/design/card-chrome.md`: one decision per question above, with the evidence (a number, a
   picture, or the code you read), what you did not verify, and the pieces that must be measured during the apply.
2. The proposed split into OpenSpec changes, each with its name, scope, tasks in outline, dependencies and a model
   recommendation. Do not create the changes; the next session proposes them.
3. Anything in `STATUS.md` that is now wrong or answered. Commit the note on a `docs/card-chrome-design` branch and open
   a pull request when the owner agrees.

## Prompt to paste

"Read `docs/handoff/2026-10-15-card-chrome-explore.md`, `AGENTS.md` and `docs/design/look-and-feel/STATUS.md`, then run
`/opsx:explore card-chrome`. Answer the eleven questions with evidence, prototype only where a question needs it, and
finish with the design note and the proposed split."
