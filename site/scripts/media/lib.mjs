// The site's media as a generated output of the repository (openspec: site-media-pipeline).
//
//   matrix   what the site needs, from the catalog (`urdeck-cli catalog export`) and the site's overrides
//   plan     each entry's input key, what is missing, stale or unneeded
//   run      renders the stale entries through a renderer, writes the generated folders and the manifest
//
// The renderer is passed in (renderer.mjs makes the real one: urdeck-cli, sharp, ffmpeg), so the tests use a fake one.
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, renameSync, cpSync, rmSync, writeFileSync } from 'node:fs';
import { cpus, tmpdir } from 'node:os';
import { dirname, join, relative, sep } from 'node:path';

// Bump it when the way a file is rendered changes without any file of the shared inputs changing.
export const SETTINGS_VERSION = 1;

export const SAMPLE_MOMENT = '2026-10-13T21:47:32';
export const DECK_SIZE = '1100x3840';
export const DECK_LOOP_SIZE = '550x1920'; // the loop ships at 540 px wide; drawing the shader at 1100x3840 for it would cost 4x the time
export const POSTER_SIZE = '540x960';
export const LOOP_SECONDS = 3; // played forwards then backwards: (2 * 90 - 2) / 30 = 5.9 s, inside "Media Limits"
export const FPS = 30;

const IMAGES = 'src/generated'; // pictures, optimised by Astro
const LOOPS = 'public/generated'; // loops, served as they are

const SHARED_DIRS = ['src/UrDeck.Engine', 'src/UrDeck.Preview', 'sdk/UrDeck.Sdk', 'widgets'];
const SHARED_EXCLUDED = ['src/UrDeck.Engine/Themes/Builtin', 'src/UrDeck.Engine/Backgrounds/Builtin'];
const THEME_DIR = 'src/UrDeck.Engine/Themes/Builtin';
const BACKGROUND_DIR = 'src/UrDeck.Engine/Backgrounds/Builtin';

const KINDS = ['decks', 'widgets', 'backgrounds', 'cookbook'];
export const COOKBOOK_SIZE = '1100x1920'; // the sample page's own size: the cookbook shows a page, the dock or a widget, never the whole 3840 px deck

export const stable = (value) => JSON.stringify(sortKeys(value), null, 2) + '\n';
function sortKeys(v) {
  if (Array.isArray(v)) return v.map(sortKeys);
  if (v && typeof v === 'object') return Object.fromEntries(Object.keys(v).sort().map((k) => [k, sortKeys(v[k])]));
  return v;
}

// ---- Hashing -------------------------------------------------------------------------------------------------------

function walk(dir, skip = () => false) {
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const p = join(dir, e.name);
    if (skip(p, e)) return [];
    return e.isDirectory() ? walk(p, skip) : [p];
  });
}

// Text files hash with LF line endings, so a checkout with CRLF and one with LF agree.
function contentOf(file) {
  const bytes = readFileSync(file);
  return bytes.subarray(0, 8000).includes(0) ? bytes : Buffer.from(bytes.toString('latin1').replace(/\r\n/g, '\n'), 'latin1');
}

function hashFiles(root, files) {
  const h = createHash('sha256');
  for (const f of files.map((x) => relative(root, x).split(sep).join('/')).sort()) {
    h.update(f).update('\0').update(contentOf(join(root, f))).update('\0');
  }
  return h.digest('hex');
}

const isBuildOutput = (p, e) => e.isDirectory() && (e.name === 'bin' || e.name === 'obj');

export function sharedHash(repo) {
  const files = [];
  const excluded = SHARED_EXCLUDED.map((d) => join(repo, d));
  for (const d of SHARED_DIRS) {
    files.push(...walk(join(repo, d), (p, e) => isBuildOutput(p, e) || excluded.includes(p)));
  }
  const providers = join(repo, 'providers');
  if (existsSync(providers)) {
    for (const p of readdirSync(providers)) {
      const sample = join(providers, p, 'sample.json');
      if (existsSync(sample)) files.push(sample);
    }
  }
  return createHash('sha256').update(`settings:${SETTINGS_VERSION}\0`).update(hashFiles(repo, files)).digest('hex');
}

function themeHash(repo, name) {
  const file = join(repo, THEME_DIR, `${name}.json`);
  if (!existsSync(file)) return `no-theme:${name}`;
  const fonts = [...new Set(readFileSync(file, 'utf8').match(/[\w.-]+\.(?:ttf|otf)/gi) ?? [])]
    .map((f) => join(repo, THEME_DIR, f)).filter(existsSync);
  return hashFiles(repo, [file, ...fonts]);
}

