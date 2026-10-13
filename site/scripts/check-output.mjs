// Checks of the built site (dist/): the schema copy, internal links, and third-party references.
// Run by `npm run build` after `astro build`; any problem fails the build.
import { existsSync, readdirSync, readFileSync, statSync, mkdirSync, copyFileSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const dist = join(here, '..', 'dist');
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

if (problems.length > 0) {
  console.error('Site checks failed:\n' + problems.map((p) => '  - ' + p).join('\n'));
  process.exit(1);
}
console.log(`Site checks passed: ${pages.length} pages, schema copy identical, no broken internal link, no third-party reference.`);
