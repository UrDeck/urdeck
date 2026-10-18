# Look-and-feel spikes (theme-look)

Three throwaway spikes run on 2026-10-13, before the card-chrome, typography and motion changes are specified
(`docs/handoff/2026-10-13-theme-look-spikes.md`). The prototype code and the scripts, configurations and captures the
sections mention (`spikes/theme-look-s1`, `-s2`, `-s3`, and `src/UrDeck.Host/PageUnderlay.cs`, `BorderSpike.cs`) are on the
local branches `spike/theme-look-s1`, `spike/theme-look-s2` (from S1) and `spike/theme-look-s3`. They are not for merging.
Installer size was deliberately not measured: CPU and memory are what matter for this product.

## Decisions at a glance

| Spike | Decision | Numbers | Carried to |
|---|---|---|---|
| S1 page underlay | Go, as **one compositor sprite per card** stretching a small Skia picture (a nine-grid). A page-sized `SKXamlCanvas` is not used. | Sprites +1.2 MB private, 1.3 ms rebuild. Canvas +33 MB (budget 2 MB). | card chrome |
| S2 animated border | Go, as a **compositor sprite per card with a stepped rotation at 15 steps per second**, a host constant. Smooth and 30 per second are out; cached ring bitmaps were not built. | 3.0 to 3.2 % of a core, +3 MB for 8 cards (budget 5 % and 8 MB). Smooth: 20 %. Repaint baseline: 11 % and +26 MB. | motion |
| S3 fonts | Go. All ten families load through the existing resolver; none needs a fallback or a static instance. | Each family costs less than Inter does today; no growth over 660 theme switches. | typography |

What the later designs have to take from these, in one place:

- **Card chrome:** the underlay is sprites under the widgets in `PageHost`; a snapshot draws the same bleed with Skia, so both
  share one routine. A translucent card shows its neighbours' shadow when the reach exceeds the gap: a validator warning, not
  an error (Prism's draft does it). A card must be the same in every corner region to be a nine-grid (so a dashed border does
  not stretch). The dock needs an underlay of its own. `pattern` cannot be a nine-grid: it draws on a page-sized surface and the
  memory cost (about +33 MB) is accepted for the three themes that use it.
- **Motion:** `borderAnimate: spin` is stepped; `motion.level`, the reduce-motion setting, display off and a hidden deck must each
  stop it (not prototyped). The value-fill animation was not tested and is deferred
  (`docs/design/theme-format-2-deferred-keys.md`). The step visibility was not judged by eye.
- **Typography:** the resolver must drive `opsz` from the role's size (Bodoni Moda stays at its text cut otherwise); a weight
  outside a variable font's range, or any weight on a static file, is silent and needs a validator finding; Pop takes its
  numerals from Archivo at weight 900 (Archivo Black duplicates it within 1 %); Bodoni Moda, Jost and Barlow Condensed have no
  arrows.
- **Separate finding:** the unchanged host holds about 54 MB more after 20 theme switches (S1, "Seen on the way"). It predates the
  spikes and is a backlog item of its own.

## S1 page underlay

Measured 2026-10-13 on branch `spike/theme-look-s1`, on the owner's panel (HYTE Y70 Touch, 1100x3840 at 100 %), Release build,
copied to a scratch folder. The prototype is `src/UrDeck.Host/PageUnderlay.cs`, switched on with `URDECK_SPIKE_UNDERLAY`
(`blur,hard,marks,pattern`) and `URDECK_SPIKE_UNDERLAY_KIND` (`canvas` or `sprites`); with the variable unset the build is
the unchanged host, which is the comparison in every table. The scripts, the configuration, the test theme and two captures
are in `spikes/theme-look-s1`.

The page: 8 cards on 4x8 cells (Clock 4x2, two Stats 2x2, two Shortcuts 1x1, Clock 2x1, Weather 4x2, Clock 4x1), a second page
of 2 cards to swipe to, one Shortcut in the dock. The theme is `default-light`'s colours with `card.gap` 0.11 and
`card.radius` 0.1, so the translucent cards show what is under them. Drawn per card: a blurred shadow (sigma 0.04 cell,
0.03 cell down), a hard shadow (0.055 cell right and down) and bracket marks at the four corners 0.03 cell outside the
card; the furthest reach is 43 px.