function backgroundHash(repo, name) {
  const dir = join(repo, BACKGROUND_DIR, name);
  if (!existsSync(dir)) return `no-background:${name}`;
  // sample.png is itself a rendering of the shader (urdeck-cli shader render), not an input.
  return hashFiles(repo, walk(dir, (p, e) => !e.isDirectory() && e.name === 'sample.png'));
}

/** A cookbook recipe's inputs are its whole folder: the theme, its assets (an icon set), the recipe and the explanation. */
function recipeHash(site, name) {
  const dir = join(site, 'cookbook', name);
  return existsSync(dir) ? hashFiles(dir, walk(dir)) : `no-recipe:${name}`;
}

const fileHash = (file) => (file && existsSync(file) ? hashFiles(dirname(file), [file]) : null);
const sha = (text) => createHash('sha256').update(text).digest('hex');

// ---- The matrix ----------------------------------------------------------------------------------------------------

/** The theme a background is shown in: its default's first theme, else the first that pairs both ways, else lists it. */
export function backgroundTheme(background, themes) {
  const sorted = [...themes].sort((a, b) => a.name.localeCompare(b.name, 'en'));
  const hit = sorted.find((t) => t.defaultBackground === background.name)
    ?? sorted.find((t) => (t.pairsWith ?? []).includes(background.name) && (background.pairsWith ?? []).includes(t.name))
    ?? sorted.find((t) => (t.pairsWith ?? []).includes(background.name));
  return hit?.name ?? 'default-dark';
}

export const widgetId = (typeId) => typeId.split('.').pop();

/**
 * @param overrides {decks: Set<theme>, widgets: {typeId: {config, loopStart, sizes: {"4x2": {config, loopStart}}}},
 *                   recipes: {name: {title, summary, order, show, background}}}
 */
export function matrix(catalog, overrides = {}) {
  const entries = [];
  const themes = [...catalog.themes].sort((a, b) => a.name.localeCompare(b.name, 'en'));
  for (const t of themes) {
    entries.push({
      id: `deck:${t.name}`, kind: 'decks', name: t.name, theme: t.name, widget: null, size: null,
      background: t.defaultBackground || null, picture: `decks/${t.name}.webp`, loop: `decks/${t.name}.mp4`,
      override: overrides.decks?.has(t.name) ?? false,
    });
  }
  for (const w of [...catalog.widgets].sort((a, b) => a.typeId.localeCompare(b.typeId, 'en'))) {
    const id = widgetId(w.typeId);
    for (const s of w.sizes.filter((x) => x.preview !== false)) {
      const size = `${s.width}x${s.height}`;
      const o = overrides.widgets?.[w.typeId] ?? {};
      const per = o.sizes?.[size] ?? {};
      for (const t of themes) {
        const base = `widgets/${id}-${size}-${t.name}`;
        entries.push({
          id: `widget:${id}-${size}-${t.name}`, kind: 'widgets', name: `${id}-${size}`, theme: t.name, widget: w.typeId, size,
          background: t.defaultBackground || null, picture: `${base}.webp`, loop: `${base}.mp4`,
          config: { ...(o.config ?? {}), ...(per.config ?? {}) }, loopStart: per.loopStart ?? o.loopStart ?? 0,
        });
      }
    }
  }
  for (const b of [...catalog.backgrounds].sort((a, c) => a.name.localeCompare(c.name, 'en'))) {
    entries.push({
      id: `background:${b.name}`, kind: 'backgrounds', name: b.name, theme: backgroundTheme(b, catalog.themes), widget: null,
      size: POSTER_SIZE, background: b.name, picture: `backgrounds/${b.name}.webp`, loop: `backgrounds/${b.name}.mp4`,
    });
  }
  // One picture of each recipe's theme folder, showing what its recipe.json asks for (the page, the dock or one widget).
  for (const name of Object.keys(overrides.recipes ?? {}).sort((a, c) => a.localeCompare(c, 'en'))) {
    const { show = 'page', background = null } = overrides.recipes[name];
    const widget = typeof show === 'object' ? show.widget : null;
    const size = typeof show === 'object' ? show.size : null;
    const o = (widget && overrides.widgets?.[widget]) || {};
    const per = (size && o.sizes?.[size]) || {};
    entries.push({
      id: `cookbook:${name}`, kind: 'cookbook', name, theme: name, widget, size, background: background || null,
      show: typeof show === 'object' ? 'widget' : show, picture: `cookbook/${name}.webp`, loop: null,
      config: { ...(o.config ?? {}), ...(per.config ?? {}) }, loopStart: per.loopStart ?? o.loopStart ?? 0,
    });
  }
  return entries;
}

