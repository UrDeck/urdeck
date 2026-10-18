## 1. Groundwork

- [x] 1.1 `card-chrome-back` and `card-chrome-front` are merged and archived; the goldens pass on the branch before any change
- [x] 1.2 Read the S1 prototype (`src/UrDeck.Host/PageUnderlay.cs` on `spike/theme-look-s1`) and `Chrome.Underlay` in `spikes/card-chrome` on `spike/card-chrome-paint`

## 2. Engine

- [x] 2.1 The resolved style gains shadow and glow in pixels, the bleed, the corner zone, and the reach limit for translucent fills with one information line per loaded theme
- [x] 2.2 The underlay routine: shadow (hard or blurred, offset, spread), glow (outer blur, colour defaulting to the border colour), the card's shape cut out, the outside part of the marks with the card's rectangle clipped away (`DrawMarks`)
- [x] 2.3 Picture making: nine-grid insets or exact size, half size for blurred-only pictures, kept per (style, size class)
- [x] 2.4 `PageRenderer`: the underlay pass before the cards; the dock's clipped to its band
- [x] 2.5 Tests: the reach limit (Prism's numbers, opaque kept, a translucent tint counts); bleed and zone per draft against `docs/perf/card-chrome.md`; a hard shadow's pixels beside a card and none under its content; a translucent card's own shadow cut out; a mark across the edge drawn unbroken in a snapshot; nothing made for a v1 theme

## 3. Host

- [x] 3.1 An underlay element first in `PageHost` (not hit-testable), one sprite per card with the nine-grid or exact-size brush; nothing when the style draws nothing outside
- [x] 3.2 The press binding (expression on `Scale` and `Opacity`, the centre point); the fallback in `AnimatePress` if the expression does not run, recorded in the design
- [x] 3.3 The dock's underlay from the dock's style, clipped to the band
- [x] 3.4 Log one line per build (pictures made, their sizes, the time)

## 4. Verification

- [x] 4.1 v1 goldens unchanged; goldens with Paper, Pop, Prism and Rigline (page and dock) and Blueprint's marks, looked at by eye before they are committed
- [x] 4.2 On the panel, the method of S1: private memory and build time per page for Paper, Prism and Rigline against `default-dark` (budget 10 MB, changed from 2 MB in the apply; 20 ms warm); record in `docs/perf/card-chrome.md`
- [x] 4.3 On the panel with the owner: a held press on a shadowed card, a slide between two pages, Prism's limited shadow, a half-size picture's edge under a translucent card, and one look at 150 % scaling
- [x] 4.4 Idle check: a minute of ticking clocks builds the underlay once (log)
- [x] 4.5 `dotnet build urdeck.slnx -c Release` 0 warnings; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; the engine tests with the coverage floor; the Linux tests

## 5. Documentation

- [x] 5.1 `docs/themes.md`: shadow, glow, outside marks, the reach limit on translucent cards
- [x] 5.2 The site's theme page; `docs/design/look-and-feel/STATUS.md` (the row, and the "not verified" items this change verified)
