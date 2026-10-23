import { defineConfig } from 'astro/config';
import mdx from '@astrojs/mdx';
import { resetRecords } from './src/lib/reference.mjs';

// What the reference components render is recorded for scripts/check-output.mjs; a build starts with none.
const references = { name: 'urdeck-reference', hooks: { 'astro:build:start': () => resetRecords() } };

// Served at the root of https://urdeck.app (GitHub Pages, custom domain), so no `base`.
export default defineConfig({
  site: 'https://urdeck.app',
  trailingSlash: 'always',
  build: { format: 'directory' },
  devToolbar: { enabled: false },
  integrations: [mdx(), references],
});
