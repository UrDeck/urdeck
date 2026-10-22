# Motion (animated border)

Measured 2026-10-21 on the owner's panel (HYTE Y70 Touch, 1100x3840 at 150 % during the run; the primary display is 180 Hz),
Release build of branch `feat/motion` copied to a scratch folder, Windows' animation effects on. The method is spike S2's
(`docs/perf/theme-look.md`): 15 s of warm-up, then one-second samples for 60 s; CPU is the process's processor time as a share
of one core (the machine has 32 logical processors), memory is the median of the samples. The page is S2's: 8 cards on 4x8 cells
(Clock 4x2, two Stats 2x2, two Shortcuts 1x1, Clock 2x1, Weather 4x2, Clock 4x1) and a second page of 2 cards, no dock. The theme
is the Rigline fixture (`tests/UrDeck.Engine.Tests/Fixtures/themes/rigline`): a six-colour conic border 0.014 cell wide, one turn
in 7 s. Its variants differ only in `card.borderAnimate`: absent (still), `spin`, `pulse` (period 7 s). Each case ran twice.

## What is built

Over each card whose style has an animated border, as the child visual of its `WidgetView`: a `SpriteVisual` painted with a
`CompositionMaskBrush`. The source is one 512x512 conic picture per style (`BorderPicture`, drawn by the engine with the same
stops and starting angle as the still border), scaled 1.5 about the card's centre so the turning square always covers the card;
the mask is the ring (`BorderPicture`, drawn by `SizedCardChrome`'s own shape and stroke), stretched round the card as a
nine-grid, or used whole for a card too small for its two insets (a circle, a card as small as its corners). `spin` turns the
source brush, `pulse` animates the sprite's opacity between 1 and 0.55; both use a step easing of `period x 15` steps. The card
back is painted by `ResolvedCardStyle.WithoutBorder()`, so the card is painted once and never repainted for the animation. The
pictures live on the style and are released with it; the sprites are released with their page. The animations are paused by
`MotionGate` (display off, deck hidden, a level below `full`) and start again together.

## Numbers

Rigline as it ships, with its moving `horizon` shader behind the cards (`Motion: full`):

| Case | CPU, share of one core | Working set | Private bytes |
|---|---|---|---|
| Still border (no `borderAnimate`), two runs | 3.44, 3.64 % | 238.5, 237.2 MB | 197.9, 202.4 MB |
| `spin`, two runs | 7.09, 7.10 % | 247.9, 243.0 MB | 204.9, 206.7 MB |
| `pulse`, two runs | 5.48, 5.24 % | 243.4, 242.7 MB | 205.1, 204.2 MB |

The still border's 3.5 % is the shader (about 30 frames a second). The same cases with `"background": { "type": "none" }`, the
form S2 measured:

| Case | CPU, share of one core | Working set | Private bytes |
|---|---|---|---|
| Still border, two runs | 0.44, 0.29 % | 209.2, 208.8 MB | 154.2, 153.8 MB |
| `spin`, two runs | 3.67, 3.90 % | 213.4, 213.9 MB | 157.6, 159.1 MB |
| `pulse`, two runs | 1.84, 2.26 % | 212.5, 211.8 MB | 157.8, 157.2 MB |

Against the budget (`widget-card`, "Animated Border Cost": at most 5 % of one core and 8 MB private for eight cards):

- **`spin`: +3.2 to +3.6 % of a core, +3.4 to +5.3 MB private** without a background, +3.5 to +3.7 % and +4.3 to +7.0 MB with the
  shader. Inside, and a little above S2's spike (+2.7 %, +2.5 to +3.4 MB; the run was at 150 % here, so the pictures are larger).
  The private figure varies by about 4 MB between runs of the same case, so +7.0 MB is close to the line but not over it.
- **`pulse`: +1.4 to +2.0 % of a core, +3.4 to +3.6 MB private** without a background (+1.6 to +2.0 % and +1.8 to +7.2 MB with
  the shader). Cheaper than `spin` because there is one opacity animation per card and no picture is turned.

At `"motion": "off"` the same page, still border against `spin`: 0.35 and 0.59 % against 0.26 and 0.74 % of a core, 162.7 and
162.8 MB against 159.4 and 163.2 MB private. Equal within noise: below `full` no sprite and no animation is made, and the shader
is drawn once (`Background: ... still`), which is why both are far under the 3.5 % of the first table.

## Theme switches

Five round trips between `default-dark` and the Rigline variants (ten switches), the host started fresh, private bytes after the
fifth against before the first, two runs:

| Case | Private bytes | Working set |
|---|---|---|
| `rig-still` | +60.7, +63.7 MB | +69.4, +72.6 MB |
| `rig-spin` | +63.4, +79.1 MB | +72.0, +87.6 MB |

The host grows on theme switches by itself (STATUS.md, "Separate backlog items"). The spinning case is at the still one's level in
one run and 15 MB above it in the other, so a longer run decided it: twenty round trips (forty switches), one run each,
**+66.0 MB private for `rig-still` and +62.7 MB for `rig-spin`**. The growth does not follow the number of switches (40 switches
cost no more than 10) and the spinning case does not exceed the still one: the border's pictures and sprites are released with
their style and page.

## On the panel

See `docs/design/look-and-feel/STATUS.md` (PR 9) for what the owner looked at.

## Rigline as a whole theme

Measured 2026-10-22 on the owner's panel (1100x3840), at 150 % (the first set) and at 100 % (the second), Release build of branch `feat/theme-rigline` copied to a fresh
scratch folder for each run, the method above (15 s of warm-up, 60 one-second samples; CPU as a share of one core, memory the
median). The theme is the built-in `rigline` as it ships: the conic border turning in 7 s, `card.glow`, `valueGlow` and
`valueFill` on every number, ring gauge glow, scanlines, tile strips with glow, outline icons with glow. The page is the site deck:
Clock 4x2, two Stats 2x1, Weather 4x2 and four shortcuts in the dock, eight cards in all (the page's four and the dock's four
all carry a border sprite). Each case ran twice; `motion` was set in the configuration, and the shader is `horizon`, moving.

At 150 % (two runs each):

| Case | CPU, share of one core | Working set | Private bytes |
|---|---|---|---|
| `full`, `horizon` moving | 7.60, 7.68 % | 233.6, 229.9 MB | 194.1, 191.3 MB |
| `reduced` | 1.02, 1.17 % | 210.5, 208.4 MB | 151.2, 152.6 MB |
| `off` | 1.28, 0.99 % | 209.5, 207.5 MB | 151.2, 152.2 MB |
| `full`, no background | 4.51, 3.88 % | 205.1, 204.2 MB | 146.7, 147.3 MB |
| `reduced`, no background | 1.35, 1.20 % | 197.3, 201.1 MB | 143.0, 145.0 MB |

At 100 % (two runs each):

| Case | CPU, share of one core | Working set | Private bytes |
|---|---|---|---|
| `full`, `horizon` moving | 7.66, 7.06 % | 234.2, 230.8 MB | 190.0, 188.9 MB |
| `reduced` | 1.43, 1.07 % | 210.5, 211.3 MB | 152.6, 152.2 MB |
| `off` | 1.25, 1.41 % | 209.9, 211.2 MB | 152.8, 152.9 MB |
| `full`, no background | 4.45, 3.31 % | 202.7, 205.2 MB | 147.0, 146.9 MB |
| `reduced`, no background | 1.09, 1.56 % | 200.2, 199.8 MB | 142.3, 144.8 MB |

The two scalings come to the same within the noise, so the figures below are given for both.

- **The border, on its own** (`full` against `reduced`, both with no background): **+2.9 % of a core and +3.0 MB private** at 150 %, **+2.6 % and +3.4 MB**
  at 100 %, for eight cards, inside the budget (`widget-card`, "Animated Border Cost": 5 % and 8 MB) and at the level the motion change measured
  (+3.2 to +3.6 %, +3.4 to +5.3 MB).
- **The moving shader** (`full` against `full` with no background): +3.4 % of a core and +45 MB private at 150 %, +3.5 % and +42 MB at 100 %. It is the shader's surface
  and 30 frames a second, not the theme; it is the cost of any moving background and the owner can switch it off.
- **Everything at `full`** is 7.6 % of a core and 192.7 MB private at 150 %, 7.4 % and 189.5 MB at 100 %. Not a budget in itself (the budget is for the border).
- **`reduced` and `off` cost what the still theme costs**: 1.0 to 1.4 % of a core and 151 to 153 MB private with the shader (drawn
  once, still), the same in both, and no sprite or animation is made (`Border animation started` is not in their logs). The glow,
  the value effects and the scanlines are cached pictures that are drawn once.
- Noise: runs of the same case differ by up to 0.4 % of a core and 3 MB; the figures above are two runs each, not a long series.

**Pausing**, from the live deck's log (`urdeck.log`), with the monitor turned off by Windows' power setting after a minute idle: at
`Windows: display off` the background logged `Background frames stopped (display off): 2712 frames in 91.1 s` (29.8 a second) and
the border `Border animation stopped (display off)` for both pages (the page's four cards and the dock's four); ten seconds later
`Windows: display on` restarted both (`Background frames started (display on)`, `Border animation started (display on)`).

**Hidden deck**: with the deck running, the configuration's `display` was changed to a monitor that is not connected. The host
logged `Background frames stopped (deck hidden): 358 frames in 12.1 s`, `Border animation stopped (deck hidden)` for both pages and
`Deck hidden: waiting for 'Nonexistent display', which is not connected`; with the configuration put back, `Placement: Absent ->
Settling -> Placed` started the background and both pages' borders again.

Not measured here: GPU load (the Task Manager figure was not recorded).