**Decision: option B, go, but not as one page-sized Skia surface.** A full-page `SKXamlCanvas` works in every respect and
costs 33 MB, sixteen times the budget. The same drawing as **one compositor sprite per card, all stretching one small Skia
picture** (a nine-grid with a hollow centre) costs about 1 MB, builds in 1.3 ms, and behaves the same under the slide, the
press and input. Option A is not needed. `pattern` does not fit the sprites and needs its own decision (below).

### The two forms of option B

| | Canvas (as the handoff describes it) | Sprites |
|---|---|---|
| What it is | one `SKXamlCanvas` the size of the page, first child of `PageHost`, `IsHitTestVisible = false`; Skia draws every card's bleed with the cards clipped away | an empty `Canvas`, first child of `PageHost`, holding one `SpriteVisual` per card (`ElementCompositionPreview.SetElementChildVisual`); Skia draws the bleed of one small card once (146x146 px here), the picture becomes a `LoadedImageSurface`, and a `CompositionNineGridBrush` stretches it around each card |
| Surface | 1100x3840 px | 146x146 px, shared by all cards of the page |

### Memory

Process sampled every second for 30 s after 15 s of warm-up, the median; three runs each, interleaved (off, canvas, sprites,
three times). GPU memory is the `GPU Process Memory` counters of the process. The range is over the three runs.

| Case | Working set | Private bytes | GPU dedicated | GPU shared |
|---|---|---|---|---|
| No underlay | 180.4 to 180.9 MB | 130.2 to 130.8 MB | 124.9 MB | 11.4 MB |
| Canvas, blur + hard + marks | 211.5 to 213.6 MB | 162.5 to 163.9 MB | 159.0 MB | 27.9 MB |
| Sprites, blur + hard + marks | 181.8 to 185.5 MB | 131.6 to 135.2 MB | 125.0 MB | 11.9 to 14.1 MB |
| Canvas with `pattern` too (one run) | 212.7 MB | 162.8 MB | 159.0 MB | 27.9 MB |
| Canvas, blur only, rendered at half size (one run) | 188.7 MB | 139.5 MB | 124.9 MB | 15.7 MB |

- **Canvas: +32 MB working set, +33 MB private, +34 MB GPU dedicated, +16.5 MB GPU shared.** The budget is 2 MB. That is
  about 8 bytes per pixel in the process and 8 more on the GPU: the 16.9 MB of pixels are held twice on each side. What is
  drawn does not matter (the pattern adds nothing); the surface does. During a swipe two pages are alive, so twice that.
- **Sprites: +1.2 MB private at the median** (+0.8, +1.2 and +4.4 MB in the three runs; the working set +3.4 MB at the
  median), +0.1 MB GPU dedicated. Inside the budget at the median; the one run at +4.4 MB is not explained.
- Half size helps less than its quarter of the pixels suggests (+9 MB private) and only a blurred shadow survives it: hard
  shadows and marks need full resolution. Not worth pursuing.
- CPU at rest is the same in all cases (0.2 to 0.5 % of one core, the clocks ticking): neither form is repainted.

### Rebuild cost

| | First build in a process | Later builds |
|---|---|---|
| Canvas, 8 cards, 1100x3840 | 7.3 to 7.6 ms (four runs); 10.3 ms with `pattern` | 2.1 to 2.3 ms |
| Sprites, 8 cards | 21.7 to 34.3 ms on the UI thread, then 68 ms until the picture is decoded (not on the UI thread) | 1.3 to 1.4 ms, then 4.5 to 6 ms until decoded |

- Both are under the 20 ms budget once the process is warm. The sprites' first build is at or over it once per process (the
  first Skia blur, the first PNG encode, the first `LoadedImageSurface`); it happens in the page build at startup.
- **Redrawn on layout or theme change only: confirmed.** Each 45 s run logged exactly one paint (canvas) or one build
  (sprites) while three clocks ticked every second and the readings changed; a swipe builds the neighbour's underlay once
  (2 cards: 2.1 ms canvas, 1.4 ms sprites); 20 theme switches gave 21 builds.
- The sprites' picture arrives a frame or two after the cards (`LoadedImageSurface` decodes asynchronously): the shadow can
  appear just after its card. Not visible in the captures; the real thing should keep the picture per theme and size, not
  per page, which removes it for every page after the first.

### Slide, press, input, z-order

