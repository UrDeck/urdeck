// The site's pictures and loops are generated (npm run media, site/README.md "Media"); what exists is listed in
// src/generated/media.json. Nothing here looks at a file name: an entry the manifest does not have is undefined, and the
// page leaves it out.
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import type { ImageMetadata } from 'astro';
import manifest from '../generated/media.json';

type Glob = Record<string, { default: ImageMetadata }>;
type Entry = { id: string; kind: string; files: string[] };

const images = import.meta.glob<{ default: ImageMetadata }>('/src/generated/**/*.webp', { eager: true }) as Glob;
const entries = new Map((manifest.entries as Entry[]).map((e) => [e.id, e]));

function resolve(id: string): { image?: ImageMetadata; video?: string } {
  const entry = entries.get(id);
  if (!entry) return {};
  const picture = entry.files.find((f) => f.startsWith('src/'));
  const loop = entry.files.find((f) => f.startsWith('public/'));
  return {
    image: picture ? images[`/${picture}`]?.default : undefined,
    video: loop ? `/${loop.slice('public/'.length)}` : undefined,
  };
}

/** The showcase page in a theme, over the theme's own background. */
export const deck = (theme: string) => resolve(`deck:${theme}`);
/** A widget at a size (`4x2`) in a theme. */
export const widget = (name: string, size: string, theme: string) => resolve(`widget:${name}-${size}-${theme}`);
/** A background's poster and loop, in the colours of the theme it is shown with. */
export const background = (name: string) => resolve(`background:${name}`);

// The landing page shows the glass theme.
const landing = 'glass';
export const deckImage = (theme: string) => deck(theme).image;
export const widgetMedia = (base: string) => {
  const i = base.lastIndexOf('-');
  return widget(base.slice(0, i), base.slice(i + 1), landing);
};
export const backgroundMedia = (name: string) => {
  const m = background(name);
  return { poster: m.image, video: m.video };
};

// The social preview picture is optional: public/og-image.png (1200x630). A site build runs with site/ as the working directory.
export const hasSocialImage = existsSync(join(process.cwd(), 'public', 'og-image.png'));
