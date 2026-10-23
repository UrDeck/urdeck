// What the product ships, read from src/generated/catalog.json (written by `npm run media`, from the engine's own catalogs).
// A theme, widget size or background appears here once it has media and disappears when it has none; the only words kept by
// hand are in src/data/site-copy.json, and an entry without any still gets text made from the catalog.
import catalog from '../generated/catalog.json';
import copy from '../data/site-copy.json';
import { deckBackground, deckMedia, widgetMedia, backgroundMedia } from './media';

export type Swatch = { background: string; card: string; accent: string; text: string };
export type Theme = {
  name: string;
  title: string;
  description: string;
  tags: string[];
  swatch: Swatch;
  defaultBackground: string | null;
  /** Names of the backgrounds the theme was designed for, among those the site shows. */
  pairsWith: string[];
  alt: string;
};
export type Background = { name: string; title: string; description: string; tags: string[]; pairsWith: Theme[] };
export type WidgetSize = { size: string; note: string; alt: string; width: number; height: number };
export type Widget = { id: string; name: string; typeId: string; description: string; sizes: WidgetSize[] };

/** The catalog writes colours with the alpha first (`#AARRGGBB`); CSS wants it last. */
const css = (color: string) => (color.length === 9 ? `#${color.slice(3)}${color.slice(1, 3)}` : color);

type SiteCopy = {
  themes: Record<string, { alt?: string } | undefined>;
  widgets: Record<string, { description?: string; sizes?: Record<string, { note?: string; alt?: string }> } | undefined>;
};
const siteCopy = copy as SiteCopy;

const withDeck = catalog.themes.filter((t) => deckMedia(t.name).image);
const backgroundTitle = (name: string) => catalog.backgrounds.find((b) => b.name === name)?.title ?? name;
const shownBackgrounds = (catalog.backgrounds as { name: string }[]).filter((b) => {
  const m = backgroundMedia(b.name);
  return m.poster || m.video;
});
const shownNames = new Set(shownBackgrounds.map((b) => b.name));

/** Glass first (the page without script), then by name. */
export const themes: Theme[] = [...withDeck]
  .sort((a, b) => (a.name === 'glass' ? -1 : b.name === 'glass' ? 1 : a.name.localeCompare(b.name)))
  .map((t) => {
    const over = deckBackground(t.name);
    return {
      name: t.name,
      title: t.displayName,
      description: t.description,
      tags: t.tags,
      swatch: { background: css(t.swatch.background), card: css(t.swatch.cardFill), accent: css(t.swatch.accent), text: css(t.swatch.text) },
      defaultBackground: t.defaultBackground ?? null,
      pairsWith: t.pairsWith.filter((n) => shownNames.has(n)),
      alt: siteCopy.themes[t.name]?.alt ?? `The sample deck in the ${t.displayName} theme${over ? ` over the ${backgroundTitle(over)} background` : ''}.`,
    };
  });

export const themeByName = (name: string) => themes.find((t) => t.name === name);

/** Every shown background, by name, with the themes that pair with it. */
export const backgrounds: Background[] = shownBackgrounds
  .map((b) => catalog.backgrounds.find((c) => c.name === b.name)!)
  .sort((a, b) => a.name.localeCompare(b.name))
  .map((b) => ({
    name: b.name,
    title: b.title,
    description: b.description,
    tags: b.tags,
    pairsWith: themes.filter((t) => t.pairsWith.includes(b.name)),
  }));

const widgetId = (typeId: string) => typeId.split('.').pop()!;

/** Widgets in catalog order; sizes largest first, those without a picture in the glass theme left out. */
export const widgets: Widget[] = catalog.widgets
  .map((w) => {
    const id = widgetId(w.typeId);
    const own = siteCopy.widgets[id];
    const sizes = w.sizes
      .filter((s) => s.preview && widgetMedia(id, `${s.width}x${s.height}`, 'glass').image)
      .sort((a, b) => b.width * b.height - a.width * a.height || b.width - a.width)
      .map((s) => {
        const size = `${s.width}x${s.height}`;
        return {
          size,
          width: s.width,
          height: s.height,
          note: own?.sizes?.[size]?.note ?? `${s.width} cells wide and ${s.height} tall.`,
          alt: own?.sizes?.[size]?.alt ?? `The ${w.name} widget at ${size}.`,
        };
      });
    return { id, name: w.name, typeId: w.typeId, description: own?.description ?? w.description, sizes };
  })
  .filter((w) => w.sizes.length > 0);