Input was injected with the mouse (`spikes/theme-look-s1/input.ps1`): a tap on a Shortcut, a press held 700 ms on the
other, a drag held at 400 px and then released past the middle, and a drag back. One capture of the held press and one of
the held drag per form. Both forms gave the same result.

| Check | Result |
|---|---|
| Z-order | The underlay is under every card; nothing of it covers a card (first child of the page's canvas). |
| Tap through it | `Spike input: tap ... reached a widget (can tap: True)`. It could not be otherwise: the pager takes pointer events on the root grid and finds the widget from the layout, not from the XAML element under the pointer. |
| Press | The card scales to 0.96 and dims as before; its shadow and marks stay at full size, so a thin band of shadowed background shows around the pressed card (`press-canvas.png`). No artefact. |
| Slide | The underlay is inside the page's own visual, so it moves with the page by construction: in the capture at 400 px both pages have their shadows and marks in place (`drag-sprites.png`). The swipe committed and came back (`Page 'Second' (2/2) shown`, `Page 'Eight' (1/2) shown`). |
| Tearing or lag in motion | **Not judged.** A still capture cannot show it and nobody watched the panel. The 250 ms settle is one compositor animation of the page's visual, which carries the underlay, so there is no mechanism for the two to separate; it is worth one look by eye. |

### What the design has to take from this

1. **Use the sprites.** One picture per (theme, cell size, card style), one sprite per card, under the widgets in `PageHost`.
   In `PageRenderer` (snapshots) the same Skia routine draws each card's bleed directly, so both paths share the drawing code.
2. **A translucent card shows what is under it.** The cards must be cut out of the shadow, or `glass` and `default-light`
   show their own shadow through the fill. Both forms cut out the card's own shape. Only the canvas can also cut out the
   *other* cards: with sprites, a shadow that reaches further than the gap lies under the neighbour and shows through a
   translucent fill (here 43 px of reach against a 30 px gap; it is faint, and invisible under an opaque fill). The validator
   rule should be: bleed of at most the gap, unless the card fill is opaque.
3. **A nine-grid needs the card to be the same in every corner region.** Radius, shadow, glow and corner marks qualify. A
   chamfer on two corners qualifies too (it is in the corner pieces). Anything that varies along an edge does not.
4. **`pattern` is not a per-card thing** and cannot be a nine-grid. It does not have to slide with the page either: draw it
   in the background layer, which already has a full-size surface when a background is set. Where no background is set, a
   pattern would bring that surface (and the 33 MB measured here) with it; a small tile in a tiling brush would not, but
   the lifted compositor has no tiling surface brush without Win2D. This was not prototyped.
5. **The dock** is a `PageHost` in the overlay and got no underlay in the spike. It needs one of its own, resolved for the
   slot size as its theme is.
6. **The press** could take the shadow along: the sprite is a visual, so the same scale animation can run on it. Left as
   it is in the spike; it looks fine.
7. The sprites were run at 100 % scaling only. The inset scales (`SetInsetScales(1 / scale)`) are there for other scalings
   but were not seen at one.

### If option B had failed: option A

Not measured, computed from the canvas's cost per pixel (7.8 bytes private, 8.1 bytes GPU dedicated). Larger widget surfaces
on this page with a bleed of 43 px (0.156 cell) add 0.77 megapixels, 18 % of the page: about +6 MB private and +6 MB on the
GPU, and every card repaint rasterises and uploads the larger surface. At the fallback's cap of 0.25 cell it is 1.3
megapixels, about +10 MB each. The sprites are cheaper than option A at any bleed, so option A is closed.

### Seen on the way: memory after theme switches

The S3 notes asked for one look at a theme switch in the running host. 20 switches between `default-dark` and the test
theme, 2.5 s apart, by saving the configuration:

| | Start | After 20 switches | After a swipe (the pager then forces a collection) |
|---|---|---|---|
| No underlay | 131.0 MB private | 190.7 MB (229.7 MB after 10) | 184.9 MB |
| Sprites | 131.9 MB private | 231.3 MB | 184.5 MB |

- The sprites add nothing: after a collection both cases are level.
- **The unchanged host holds about 54 MB more after 20 theme switches, also after a collection**, and far more until one
  runs: a rebuild releases the page but, unlike a slide, does not arm the pager's reclaim timer. No warning or error in the
  log. Whether it keeps growing or settles was not established (one series of 20); it predates the spike and deserves its
  own item. The first switch alone adds about 21 MB.

## S2 animated border

Measured 2026-10-13 on branch `spike/theme-look-s2` (from S1), same panel (60 Hz; the primary display runs at 180 Hz), same
method, Release build in a scratch folder. The prototype is `src/UrDeck.Host/BorderSpike.cs`, switched on with
`URDECK_SPIKE_BORDER` (`compositor`, `static`, `repaint`) and `URDECK_SPIKE_BORDER_FPS`; unset, it is the unchanged host. The
script, configuration, theme, raw lines and one capture are in `spikes/theme-look-s2`.

The page is S1's 8 cards without the dock, in Rigline's colours (`card.radius` 0.145, `card.gap` 0.09). The border is
Rigline's: a conic gradient of six colours, 0.014 cell wide (4 px), one turn in 7 s. Each case ran 60 s after 15 s of
warm-up. CPU is the process's processor time as a share of one core (the machine has 32 logical processors); the desktop
compositor (`dwm.exe`) is listed beside it because the cost of presenting a changed window lands there.

**Decision: go with the first approach, a compositor sprite per card, stepped at 15 steps per second.** 3.0 to 3.2 % of a
core and +3 MB, with the card painted once. At 30 steps per second it is at the edge of the budget (4.4, 4.5 and 6.4 %).
A *smooth* compositor animation is not usable (20 % of a core). The cached ring bitmaps were not built: the first
approach meets the budget, and they cannot meet the memory budget by arithmetic (below).

### How the border is made

Over each card, as the child visual of its `WidgetView` (so the press scales it with the card): a `SpriteVisual` painted
with a `CompositionMaskBrush`. The source is one 512x512 Skia picture of the conic gradient, shared by all cards, scaled so
that the turning square always covers the card; the mask is a 92x92 Skia picture of the stroked ring, stretched around the
card as a nine-grid. A key-frame animation turns the *source brush* (`RotationAngleInDegrees`, 0 to 360, forever); with a
step easing function of `period x rate` steps it only changes that many times per second. No Skia paint, no frame clock,
no UI-thread work per frame.

### Numbers

| Case | CPU, share of one core | Working set | Private bytes | GPU dedicated | GPU, process | `dwm.exe` CPU |
|---|---|---|---|---|---|---|
| No border (two runs) | 0.34, 0.44 % | 179.0, 178.6 MB | 129.7, 129.6 MB | 124.9 MB | 0.2 % | 0.2 to 0.3 % |
| Sprite, not animated (the static border) | 0.49 % | 183.4 MB | 132.0 MB | 125.0 MB | 0.2 % | 0.3 % |
| Sprite, smooth (two runs) | **20.0, 19.5 %** | 184.5, 184.2 MB | 132.6, 133.1 MB | 141.5 MB | 4.1 % | 5.1, 4.7 % |
| Sprite, 30 steps per second (three runs) | 4.5, 4.4, **6.4 %** | 184.1, 185.9, 183.0 MB | 131.7, 133.1, 131.7 MB | 125.0 MB | 0.9 to 1.3 % | 2.3, 3.8, 3.8 % |
| **Sprite, 15 steps per second (two runs)** | **3.2, 3.0 %** | 183.9, 185.5 MB | 132.2, 133.0 MB | 125.0 MB | 0.6 to 0.9 % | 1.4, 3.6 % |
| Baseline: every card repainted by the frame clock | 11.3 % at 21.4 paints per second | 205.6 MB | 155.3 MB | 176.7 MB | 10.6 % | 1.8 % |
| Baseline asked for 15 per second | 4.7 % at 10.8 paints per second | 205.6 MB | 154.3 MB | 195.7 MB | 5.7 % | 1.0 % |

Against the budget (at most 5 % of a core and +8 MB, 8 cards):

- **15 steps per second: +2.7 % of a core, +2.5 to +3.4 MB private, +5 to +7 MB working set, +0.1 MB GPU.** Inside.
- **30 steps per second: +4.1 to +6.1 % of a core**, the same memory. Two runs inside, one outside; too close to promise.
- **Smooth: +19.5 % of a core and +16.6 MB of GPU memory.** Far outside. The lifted compositor renders every frame of an
  animation in the process; the rate it does that at was not measured (the primary display's 180 Hz would fit the number,
  which is a guess).
- The sprite alone, not animated, costs +2.3 MB private and no CPU: that is the price of a static gradient border made this
  way, and most of the animated one's memory.
- **The baseline shows why the card must not repaint:** +11 % of a core at only 21 paints per second (`FrameClock` does not
  reach 30, as `docs/perf/backgrounds.md` says), which scales to about 16 % at a true 30, and +26 MB private and +52 MB of
  GPU memory, both from surfaces that are re-uploaded continuously. Slowing it to 11 paints per second brings the CPU
  under 5 % but not the memory.
- `dwm.exe` pays as well, 1 to 4 % of a core while the border moves, against 0.3 % at rest. It is outside the process and
  outside the budget as written, and it varied between runs with whatever else was on the desktop.

### Why the cached ring bitmaps were not built

The handoff says to stop at the first approach that meets the budget. Beyond that, a ring bitmap is specific to a card size,
and this page has five sizes. As whole-card bitmaps, 72 steps of them are 368 MB. Stored as the border's pixels alone (four
corner squares and four 4 px strips), a 4x2 card is about 48 KB per step, 3.4 MB for 72 steps, and the page about 15 MB:
over the +8 MB before anything is drawn. Blitting them also needs a surface per card that is uploaded every frame, which
is the baseline's memory cost again.

