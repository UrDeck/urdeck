## Context

See proposal.md for why. What exists:

- `ThemeMotionDefinition.Level` and `ThemeCardBorderAnimateDefinition` (`mode` `none|spin|pulse`, `period` 1 to 120 s) are
  read, sanitized and merged (`ThemeFormat2.Motion.cs`, `ThemeFormat2.Card.cs`); `THEME051` (spin without a conic gradient)
  and `THEME090` exist. Nothing resolves `BorderAnimate`; `ResolvedCardStyle` has `BorderGradient`, `BorderConic` and
  `BorderAngle`, and `SizedCardChrome` draws the border (still) into the cached card back.
- `BackgroundLayer` already stops its frames for display off (`SetDisplayOn`, fed by `DisplayPowerWatcher`) and a hidden
  deck (`SetVisible`). A shader with `speed` 0 is still (`DeckBackground.IsMoving`), and `RequestKey` includes the speed.
- `DisplayPowerWatcher` reads window messages in a subclass of the deck window (`SetWindowSubclass`, id 2; `WindowFocus` is 1).
- Spike S2 (`spike/theme-look-s2`, `src/UrDeck.Host/BorderSpike.cs`, read it with `git show spike/theme-look-s2:src/UrDeck.Host/BorderSpike.cs`)
  built the border as a `SpriteVisual` with a `CompositionMaskBrush`: source a 512x512 conic picture shared by all cards
  (`Stretch = UniformToFill`, scale 1.5 about the card's centre so the turning square always covers the card), mask a small
  ring picture as a hollow nine-grid (insets `radius + width + 1`, inset scale `1 / rasterization scale`), set as the
  `WidgetView`'s child visual so the press scales it. A `ScalarKeyFrameAnimation` on the source brush's
  `RotationAngleInDegrees`, 0 to 360, `Forever`, with `CreateStepEasingFunction(period × 15)`. Measured: +2.7 % of a core,
  +2.5 to +3.4 MB private for 8 cards; smooth was +19.5 %.
- **Eye check, 2026-10-21:** the owner watched the spike on the panel (150 %, Rigline's colours, 7 s per turn) at 15 and 30
  steps per second. 30 looks slightly better; 15 is fine ("it just doesn't change quite as quickly so it's not as
  noticeable"). 15 is kept. This settles the item in STATUS.md "Not verified".

## Goals / Non-Goals

**Goals:**
- One pure policy decides the level; the host only feeds it inputs.
- The animated border costs no card repaint and nothing at all below `full`.
- The snapshot, and so the goldens, are byte-identical to today.

**Non-Goals:**
- `Theme.Motion` in the SDK, widgets honouring the level, the page slide or press at `off` (follow-up).
- `valueFill.animate`, `pattern.scroll` (deferred keys stay deferred).
- A smooth (unstepped) animation or a theme-set step rate.

## Decisions

### 1. `MotionPolicy` in the engine

A small pure type (`src/UrDeck.Engine/Themes` or a new `Motion` folder) with
`Decide(string? configMotion, bool windowsAnimations, string? themeLevel)` returning the effective level, the user's level
(before the theme's ceiling, for the background, decision 6) and the source (`configuration`, `Windows`, `theme`) for the
log line. Levels are an enum `Off < Reduced < Full`. Parsing the config value (trim, lower case, unknown → absent with a
warning) lives here too, so the truth table is one test class on Linux. The theme's level is already sanitized.

Alternative: a `min()` of all three (the package's `03` §1.9). Rejected in the explore: the user's explicit value must be
able to override Windows in both directions, which `min()` cannot.

### 2. Windows' setting through Win32, not `UISettings`

Read `SystemParametersInfo(SPI_GETCLIENTAREAANIMATION)` (the value behind Settings → Accessibility → Visual effects →
Animation effects) at startup, and again on `WM_SETTINGCHANGE` in a window subclass (a new watcher beside
`DisplayPowerWatcher` with subclass id 3, or `DisplayPowerWatcher` widened and renamed; the applier picks whichever reads
better). Re-read on any `WM_SETTINGCHANGE` rather than matching `wParam`, it is cheap and the parameter is not reliable for
every sender.

Alternative: `UISettings.AnimationsEnabled` with `AnimationsEnabledChanged`. The event's availability for the host's
19041 target was not confirmed, and `UISettings` events arrive on a thread-pool thread that must be marshalled anyway.
**Verify on apply** that toggling the Settings switch flips the value and delivers `WM_SETTINGCHANGE` to the deck window
(it is `WS_EX_NOACTIVATE`, but broadcasts go to every top-level window). If it does not arrive, fall back to re-reading on
display-on and on a configuration reload, and record it in "Findings during apply".

### 3. The border is a sprite per card, as in S2, with the engine making its pictures

- **Resolution.** `ResolvedCardStyle` gains the resolved animation: mode, period. It is null when the mode is `none`, when
  `spin` has no conic gradient (`THEME051`), when the border is dashed (`THEME057`), or when the border width is 0. The compact
  style resolves its own (`card.compact.borderAnimate` merged key by key, as the other compact values).
- **The source.** `spin`: one square conic picture per style (512 px is S2's size; the applier may check that 512 holds
  up on the 4x2 card at 150 %), the same stops and starting angle `SizedCardChrome` uses, so that rotation 0 is the still
  border. `pulse`: no rotation, the source is a `CompositionColorBrush` for a solid border, a `CompositionLinearGradientBrush`
  whose start and end points are computed exactly as `SizedCardChrome` computes the linear border gradient, or the conic
  picture. The pictures are made in `src/UrDeck.Engine/Rendering` (pure Skia, tested on Linux), decoded once by the host
  into a `LoadedImageSurface` and kept on the style, as `UnderlayPicture.HostSurface` and `PatternTile` do.
- **The mask.** A ring picture per (shape, radius or cut, cut corners, border width) in physical pixels, drawn white with
  **the same border path routine** `SizedCardChrome` uses (round, chamfer with its `cutCorners`, and the compact circle),
  sized `2 × inset + 2` with `inset = corner extent + width + 1`, used as a hollow nine-grid. It is the border's shape,
  so the hairline rule (a border of 2 px or less is a whole pixel) holds. A dashed border is excluded because a
  nine-grid stretches the straight runs, which breaks the per-side dash fit.
- **The sprite.** As in S2: the `WidgetView`'s child visual (that slot is unused today), so it scales with the press and
  slides with the page; the page's `UnderlayView` stays underneath. `WidgetView.Dispose` clears the child visual.
- **The card back leaves out the ring.** When the host animates a style, it paints the card backs with a derived style that
  has no border (`ResolvedCardStyle.WithoutBorder()`, made once and kept on the parent like `Compact`, with its own
  cached backs). Nothing else in the back changes. The snapshot and `PageRenderer` never use it, so they draw the still
  border, and the goldens do not change. Alternative: a flag in the back's cache key; rejected because the derived style
  keeps every existing cache and key untouched.
- **Below `full` there is no sprite at all.** The host builds cards with the original style and no sprite, exactly as for a
  theme without `borderAnimate` (the spec's cost requirement). A change of the effective level therefore rebuilds the
  page, which is rare (a setting change).

### 4. Steps and the animation

`spin`: S2's key-frame animation on the source brush's `RotationAngleInDegrees`. `pulse`: a scalar animation on the
sprite's `Opacity`, key frames 1 → 0.55 → 1 over the period with a step easing of `period × 15 / 2` per half.
The step rate is a host constant `BorderStepsPerSecond = 15`. All sprites of a page start their animation together, from
angle 0 or opacity 1, so the borders turn in phase.

### 5. One gate for display off and a hidden deck

A small host type (`MotionGate`) holds `displayOn`, `deckShown` and the effective level, and raises one event when
"animations may run" changes. `MainWindow` feeds it from `DisplayPowerWatcher` and from where it calls
`_backgroundLayer.SetVisible`, and both the background layer and the border sprites follow it. Pausing stops each
sprite's animation (`StopAnimation`) and resuming starts it again from 0. Phase continuity after a pause is not needed.

The background layer keeps its own reasons in its log lines; changing it to read the gate is optional. The requirement is
only that the two never disagree about display off and hidden.

### 6. The background follows the user's level, not the effective one

`BackgroundPlan.Resolve`/`DeckBackground.Create` take the user's level. Below `full`, a shader candidate is created with
speed 0, so the existing "speed 0 is still" path draws it once. The speed is in `RequestKey`, so a change restarts the
layer and an unrelated save does not.

Alternative: skip to the theme's `background.static` (the package's other option). Rejected: it changes what the user
chose, and many themes have no still background. Freezing keeps the look.

`--snapshot` passes the configuration's level only (Windows is treated as on), so snapshots are reproducible.

### 7. `THEME057`

In `ThemeDefinition.Rules.cs` beside `THEME051`, in `ThemeCodes`, the `theme-lint` table and `docs/themes.md` ("Checking a
theme"), with a pass/fail fixture pair under `tests/UrDeck.Engine.Tests/Fixtures/lint/THEME057/`, as the other codes have.
`ContinuousAnimations()` stays as it is; `THEME090`'s message ("each repaints on every frame") is no longer true for a
border. Change it to "each costs CPU while it runs".

## Risks / Trade-offs

- [The nine-grid ring and the Skia ring differ at the corners by more than anti-aliasing] → the mask is drawn by the same
  path routine at physical pixels; a host-only test cannot compare pixels, so the panel look at 100/125/150 % checks it
  (tasks 5.x), with the border switched between animated and `reduced` to compare.
- [Conic source on a very wide card: UniformToFill × 1.5 may not cover the corners while it turns] → a 4x1 card is 4:1;
  the source side is the card's width × 1.5, which covers the diagonal (1.03 × width). Covered for every grid shape;
  note it in the code.
- [`WM_SETTINGCHANGE` not delivered or `SPI_GETCLIENTAREAANIMATION` not tied to the Settings switch] → verify first
  (task 3.1); fallback in decision 2.
- [Page rebuild on a level change loses widget state] → the same as a theme change today; accepted.
- [Memory on theme switches] (STATUS.md, "Separate backlog items") → the pictures live on the style and are dropped with
  it; the sprites are cleared in `WidgetView.Dispose`. The theme-switch check in tasks catches a leak of this change's own.
- [Pulse not measured] → it is one opacity animation per sprite; measured with spin in the same run (task 5.3).

## Migration Plan

None. A configuration without `motion` follows Windows, which on a default Windows install is `full`, so nothing changes
for an existing user unless they turned animations off in Windows, in which case a moving background now stands still.
That is intended; the release notes and the configuration docs say how to get it back (`"motion": "full"`).

## Findings during apply

- **Compact cards take the page card's `borderAnimate`.** The format has no `card.compact.borderAnimate` key (`ThemeCardCompactDefinition`
  holds radius, border width, shape, strip and marks), so the compact style inherits the page card's animation and resolves only
  what it overrides (its own border width, radius, shape). A compact card with `card.compact.borderWidth` 0 has no animation. Adding the key is a
  format change; left out.
- **A ring too big for its card is drawn at the card's size.** The nine-grid needs a card of two insets (corner reach + border
  width + 1) on each side; a circle (compact dock slot) or a card as small as its corners is not, so `BorderRing` draws the ring
  at that card's exact size and the host stretches it whole (the same split as the underlay's nine-grid and exact pictures).
- **A conic `pulse` uses the conic picture, not a brush.** `BorderConicSource()` is made for any animated conic border; the
  gradient brush is for solid and linear ones only.
- **An unrelated configuration save rebuilds the page** (existing behaviour: a reload forces a rebuild), so the border sprites are
  made again and the log has one more `Border animation started (page rebuilt)`; no `Motion:` or `Background` line, and the
  background does not restart. Not changed here.
- **Measured** (`docs/perf/motion.md`): inside the budget in every case; the first batch of the cost runs compared against the
  shader-backed page, so the border's own cost was also measured with `"background": { "type": "none" }`, as S2 did.
- **Windows' setting (3.1, 5.2), checked by the owner 2026-10-21:** toggling Settings, Accessibility, Visual effects, Animation effects
  off and on delivers `WM_SETTINGCHANGE` to the deck window, `SPI_GETCLIENTAREAANIMATION` follows it, and the border stops and
  resumes (`Windows: animation effects off`, `Motion: off (Windows animation setting)`, then back). No fallback needed.
- **Display off and on (5.1):** a monitor-off timeout of one minute logged `Border animation stopped (display off)` and
  `started (display on)`. The tray menu has no hide/show entry, so the hidden-deck path was not driven by hand; it goes through
  the same gate as placement's `HideDeck`/`ShowDeck`. That run had `"background": none` in the scratch config, so the background's
  own display-off lines were seen in the earlier run only.
- **Panel look (5.5), owner, 2026-10-21:** swipes and presses at 100, 125 and 150 % looked right. With `"motion": "reduced"` set in the
  configuration the border stands still, and removing it brings the motion back without a restart. The dock was not looked at
  separately, and no corner difference between the still and the turning ring was reported.