// ---- The plan ------------------------------------------------------------------------------------------------------

export function readOverrides(site) {
  const decks = new Set();
  const dir = join(site, 'decks');
  if (existsSync(dir)) {
    for (const d of readdirSync(dir)) if (existsSync(join(dir, d, 'urdeck-config.json'))) decks.add(d);
  }
  const widgetsFile = join(site, 'showcase', 'widgets.json');
  // A folder of site/cookbook/ with a recipe.json is a recipe; its name is the folder's.
  const recipes = {};
  const cookbook = join(site, 'cookbook');
  if (existsSync(cookbook)) {
    for (const d of readdirSync(cookbook)) {
      const file = join(cookbook, d, 'recipe.json');
      if (existsSync(file)) recipes[d] = JSON.parse(readFileSync(file, 'utf8'));
    }
  }
  return { decks, widgets: existsSync(widgetsFile) ? JSON.parse(readFileSync(widgetsFile, 'utf8')) : {}, recipes };
}

export const deckConfigPath = (site, entry) =>
  entry.override ? join(site, 'decks', entry.theme, 'urdeck-config.json') : join(site, 'showcase', 'urdeck-config.json');

function readManifest(site) {
  const file = join(site, IMAGES, 'media.json');
  return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : { version: 1, entries: [] };
}

const filesOf = (entry, loop) => [`${IMAGES}/${entry.picture}`, ...(loop ? [`${LOOPS}/${entry.loop}`] : [])];

/** The options a file is made with; written to the manifest so a visitor can reproduce it. */
export function settingsOf(entry, loop) {
  const common = { theme: entry.theme, background: 'theme' };
  if (entry.kind === 'decks') {
    return { ...common, config: entry.override ? `site/decks/${entry.theme}/urdeck-config.json` : 'site/showcase/urdeck-config.json',
      page: 'dock', size: DECK_SIZE, moment: SAMPLE_MOMENT, loop: loop ? { frames: LOOP_SECONDS * FPS, fps: FPS, size: DECK_LOOP_SIZE } : null };
  }
  if (entry.kind === 'cookbook') {
    return { themeDir: `site/cookbook/${entry.name}`, background: entry.background ?? 'none', show: entry.show, widget: entry.widget,
      widgetSize: entry.size, widgetConfig: entry.widget ? entry.config : null, size: entry.widget ? null : COOKBOOK_SIZE,
      moment: entry.widget ? addSeconds(SAMPLE_MOMENT, entry.loopStart) : SAMPLE_MOMENT, loop: null };
  }
  if (entry.kind === 'widgets') {
    return { ...common, widget: entry.widget, widgetSize: entry.size, width: 1100, widgetConfig: entry.config,
      moment: addSeconds(SAMPLE_MOMENT, entry.loopStart), loop: loop ? { frames: LOOP_SECONDS * FPS, fps: FPS } : null };
  }
  return { theme: entry.theme, background: entry.background, size: POSTER_SIZE, moment: SAMPLE_MOMENT,
    loop: { frames: LOOP_SECONDS * FPS, fps: FPS } };
}

export function addSeconds(moment, seconds) {
  const d = new Date(`${moment}Z`);
  d.setTime(d.getTime() + Math.round(seconds * 1000));
  return d.toISOString().slice(0, 23);
}

function listGenerated(site) {
  const out = [];
  for (const root of [IMAGES, LOOPS]) {
    for (const f of walk(join(site, root))) {
      const rel = relative(site, f).split(sep).join('/');
      if (rel === `${IMAGES}/media.json` || rel === `${IMAGES}/catalog.json` || f.endsWith('.gitkeep')) continue;
      out.push(rel);
    }
  }
  return out.sort();
}

/**
 * @param moves async (entry) => boolean | null  whether the entry's media move; null when it cannot be told (no renderer)
 * @returns {entries, unneeded}; each entry has key, loop (bool), files, settings and state ok | missing | stale
 */