### What the motion design has to take from this

1. `borderAnimate: spin` is a compositor sprite with a **stepped** animation; the step rate is a host constant (15 per
   second), not a theme key. At 7 s per turn a step is 3.4 degrees. Whether the steps are visible was **not judged by
   eye**: one still capture was taken (`border-compositor.png`), nobody watched the panel.
2. **The animation does not stop by itself.** `motion.level`, `reduceMotion`, display off and a hidden deck must each stop
   it (`StopAnimation`), as the background layer does for its frames. Not prototyped and not measured.
3. The border is then drawn by the compositor, not by `WidgetPainter`, so a snapshot has to draw the same ring with Skia
   at a fixed angle (`BorderSpike.DrawRing` is that routine) and the two must be kept alike. A static gradient border can
   stay in `WidgetPainter` and costs nothing extra.
4. The ring is a nine-grid, so the limit from S1 holds: the same shape in every corner region. A dashed border does not
   stretch; a chamfered one does.
5. `pulse` (alpha between 0.55 and 1) is the same sprite with an opacity animation; not measured, and it should be stepped
   for the same reason.
6. **Not done:** the value fill animation (`typography.valueFill.animate`), the second test "if time allows". It is text
   inside the card's own surface, so the sprite trick does not apply directly; it needs its own spike or a static fill.


