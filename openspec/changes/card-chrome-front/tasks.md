## 1. Groundwork

- [ ] 1.1 `card-chrome-back` is merged and archived; the goldens pass on the branch before any change

## 2. Style

- [ ] 2.1 The resolved style gains highlight, sheen, accent strip (with glow) and corner marks in pixels; thicknesses rounded as hairlines; the mark colour and strip colours default to the accent
- [ ] 2.2 Tests: the defaults (corners top-left and bottom-right, accent colour), hairline rounding of each thickness, strip colour by index round the list

## 3. Pieces

- [ ] 3.1 Sheen and accent strip (fade, glow) in the back pass and the back image; the cache key includes the strip colour
- [ ] 3.2 Highlight top and bottom in the front pass, clipped, its shaders made per size
- [ ] 3.3 `CardChrome.DrawMarks` (bracket, tick; inset to the outer edge) called by the front pass after the clip is restored
- [ ] 3.4 Tests: highlight pixels over an opaque content fill; the fade at both ends; sheen brightest at its centre; strip along each side with `length` centred; the fade's ends; marks over an opaque content fill; a mark with `inset` of minus half its thickness half on the card

## 4. Verification

- [ ] 4.1 v1 goldens unchanged; goldens of the sample page for Obsidian, Carbon, Paper (highlight only), Prism (without its shadow) and Rigline's page cards, looked at by eye before they are committed
- [ ] 4.2 Paint benchmark: Prism and Obsidian at most 1.5 times `default-dark`; record the rows in `docs/perf/card-chrome.md`
- [ ] 4.3 On the panel with the owner: Obsidian and Carbon
- [ ] 4.4 `dotnet build urdeck.slnx -c Release` 0 warnings; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; the engine tests with the coverage floor; the Linux tests

## 5. Documentation

- [ ] 5.1 `docs/themes.md` card section: highlight, sheen, accent strip, corner marks and what `inset` means
- [ ] 5.2 The site's theme page; `docs/design/look-and-feel/STATUS.md`
