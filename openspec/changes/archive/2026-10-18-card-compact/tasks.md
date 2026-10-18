## 1. Groundwork

- [x] 1.1 `card-chrome-back`, `card-chrome-front` and `card-underlay` are merged and archived; the goldens pass on the branch before any change

## 2. Style

- [x] 2.1 The compact variant of the resolved style: radius, border width, shape (circle as half the smaller side), strip and marks merged key by key; a style without `card.compact` is its own variant
- [x] 2.2 The compact style (`ResolvedCardStyle.Compact`, `ForCard`) selected from the layout item (1x1) or the dock; the per-size cache and the underlay pictures key on the variant
- [x] 2.3 The dock's `Theme` takes the compact `CardRadius` and `CardBorderWidth`; the page's does not change
- [x] 2.4 Tests: each override alone and the merge of marks and strip (Blueprint's tick, Rigline's strip); circle on a square and on a non-square card with one information line; chamfer page with circle compact; a 1x1 and a 2x2 on one page; the dock theme's radius; a theme without `compact` makes no variant

## 3. Verification

- [x] 3.1 v1 goldens unchanged; goldens of the dock page and a page with 1x1 cards for Obsidian, Rigline, Blueprint and Prism, looked at by eye before they are committed
- [x] 3.2 On the panel with the owner: Obsidian's and Rigline's docks
- [x] 3.3 `dotnet build urdeck.slnx -c Release` 0 warnings; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; the engine tests with the coverage floor; the Linux tests

## 4. Documentation

- [x] 4.1 `docs/themes.md`: which cards are compact, what `compact` overrides and how its groups merge, circle
- [x] 4.2 The site's theme page; `docs/design/look-and-feel/STATUS.md` (the card chrome row done)