## S3 fonts

Measured 2026-10-13 on branch `spike/theme-look-s3`, Windows 11, Release build, SkiaSharp 4.153.1, software raster, no panel.
The harness is `spikes/theme-look-s3` (`Get-Fonts.ps1` downloads the files from the `google/fonts` repository; `Program.cs`
has the modes `verify`, `mem`, `switch` and `pages`). Every font goes through the unchanged `ThemeStore` and `ThemeResolver`: a
user theme folder per family holding `theme.json` (`typography.font`) and the font file. The specimen is
`spikes/theme-look-s3/specimen.png` (each family at the requested weights 300, 500 and 800).

**Decision: go.** All ten families load through the existing resolver with no fallback and no warning, the `wght` axis works
on all six variable files, and a family costs no more memory than Inter does today. No font needs a fallback or a static
instance in place of its variable file. Four things have to go into the typography design (below): the weight ranges are
narrower than 300 to 900, the optical size axis is not driven, a static file ignores weights silently, and three families
have no arrows.

### What was verified

For each family and each weight from 300 to 900, a theme with all five role weights set to that weight was resolved, and the
typeface was checked for: its family name (not the fallback), the variation position it reports, and the ink (sum of coverage)
and advance of `0123456789` at 200 px, which must grow with each weight step. The glyphs checked are `° % : · + - ↑ ↓`, U+2212
and the digits. The licence is the `OFL.txt` next to the font in `google/fonts` (all ten are SIL OFL 1.1) and name id 13 in the
font, which says the same in every file.