export async function plan({ repo, site, catalog, moves = null, log = () => {} }) {
  const overrides = readOverrides(site);
  const entries = matrix(catalog, overrides);
  const manifest = readManifest(site);
  const previous = new Map(manifest.entries.map((e) => [e.id, e]));
  const shared = sharedHash(repo);
  const showcase = fileHash(join(site, 'showcase', 'urdeck-config.json'));
  const memo = new Map();

  for (const e of entries) {
    const specific = {
      theme: e.kind === 'cookbook' ? recipeHash(site, e.name) : themeHash(repo, e.theme),
      background: e.background ? backgroundHash(repo, e.background) : null,
      // The theme's own background is the one that is drawn over a background that is a name.
      kind: e.kind, widget: e.widget, size: e.size, config: e.config ?? null, loopStart: e.loopStart ?? null,
      page: e.kind === 'decks' ? fileHash(deckConfigPath(site, e)) ?? showcase : null,
      settings: settingsOf(e, false),
    };
    e.key = sha(`${shared}\0${stable(specific)}`);
    const old = previous.get(e.id);
    const present = (files) => files.every((f) => existsSync(join(site, f)));
    if (e.kind === 'backgrounds') e.loopMade = true;
    else if (e.kind === 'cookbook') e.loopMade = false; // a recipe is a still picture
    else if (old && old.key === e.key) e.loopMade = old.files.some((f) => f.startsWith(LOOPS));
    else {
      // Probes start now and run together; their answers are read just below.
      e.probeKey = e.kind === 'widgets' ? `${e.widget}@${e.size}@${e.theme}@${stable(e.config)}@${e.loopStart}` : e.id; // a theme can still a widget (the weather icons in Blueprint)
      if (!memo.has(e.probeKey)) {
        const t = Date.now();
        const key = e.probeKey;
        memo.set(key, moves ? Promise.resolve(moves(e)).then((m) => {
          if (m !== null) log(`probe ${key.split('@')[0]}: ${m ? 'moves' : 'still'} (${((Date.now() - t) / 1000).toFixed(1)} s)`);
          return m;
        }) : Promise.resolve(null));
      }
      e.fallbackLoop = old ? old.files.some((f) => f.startsWith(LOOPS)) : false;
    }
    if (e.probeKey) e.loopMade = (await memo.get(e.probeKey)) ?? e.fallbackLoop;
    e.files = filesOf(e, e.loopMade);
    e.settings = settingsOf(e, e.loopMade);
    e.state = !old || !present(e.files) ? 'missing' : old.key !== e.key ? 'stale' : 'ok';
  }

  const named = new Set(entries.flatMap((e) => e.files));
  const unneeded = listGenerated(site).filter((f) => !named.has(f));
  return { entries, unneeded };
}

// ---- Filters, the report -------------------------------------------------------------------------------------------

// Renders at once: each is mostly one core; leave room for the machine.
const defaultJobs = () => Math.max(1, Math.min(6, Math.floor(cpus().length / 4)));

export function parseArgs(argv) {
  const opts = { status: false, all: false, kinds: [], names: [], report: null, catalog: null, jobs: defaultJobs() };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--status') opts.status = true;
    else if (a === '--all') opts.all = true;
    else if (a === '--report') opts.report = argv[++i];
    else if (a === '--catalog') opts.catalog = argv[++i];
    else if (a === '--jobs') opts.jobs = Math.max(1, Number(argv[++i]) || 1);
    else if (KINDS.includes(a)) opts.kinds.push(a);
    else if (a.startsWith('--')) throw new Error(`unknown option ${a}`);
    else opts.names.push(a);
  }
  return opts;
}

const wanted = (e, opts) =>
  (opts.kinds.length === 0 || opts.kinds.includes(e.kind)) &&
  (opts.names.length === 0 || opts.names.some((n) => [e.name, e.id, e.theme, e.id.split(':')[1]].includes(n)));

export function report(result) {
  const by = (state) => result.entries.filter((e) => e.state === state);
  const lines = [
    `Media: ${result.entries.length} entries, ${result.entries.flatMap((e) => e.files).length} files`,
    `  missing:  ${by('missing').length}`,
    `  stale:    ${by('stale').length}`,
    `  unneeded: ${result.unneeded.length}`,
  ];
  for (const [label, list] of [['missing', by('missing').map((e) => e.id)], ['stale', by('stale').map((e) => e.id)], ['unneeded', result.unneeded]]) {
    if (list.length > 0) lines.push('', `${label}:`, ...list.map((x) => `  ${x}`));
  }
  return lines.join('\n') + '\n';
}

/** Markdown for a pull request: added, changed and removed, per kind. */
export function markdown(before, rendered, removed) {
  const out = [];
  const group = (title, items) => {
    if (items.length === 0) return;
    out.push(`### ${title}`);
    for (const kind of KINDS) {
      const k = items.filter((e) => e.kind === kind);
      if (k.length > 0) out.push(`- **${kind}** (${k.length}): ${k.map((e) => `\`${e.name}${e.kind === 'widgets' ? `-${e.theme}` : ''}\``).join(', ')}`);
    }
    out.push('');
  };
  group('Added', rendered.filter((e) => before.get(e.id) === 'missing'));
  group('Changed', rendered.filter((e) => before.get(e.id) === 'stale' || before.get(e.id) === 'forced'));
  if (removed.length > 0) out.push('### Removed', ...removed.map((f) => `- \`${f}\``), '');
  return out.join('\n');
}

