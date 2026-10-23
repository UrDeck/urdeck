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

export type Media = { image?: ImageMetadata; video?: string };

/** The showcase page in a theme, over the theme's own background. */
export const deckMedia = (theme: string): Media => resolve(`deck:${theme}`);
/** The background a deck was drawn over, when the manifest names one. */
export const deckBackground = (theme: string): string | undefined => (entries.get(`deck:${theme}`) as (Entry & { background?: string | null }) | undefined)?.background ?? undefined;
/** A widget (`clock`) at a size (`4x2`) in a theme. */
export const widgetMedia = (widget: string, size: string, theme: string): Media => resolve(`widget:${widget}-${size}-${theme}`);
/** A background's poster and loop, in the colours of the theme it is shown with. */
export const backgroundMedia = (name: string) => {
  const m = resolve(`background:${name}`);
  return { poster: m.image, video: m.video };
};

// The social preview picture is optional: public/og-image.png (1200x630). A site build runs with site/ as the working directory.
export const hasSocialImage = existsSync(join(process.cwd(), 'public', 'og-image.png'));
