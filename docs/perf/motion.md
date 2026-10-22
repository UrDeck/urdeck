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
