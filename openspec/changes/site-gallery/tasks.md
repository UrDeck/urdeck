## 1. Groundwork

- [x] 1.1 `site-media-pipeline` is archived and its first generation is on `main`; on `feat/site-gallery` `npm run build` passes
- [x] 1.2 Read `index.astro`, `Base.astro`, `WidgetShowcase.astro`, `BackgroundShowcase.astro`, `global.css`, `home.css`, `media.ts`, `check-output.mjs`, and `catalog.json`/`media.json` as generated

## 2. Data

- [x] 2.1 `site/src/lib/catalog.ts` (design, decision 6); `site/src/data/site-copy.json` with the prose moved from `themes.json`, `widgets.json` and `backgrounds.json`; delete those three
- [x] 2.2 Accent generation (decision 3): OKLCH lift to 4.5:1 on `--ink`, hover and glow; the generated stylesheet; a `node --test` test for the contrast lift (Pop lifted, Paper and a passing accent unchanged)

## 3. Media slots and swapping

- [x] 3.1 `ProductMedia` component and the page's variant map (decision 1)
- [x] 3.2 Head script: query, storage with `try`/`catch`, name check, `data-theme` (decision 2); the hero's inline swap; the swap routine for all slots and for videos (decision 5)

## 4. Picker

- [x] 4.1 `ThemePicker.astro` in `Base.astro` (decision 4): hidden without script, fixed corner, phone size, listbox semantics, keyboard, Escape and outside click, focus return, `urdeck:theme` event

## 5. Sections

- [x] 5.1 Hero on the `deck` slot with the theme's name in the caption
- [x] 5.2 Theme gallery replacing "Sample decks" (decision 7), cards dispatch the choice, chosen card marked
- [x] 5.3 `WidgetShowcase` on slots, theme label follows the choice
- [x] 5.4 `BackgroundShowcase` with all sixteen, pairs-with chips, the chosen theme's badge and order, loops only while visible
- [x] 5.5 Replace `--violet` with `--glow` across the site's CSS

## 6. Checks and verification

- [x] 6.1 `check-output.mjs` additions (decision 8)
- [x] 6.2 `npm run build` passes; the third-party check finds no outside request
- [x] 6.3 By hand in a browser (Chrome and Firefox, desktop and phone width): every `site-theme-picker` scenario (choosing, `?theme=`, across pages, storage blocked via a private window with storage disabled, JavaScript off, reduced motion, keyboard only); no flash of glass on reload with a stored theme; record what was seen in the pull request
  (done with Playwright in Chrome and puppeteer in Firefox, not by a person at the keyboard: see the pull request)
- [x] 6.4 Owner's look at the landing page in three themes (stop and ask; do not tick without them)

## 7. Documentation

- [x] 7.1 `site/README.md` (catalog, `site-copy.json`, slots, the picker, the accent rule); `docs/design/look-and-feel/STATUS.md` (PR 17 row, the "Pairs with" badge done)