| Family | File | Form | Axes (default) | Weights that differ | Missing glyphs | Licence file | Digits |
|---|---|---|---|---|---|---|---|
| Inter (today) | `InterVariable.ttf` | variable | opsz 14..32 (14), wght 100..900 | 300 to 900 | none | yes | proportional |
| Instrument Sans | `InstrumentSans-Variable.ttf` | variable | wdth 75..100 (100), wght 400..700 | **400 to 700** (300 gives 400; 800, 900 give 700) | none | yes | proportional |
| JetBrains Mono | `JetBrainsMono-Variable.ttf` | variable | wght 100..800 | **300 to 800** (900 gives 800) | none | yes | tabular |
| Bricolage Grotesque | `BricolageGrotesque-Variable.ttf` | variable | opsz 12..96 (**96**), wdth 75..100 (100), wght 200..800 | **300 to 800** (900 gives 800) | none | yes | proportional |
| Archivo | `Archivo-Variable.ttf` | variable | wdth 62..125 (100), wght 100..900 | 300 to 900 | none | yes | proportional |
| Bodoni Moda | `BodoniModa-Variable.ttf` | variable | opsz 6..96 (**11**), wght 400..900 | **400 to 900** (300 gives 400) | ↑ ↓ | yes | proportional |
| Jost | `Jost-Variable.ttf` | variable | wght 100..900 | 300 to 900 | ↑ ↓ | yes | proportional |
| Chakra Petch | `ChakraPetch-Bold.ttf` | static | none | one (700) | none | yes | proportional |
| Barlow Condensed | `BarlowCondensed-Bold.ttf` | static | none | one (700) | ↑ ↓ | yes | proportional |
| IBM Plex Mono | `IBMPlexMono-Regular.ttf` (and `-Medium`) | static | none | one per file (400, 500) | none | yes (Reserved Font Name "Plex") | tabular |
| Archivo Black | `ArchivoBlack-Regular.ttf` | static | none | one (900) | none | yes | tabular |

- Within its range every variable file answers each 100 step with more ink, and the typeface reports the requested position
  (`wght=500` and so on). The resolver clamps a weight outside the range without a word.
- The draft themes ask only for weights their variable fonts have (Prism's 300 numerals are inside Bricolage's 200..800).
  Three draft weights do nothing because the file is static: `body` 500 in Rigline and Ember (the Bold file) and `title` 500
  in Blueprint (the Regular file; it needs `IBMPlexMono-Medium.ttf` and a per-role font to be real).
- DirectWrite names two of the static files by their typographic family (`Barlow`, `Archivo`). They load by file name, so
  this matters only to a check that compares family names.
- Nothing in `sdk/`, `widgets/` or `providers/` draws `↑` or `↓` today. If readings get arrows, Bodoni Moda, Jost and Barlow
  Condensed draw a missing-glyph box: the text components have no per-character fallback.
- Only the two monos and Archivo Black have equal-width digits; the others rely on `Readout` equalising them, as Inter does.

### Findings for the typography design

1. **Optical size is not driven.** `ApplyWeight` clones with the `wght` coordinate only, so the other axes stay at the file's
   default, and DirectWrite does not pick an optical size from the text size. Bodoni Moda stays at opsz 11, the text cut, also
   for the large values Obsidian uses it for: at 200 px and weight 500 the digits have 50 451 px of ink against 45 621 px at
   opsz 96, and the hairlines that make it a Didone are visibly thicker (last two lines of the specimen). Bricolage's default is
   the other end, opsz 96, so its labels get the display cut. The per-role resolver should set `opsz` from the role's size, or
   the theme should name it; without that, Obsidian's values will not match the mock.
2. **Out-of-range and static weights are silent.** A validator finding (or one warning) for a weight outside the file's `wght`
   range, and for a weight that differs from a static file's own, would have caught the three draft weights above.
3. **Archivo Black duplicates Archivo at weight 900**: the digits' advance is identical (1334.0 px at 200 px) and the ink is
   within 1 % (103 159 against 104 110). Pop could take its values from `Archivo-Variable.ttf` at 900 and drop one family. The
   one difference: Archivo Black's digits are tabular, Archivo's are not.
4. **The font file is read once per resolve.** The host resolves a theme twice (page and dock), so it holds the font data
   twice: about +0.9 MB private per family (table below). Sharing the base typeface between the two would save it; it is small
   enough to leave.

### Memory

