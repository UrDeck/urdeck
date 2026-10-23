## Context

See proposal.md for why. Depends on `site-media-pipeline`: `site/src/generated/catalog.json` (themes with swatch,
description, tags, default background and pairings; backgrounds; widgets with sizes) and `media.json` (per theme, widget,
size and background: picture, loop, settings), read through `site/src/lib/media.ts`.

The site today: Astro, static, no UI framework; `Base.astro` layout; `global.css` tokens (`--ink #080918`, `--accent
#8ab8ff`, `--accent-hover`, `--violet`, used for text, buttons and the aurora glows); `WidgetShowcase.astro` (a stage, a
marquee, a tour that stops on the first choice, reduced motion respected, everything visible without script);
`BackgroundShowcase.astro`; `check-output.mjs` (internal links, no third-party requests, schema copy).

## Goals / Non-Goals

**Goals:**
- One choice changes every product picture on the page, with no flash of glass and no reload.
- Nothing about the product's contents is typed into the site.
- Without script the site is today's site.

**Non-Goals:**
- Light mode or the theme's fonts on the site.
- Audience filters, search, a theme detail page.

## Decisions

### 1. Each product picture is a slot with a build-time map of variants

A component `ProductMedia` takes a slot (`deck`, `widget:<id>:<WxH>`, `background:<name>`), renders the glass variant as
today (`<Image>` with widths and sizes, or `<video>` with the glass poster), and adds `data-slot="<slot>"`. At build time
`getImage()` makes the optimised `src` and `srcset` for every theme's picture; the page emits one
`<script type="application/json" id="media-variants">` holding `{slot: {theme: {src, srcset, video?, poster?}}}`, so each
slot's variants are listed once even when it appears twice. A slot with no file in a theme has no entry for it and keeps
the glass one (spec, "A Choice Changes The Page").

*Alternative:* render all variants in the HTML and toggle `hidden`. Rejected: eleven times the DOM, and lazy loading does
not stop a hidden `<img>` with `loading=eager` (the hero) from downloading.

### 2. No flash: the theme is decided in `<head>`, pictures swap as they are parsed

An inline script at the top of `<head>` (in `Base.astro`) reads `?theme=` (and stores it), else local storage (in
`try`/`catch`), checks the name against the inlined list of theme names, and sets `document.documentElement.dataset.theme`.
Colours then follow by CSS alone (decision 3). For pictures, a small inline script directly after the hero's slot swaps
its `src`/`srcset` while the page is still being parsed, before the first paint; every other slot is swapped on
`DOMContentLoaded` (they are lazy and not yet fetched). On a choice the same routine runs over all slots.

### 3. Accents are generated CSS per theme

At build time, for each theme in the catalog, a script turns the swatch's accent into three values: `--accent` (the accent,
or, when it reads under 4.5:1 on `--ink`, the same hue with its OKLCH lightness raised in steps of 0.01 until it reaches
4.5:1), `--accent-hover` (lightness +0.08) and `--glow` (the accent at 35 % alpha, for the aurora gradients, which change
from the fixed `--violet` to it). They are emitted as `:root[data-theme="<name>"] { ... }` rules in a generated stylesheet
imported by `Base.astro`. Glass gets no rule (the site's own colours are glass's look). `--ink`, `--text` and fonts never
change.

### 4. The picker component

`ThemePicker.astro` in `Base.astro`: a `<div hidden>` that the head script un-hides (so it does not exist without script),
fixed bottom-right with `env(safe-area-inset-*)` margins and a high z-index. Closed: a button with the chosen swatch and
name (`aria-haspopup="listbox"`, `aria-expanded`, `aria-label="Theme: Ember"`); below 40 rem only the swatch dot. Open: a
`role="listbox"` of options (swatch, name, `aria-selected`), arrow keys and Home/End move, Enter or click chooses, Escape or
a click outside closes and returns focus. A choice dispatches `urdeck:theme` on `document`; the gallery cards dispatch the
same event, and the picker listens to stay in step.

### 5. Videos follow the choice

A swapped `<video>` keeps its state: if it was playing, the new `src` is set with the current time reset and `play()` is
called; if not, only `src` and `poster` change. With reduced motion no `play()` is ever called. The background gallery's
sixteen loops play only while visible (`IntersectionObserver`, threshold 0.25) and pause when they leave, so at most a few
decode at once.

### 6. Pages built from the catalog and one overrides file

`site/src/lib/catalog.ts` reads `catalog.json` and exposes themes, backgrounds, widgets and pairings in display order
(glass first, then by name). `site/src/data/site-copy.json` holds what the catalog does not: per theme an `alt` (default
"The sample deck in the {name} theme over the {background} background."), per widget a longer `description` and per size a
`note` and `alt` (defaults made from the widget's name, size and catalog description). `themes.json`, `widgets.json` and
`backgrounds.json` are deleted, their prose moved into `site-copy.json` where it is still true.

### 7. Sections

- **Hero:** the `deck` slot (loop when the media has one, else picture) in the device frame; the caption names the theme.
- **Theme gallery** (replaces "Sample decks"): one card per theme: deck thumbnail (the slot's glass variant is not used
  here; each card shows its own theme's deck), name, description, tags, swatch and up to three "pairs with" chips; the
  chosen theme's card is marked; a card is a button that dispatches the choice.
- **Widgets:** `WidgetShowcase` keeps its stage and marquee; its media become slots; the "glass theme" label shows the chosen
  theme's name.
- **Backgrounds:** sixteen figures with loop or poster, title, description and "pairs with" chips (themes); with a theme
  chosen, the figures it pairs with get a "pairs with <Theme>" badge and its default background moves first (by CSS
  `order`, no DOM move).

### 8. Build checks

`check-output.mjs` adds: every theme in `catalog.json` either has a deck in `media.json` or is listed as left out in the build
log (a warning, not a failure: a theme may land before its media refresh); the generated accent stylesheet has a rule for
every theme with media except glass; every slot in a page's variant map points to files in the output.

## Risks / Trade-offs

- [A page lists eleven variants per slot, the JSON grows with themes] → about 120 slots × 11 themes of short URLs, under 100 KB
  uncompressed on the landing page, compressed by Pages; measure in the build log.
- [Swapping the hero after the browser started fetching glass] → the inline script runs before the parser reaches the next
  element, normally before the request goes out; if not, one extra picture on first load only.
- [A tinted accent clashes with the site's fixed `--violet` art elsewhere] → every use of `--violet` in the site's CSS moves to
  `--glow`.

## Migration Plan

Site only. Merge after the first media refresh. Rollback is reverting the change.
