// Checks of the built site (dist/): the schema copy, internal links, and third-party references.
// Run by `npm run build` after `astro build`; any problem fails the build.
import { existsSync, readdirSync, readFileSync, statSync, mkdirSync, copyFileSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { completenessProblems } from './completeness.mjs';
import { allPaths, readRecords, readSchema } from '../src/lib/reference.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const siteRoot = join(here, '..');
const dist = join(siteRoot, 'dist');
const schemasSource = join(here, '..', '..', 'docs', 'schemas');
const problems = [];

function walk(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

const urlPath = (file) => '/' + relative(dist, file).split(sep).join('/');

// 1. docs/schemas/** is served under /schemas/, byte for byte. It is copied here rather than kept in public/ so that the
// one file BackgroundPackageTests and urdeck-cli read is the one that is published.
for (const source of walk(schemasSource)) {
  const target = join(dist, 'schemas', relative(schemasSource, source));
  mkdirSync(dirname(target), { recursive: true });
  copyFileSync(source, target);
  if (!readFileSync(source).equals(readFileSync(target))) {
    problems.push(`schema copy differs from its source: ${urlPath(target)}`);
  }
}

// 2. Internal links and references must lead to a file of the output.
const files = walk(dist);
const pages = files.filter((f) => f.endsWith('.html'));
const attribute = /\b(href|src|poster|srcset|action)\s*=\s*(?:"([^"]*)"|'([^']*)')/gi;

function resolves(target) {
  const clean = target.split('#')[0].split('?')[0];
  if (clean === '') return true;
  const candidate = join(dist, decodeURIComponent(clean));
  if (clean.endsWith('/')) return existsSync(join(candidate, 'index.html'));
  return existsSync(candidate) && statSync(candidate).isFile();
}

// A link to `/docs/themes/#icons` must find an element with that id on the page it leads to.
const ids = new Map();
function anchorExists(target, fragment) {
  const clean = decodeURIComponent(fragment);
  if (clean === '') return true;
  const path = target.split('#')[0].split('?')[0];
  const file = path.endsWith('/') ? join(dist, decodeURIComponent(path), 'index.html') : join(dist, decodeURIComponent(path));
  if (!file.endsWith('.html')) return true;
  if (!ids.has(file)) ids.set(file, new Set([...readFileSync(file, 'utf8').matchAll(/\sid="([^"]*)"/g)].map((m) => m[1])));
  return ids.get(file).has(clean);
}

for (const page of pages) {
  const html = readFileSync(page, 'utf8');
  const pageUrl = urlPath(page).replace(/index\.html$/, '');
  for (const match of html.matchAll(attribute)) {
    const [, name, double, single] = match;
    const values = name.toLowerCase() === 'srcset'
      ? (double ?? single).split(',').map((part) => part.trim().split(/\s+/)[0])
      : [double ?? single];
    for (const value of values) {
      if (value === '' || /^(mailto:|tel:|data:|javascript:)/i.test(value)) continue;
      if (/^https?:\/\//i.test(value) || value.startsWith('//')) {
        // 3. Third-party references: a request the page itself makes. A link the visitor chooses to follow is
        // allowed (an `a` element's href), a host other than urdeck.app is not for anything else.
        const isAnchor = name.toLowerCase() === 'href' && /<a\s[^>]*$/i.test(html.slice(0, match.index + match[0].length));
        const own = /^https?:\/\/(www\.)?urdeck\.app(\/|$)/i.test(value);
        if (!isAnchor && !own) problems.push(`${pageUrl}: third-party reference ${name}="${value}"`);
        continue;
      }
      const target = value.startsWith('/') ? value : new URL(value, 'https://x.invalid' + pageUrl).pathname;
      if (!resolves(target)) problems.push(`${pageUrl}: ${name}="${value}" leads nowhere`);
      else if (name.toLowerCase() === 'href' && value.includes('#') && !anchorExists(target, value.split('#')[1])) {
        problems.push(`${pageUrl}: ${name}="${value}" names a section the page does not have`);
      }
    }
  }
  // Other places a page can reach out from: CSS url() and @import in inline styles, <link> and <script> elements.
  for (const css of html.matchAll(/url\(\s*['"]?(https?:)?\/\/[^)'"]+/gi)) {
    problems.push(`${pageUrl}: third-party reference in CSS: ${css[0]}`);
  }
}

for (const file of files.filter((f) => f.endsWith('.css') || f.endsWith('.js'))) {
  const text = readFileSync(file, 'utf8');
  for (const hit of text.matchAll(/(?:url\(\s*['"]?|@import\s+['"]|\bfetch\(\s*['"]|from\s*['"])(https?:)?\/\/[^)'"\s]+/gi)) {
    problems.push(`${urlPath(file)}: third-party reference: ${hit[0]}`);
  }
}

// 4. Every file the media manifest names exists: pictures in src/generated/ (Astro optimises them into the output), loops in
// public/generated/ (copied to the output as they are).
const manifestFile = join(siteRoot, 'src', 'generated', 'media.json');
if (existsSync(manifestFile)) {
  for (const entry of JSON.parse(readFileSync(manifestFile, 'utf8')).entries) {
    for (const file of entry.files) {
      const served = file.startsWith('public/') ? join(dist, file.slice('public/'.length)) : join(siteRoot, file);
      if (!existsSync(served)) problems.push(`media.json: ${entry.id} names ${file}, which does not exist`);
    }
  }
} else {
  problems.push('src/generated/media.json is missing (npm run media makes it)');
}

// 5. The theme picker (spec "site-theme-picker"): every theme of the catalog either has a deck in the manifest or is named as
// left out (a warning: a theme may land before its media refresh); every theme with media has an accent rule in the pages and
// is in the picker; every file a slot lists for any theme is in the output.
const catalogFile = join(siteRoot, 'src', 'generated', 'catalog.json');
if (existsSync(catalogFile) && existsSync(manifestFile)) {
  const catalog = JSON.parse(readFileSync(catalogFile, 'utf8'));
  const decks = new Set(JSON.parse(readFileSync(manifestFile, 'utf8')).entries.filter((e) => e.kind === 'decks').map((e) => e.theme));
  const left = catalog.themes.filter((t) => !decks.has(t.name)).map((t) => t.name);
  if (left.length > 0) console.warn(`Themes left out of the site (no deck in media.json): ${left.join(', ')}`);
  const shown = catalog.themes.filter((t) => decks.has(t.name)).map((t) => t.name);
  const home = readFileSync(join(dist, 'index.html'), 'utf8');
  for (const name of shown) {
    if (name !== 'glass' && !home.includes(`:root[data-theme="${name}"]`)) problems.push(`index.html: no accent rule for the theme "${name}"`);
    if (!home.includes(`id="tp-${name}"`)) problems.push(`index.html: the theme picker does not list "${name}"`);
  }
  const unescape = (text) => text.replace(/&quot;/g, '"').replace(/&amp;/g, '&').replace(/&#39;/g, "'");
  let slots = 0;
  for (const page of pages) {
    const html = readFileSync(page, 'utf8');
    const pageUrl = urlPath(page).replace(/index\.html$/, '');
    for (const match of html.matchAll(/data-slot="([^"]*)"[^>]*?data-variants="([^"]*)"/g)) {
      slots++;
      const variants = JSON.parse(unescape(match[2]));
      if (!variants.glass) problems.push(`${pageUrl}: slot ${match[1]} has no glass variant`);
      for (const [theme, v] of Object.entries(variants)) {
        if (!shown.includes(theme)) problems.push(`${pageUrl}: slot ${match[1]} lists "${theme}", which has no deck`);
        const urls = [v.src, v.poster, ...(v.srcset ? v.srcset.split(',').map((part) => part.trim().split(/\s+/)[0]) : [])].filter(Boolean);
        for (const url of urls) if (!resolves(url)) problems.push(`${pageUrl}: slot ${match[1]}, theme ${theme}: ${url} leads nowhere`);
      }
    }
  }
  if (slots === 0) problems.push('index.html: no media slot found (data-slot / data-variants)');
}

// A recipe of the cookbook without a picture is named, not failed: a recipe may land before its media refresh.
if (existsSync(manifestFile)) {
  const made = new Set(JSON.parse(readFileSync(manifestFile, 'utf8')).entries.filter((e) => e.kind === 'cookbook').map((e) => e.id.slice('cookbook:'.length)));
  const cookbook = join(siteRoot, 'cookbook');
  const bare = existsSync(cookbook) ? readdirSync(cookbook).filter((d) => existsSync(join(cookbook, d, 'recipe.json')) && !made.has(d)) : [];
  if (bare.length > 0) console.warn(`Cookbook recipes without a picture (no cookbook entry in media.json): ${bare.join(', ')}`);
}

// 6. The generated reference is complete ("Configuration Pages"): every key of the published schemas, every setting of every
// widget, every finding code and every built-in theme, background and widget is on a documentation page.
const records = readRecords(siteRoot);
const troubleshootingPage = join(dist, 'docs', 'troubleshooting', 'index.html');
if (existsSync(catalogFile)) {
  const gaps = completenessProblems({
    catalog: JSON.parse(readFileSync(catalogFile, 'utf8')),
    schemas: ['v1/theme.json', 'v1/shader-manifest.json'].map((name) => ({ name, paths: allPaths(readSchema(name, siteRoot)) })),
    records,
    troubleshooting: existsSync(troubleshootingPage) ? readFileSync(troubleshootingPage, 'utf8') : '',
  });
  for (const gap of gaps) problems.push(`documentation: ${gap}`);
}

if (problems.length > 0) {
  console.error('Site checks failed:\n' + problems.map((p) => '  - ' + p).join('\n'));
  process.exit(1);
}
console.log(`Site checks passed: ${pages.length} pages, schema copy identical, no broken internal link, no third-party reference.`);