One process per row, five runs, the median. The baseline is a process that has drawn text with the system font. "Theme" is one
`ThemeResolver.Resolve` with five distinct role weights (300, 400, 500, 700, 900), each drawn at 24, 48 and 220 px; "and dock"
adds the second resolve the host makes; "clones" is the task's literal case, the base typeface and one clone for each of the
seven weights (a static file: the base alone). The runs of a row differ by at most 0.5 MB in working set.

| Family | File | Theme: working set / private | And dock: working set / private | Clones: working set / private |
|---|---|---|---|---|
| Inter (today) | 0.84 MB | 14.5 / 7.0 MB | 16.6 / 9.2 MB | 14.8 / 7.4 MB |
| Instrument Sans | 0.19 MB | 12.0 / 5.2 MB | 12.8 / 6.0 MB | 12.3 / 5.6 MB |
| JetBrains Mono | 0.18 MB | 13.2 / 6.4 MB | 13.5 / 7.2 MB | 12.7 / 6.5 MB |
| Bricolage Grotesque | 0.39 MB | 13.6 / 6.9 MB | 14.5 / 7.8 MB | 13.2 / 6.9 MB |
| Archivo | 0.63 MB | 14.2 / 7.8 MB | 15.7 / 9.1 MB | 12.7 / 6.8 MB |
| Bodoni Moda | 0.15 MB | 12.7 / 5.9 MB | 13.3 / 6.9 MB | 12.8 / 6.2 MB |
| Jost | 0.13 MB | 13.0 / 6.3 MB | 13.9 / 7.3 MB | 12.4 / 5.9 MB |
| Chakra Petch | 0.07 MB | 9.2 / 2.5 MB | 10.1 / 3.4 MB | 8.7 / 2.2 MB |
| Barlow Condensed | 0.10 MB | 8.9 / 2.0 MB | 9.9 / 2.9 MB | 8.8 / 1.9 MB |
| IBM Plex Mono | 0.13 MB | 9.1 / 2.4 MB | 10.2 / 3.3 MB | 9.0 / 2.4 MB |
| Archivo Black | 0.09 MB | 9.2 / 2.5 MB | 10.0 / 3.2 MB | 9.0 / 2.2 MB |
| All eleven files at once | 2.19 MB | 22.0 / 14.8 MB | 30.1 / 23.2 MB | 23.9 / 16.8 MB |

- Most of a single family's number is paid once per process, not per family: the first font from a file costs about 9 MB of
  working set whatever its size (the static rows), and a variable font about 4 MB more. All eleven files together cost 22 MB,
  not eleven times 12. From the last row, a further family in the same theme is about 1 MB private (an estimate, not measured
  pair by pair).
- **No regression against today**: every new family costs less than Inter, which every v1 theme already loads. A theme with
  its own font does not load Inter as well, so a one-family theme is level with today or below it, and a two-family theme
  (Carbon, Paper, Pop, Obsidian) about 1 MB above.
- The weight clones are nearly free: seven clones cost the same as the theme's five within the noise.

### Theme switches

A "switch" loads a theme, resolves it for the page and for the dock, draws with every role and disposes both, as
`MainWindow.ApplyLayout` does (views released, then `DisposeThemes`). Eleven themes (the ten families and Inter) per round;
memory read after a forced collection.

| Case | Start | After 220 switches | After more |
|---|---|---|---|
| Engine only, themes disposed | 24.0 MB private | 28.2 MB | 22.5 MB after 1100; between 22 and 28 MB throughout |
| Engine only, themes kept (the control: what a leak looks like) | 37.5 MB private | 379.1 MB | |
| Real widgets (Clock, Stats, Weather, a Shortcut in the dock) from the collectible plugin contexts, `PageRenderer` at 1100x3840 | 32.9 MB private | 66.1 MB | 57.0 MB after 660; between 54 and 66 MB from the fifth round on |
| The same with one font every time (the control: no font changes) | 47.3 MB private | 128.5 MB | 112.1 MB after 660; between 104 and 129 MB |

- Typefaces are released on a switch: nothing grows after the first rounds, over 660 switches with the real widgets and 1100
  without them, while the same loop without `Dispose` grows by 1.6 MB per switch.
- The rise over the first five rounds with widgets is not the fonts: the run that never changes its font rises more. It is the
  17 MB page bitmap of each render and the glyph cache settling.
