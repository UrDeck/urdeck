// Media for the landing page is found by file name, so adding a picture or a video is dropping a file, not editing code.
//
//   src/assets/widgets/<widget>-<size>.(png|jpg|webp)      picture of a widget at a size (required for the entry to show)
//   public/media/widgets/<widget>-<size>.mp4               optional short loop; the picture is its poster
//   src/assets/backgrounds/<name>.(png|jpg|webp)           optional poster frame of a background
//   public/media/backgrounds/<name>.mp4                    the loop of a background (a background with none is left out)
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import type { ImageMetadata } from 'astro';

type Glob = Record<string, { default: ImageMetadata }>;

const widgetImages = import.meta.glob<{ default: ImageMetadata }>('/src/assets/widgets/*.{png,jpg,jpeg,webp}', { eager: true }) as Glob;
const backgroundImages = import.meta.glob<{ default: ImageMetadata }>('/src/assets/backgrounds/*.{png,jpg,jpeg,webp}', { eager: true }) as Glob;
const deckImages = import.meta.glob<{ default: ImageMetadata }>('/src/assets/decks/*.{png,jpg,jpeg,webp}', { eager: true }) as Glob;

function find(images: Glob, dir: string, base: string): ImageMetadata | undefined {
  for (const ext of ['png', 'jpg', 'jpeg', 'webp']) {
    const hit = images[`/src/assets/${dir}/${base}.${ext}`];
    if (hit) return hit.default;
  }
  return undefined;
}

// A site build runs with site/ as the working directory.
function video(kind: 'widgets' | 'backgrounds', base: string): string | undefined {
  return existsSync(join(process.cwd(), 'public', 'media', kind, `${base}.mp4`)) ? `/media/${kind}/${base}.mp4` : undefined;
}

export const widgetMedia = (base: string) => ({ image: find(widgetImages, 'widgets', base), video: video('widgets', base) });
export const backgroundMedia = (name: string) => ({ poster: find(backgroundImages, 'backgrounds', name), video: video('backgrounds', name) });
export const deckImage = (id: string) => find(deckImages, 'decks', id);

// The social preview picture is optional: public/og-image.png (1200x630).
export const hasSocialImage = existsSync(join(process.cwd(), 'public', 'og-image.png'));
