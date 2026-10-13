import { defineConfig } from 'astro/config';

// Served at the root of https://urdeck.app (GitHub Pages, custom domain), so no `base`.
export default defineConfig({
  site: 'https://urdeck.app',
  trailingSlash: 'always',
  build: { format: 'directory' },
  devToolbar: { enabled: false },
});