// ---- Running -------------------------------------------------------------------------------------------------------

/**
 * @param renderer {catalog(), moves(entry), render(entry, dest, work)}; render writes dest.picture and, when
 *                 entry.loopMade, dest.loop (absolute paths)
 */
export async function run({ repo, site, renderer, argv, log = console.log }) {
  const opts = parseArgs(argv);
  const catalog = opts.status && opts.catalog ? JSON.parse(readFileSync(opts.catalog, 'utf8')) : await renderer.catalog();
  const result = await plan({ repo, site, catalog, moves: opts.status && !renderer.canRender ? null : (e) => (wanted(e, opts) ? renderer.moves(e) : null), log });

  if (opts.status) {
    log(report(result));
    return result;
  }

  const before = new Map(result.entries.map((e) => [e.id, opts.all ? 'forced' : e.state]));
  const todo = result.entries.filter((e) => wanted(e, opts) && (opts.all || e.state !== 'ok'));
  const work = mkdtempSync(join(tmpdir(), 'urdeck-site-media-'));
  try {
    let next = 0;
    const worker = async () => {
      for (let i = next++; i < todo.length; i = next++) await renderOne(i);
    };
    const renderOne = async (i) => {
      const e = todo[i];
      const began = Date.now();
      log(`[${i + 1}/${todo.length}] ${e.id}${e.loopMade ? ' (with a loop)' : ''}`);
      const dest = { picture: join(work, 'out', e.picture), loop: e.loopMade ? join(work, 'out', e.loop) : null };
      mkdirSync(dirname(dest.picture), { recursive: true });
      if (dest.loop) mkdirSync(dirname(dest.loop), { recursive: true });
      await renderer.render(e, dest, join(work, `w${i}`));
      // Only now does the entry's media change in the site: a failed render leaves the previous files.
      move(dest.picture, join(site, IMAGES, e.picture));
      if (dest.loop) move(dest.loop, join(site, LOOPS, e.loop));
      else if (e.loop) rmSync(join(site, LOOPS, e.loop), { force: true });
      e.state = 'ok';
      log(`  ${e.id}: ${((Date.now() - began) / 1000).toFixed(1)} s`);
    };
    await Promise.all(Array.from({ length: Math.max(1, Math.min(opts.jobs, todo.length)) }, worker));
  } finally {
    rmSync(work, { recursive: true, force: true });
  }

  // Files the matrix no longer names, and the loops of entries that stopped moving.
  const named = new Set(result.entries.flatMap((e) => e.files));
  const removed = listGenerated(site).filter((f) => !named.has(f));
  for (const f of removed) rmSync(join(site, f), { force: true });
  pruneEmpty(join(site, IMAGES));
  pruneEmpty(join(site, LOOPS));

  const rendered = new Set(todo.map((e) => e.id));
  const previous = new Map(readManifest(site).entries.map((e) => [e.id, e]));
  const entries = result.entries
    .filter((e) => rendered.has(e.id) || (previous.has(e.id) && e.files.every((f) => existsSync(join(site, f)))))
    .map((e) => {
      const old = previous.get(e.id);
      if (!rendered.has(e.id) && old) return old; // not rendered this time: it keeps its key, so it stays stale if it was
      return { id: e.id, kind: e.kind, theme: e.theme, widget: e.widget, size: e.size, background: e.background,
        files: e.files, key: e.key, settings: e.settings };
    })
    .sort((a, b) => a.id.localeCompare(b.id, 'en'));
  mkdirSync(join(site, IMAGES), { recursive: true });
  writeFileSync(join(site, IMAGES, 'media.json'), stable({ version: 1, entries }));
  writeFileSync(join(site, IMAGES, 'catalog.json'), stable(catalog));

  const text = markdown(before, todo, removed);
  log(todo.length === 0 && removed.length === 0 ? 'Media are current.' : `Rendered ${todo.length} entries, removed ${removed.length} files.`);
  if (opts.report) writeFileSync(opts.report, text);
  return { ...result, rendered: todo, removed };
}

function move(from, to) {
  mkdirSync(dirname(to), { recursive: true });
  try {
    renameSync(from, to);
  } catch {
    cpSync(from, to); // across devices
    rmSync(from, { force: true });
  }
}

function pruneEmpty(dir) {
  if (!existsSync(dir)) return;
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    if (!e.isDirectory()) continue;
    const p = join(dir, e.name);
    pruneEmpty(p);
    if (readdirSync(p).length === 0) rmSync(p, { recursive: true });
  }
}