- Not measured: the live host on the panel. S3 needs no panel; the order in `ApplyLayout` (`_pager.Release()` before
  `DisposeThemes()`) was read, not run. A switch between two themes in the running host is worth one look during S1.

## Value glow and fill (theme-typography)

Measured 2026-10-15 in the engine tests' process, Windows 11, Release build, SkiaSharp 4.153.1, software raster, no panel. One
`Readout` of `41` with a unit and a label in a 1100 x 550 rectangle (the value about 400 px high), drawn 500 times after 50 to
warm up, the canvas cleared before each draw; "a new value" draws 100 different values in a row, so every draw blurs. Each row
is the median of three runs, which differed by under 5 %.

| Theme style | Same value (ms per draw) | A new value each draw (ms) |
|---|---|---|
| none (a v1 theme) | 0.105 | 0.15 |
| `valueGlow` blur 0.16, alpha 0.55 | 0.29 | 2.1 |
| Ember-like: `slant` 8 and that glow | 0.30 | 2.3 |
| Rigline-like: a three-colour `valueFill` and a glow (blur 0.1) | 0.74 | 3.7 |

- **Over the package's budget of 2x, at an absolute cost that does not matter.** A glowing readout that repaints with the same
  value costs 0.19 ms more than a plain one (2.8x); the blurred copy is cached on the theme (16 entries), so the blur itself
  (about 2 ms at this size) is paid once per new value: once a minute for a clock, once a second for a stats tile.
- **How the copy is drawn decides the cost.** The first version, an RGBA copy drawn with bilinear sampling through the slant's
  skew, cost 3.2 ms per draw (30x). Keeping the copy in the platform's colour type (BGRA on Windows, so no swizzle per draw),
  drawing it with nearest sampling (it is blurred, so this is invisible) and baking the slant into the copy, so the draw is a
  plain translation, brought it to 0.29 ms. A copy kept as coverage only (A8) and tinted when drawn was slower (1.2 ms) and was
  not kept.
- The fill's extra cost (Rigline, 0.45 ms over the glow) is the gradient shader over the glyphs, paid on every draw; it is not
  cached. Worth a look only if a filled clock shows up in a CPU profile.

### Fonts on the panel

The host on the HYTE Y70 (1100 x 3840, the sample configuration: clock, stats, weather, one shortcut in the dock), private
memory 25 s after start, median of three runs, themes on `default-dark`'s colours: `default-dark` (Inter) 134.5 MB; Obsidian's
typography with Jost alone 131.9 MB; with Jost and Bodoni Moda per role and `opticalSize: auto` 134.2 MB. A second family is
about 2.3 MB: S3's 1 MB estimate per resolve, paid twice because the page and the dock each resolve the theme.

## Gauge styles (theme-gauges)

One repaint of a gauge at a 275 px cell (a 205 px square, a label, a changing fraction), mean of 300 draws after 20 warm-up
draws on the development machine, Release build, software canvas. The v1 ring is 0.051 ms.

| Theme (style) | ms | x the ring | Glow |
|---|---|---|---|
| Ember (tach) | 0.21 | 4.3 | yes (fill and redline) |
| Rigline (ring, gradient) | 0.25 | 5.1 | yes |
| Prism (ring, gradient) | 0.18 | 3.5 | yes |
| Blueprint (marker, ticks with numbers) | 0.09 | 1.7 | no |
| Obsidian (marker) | 0.05 | 1.0 | no |
| Paper (meter), Carbon (segmented bar), Pop (thick bar) | 0.03 to 0.04 | 0.7 to 0.8 | no |

- Without a glow every style is inside the budget (2x). With a glow three draft themes are over the 3x budget (3.5x to 5.1x);
  the cost is the blurred stroke, drawn on every repaint. A gauge repaints only when its rounded value changes or while its
  300 ms ease runs, so the worst case is about 0.25 ms a few times a second per gauge. Caching the blur per fraction would not
  help during an ease, because the fraction changes each frame; it is not done. Needs the owner's acceptance, as for Prism's chrome.
- The shaders (sweep, linear) and the blur filters are cached on the theme per size and disposed with it: five repaints of a ring
  and a bar with a gradient and a glow build three objects (`TheShaders_AreBuiltOncePerSize_AndDisposedWithTheTheme`).
