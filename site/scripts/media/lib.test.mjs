// The generator's logic on a stub catalog and a fake renderer: no .NET, no ffmpeg. `npm test`.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { afterEach, beforeEach, test } from 'node:test';
import { backgroundTheme, matrix, plan, run } from './lib.mjs';

let root, repo, site, catalog, renders, movers;

const put = (base, rel, text) => {
  mkdirSync(dirname(join(base, rel)), { recursive: true });
  writeFileSync(join(base, rel), text);
};

const makeCatalog = () => ({
  themes: [
    { name: 'ember', defaultBackground: 'embers', pairsWith: ['embers'] },
    { name: 'paper', defaultBackground: 'contour', pairsWith: ['contour', 'embers'] },
  ],
  backgrounds: [
    { name: 'contour', pairsWith: ['paper'] },
    { name: 'embers', pairsWith: ['ember'] },
  ],
  widgets: [
    { typeId: 'urdeck.widgets.clock', sizes: [{ width: 4, height: 2, preview: true }, { width: 1, height: 1, preview: false }] },
    { typeId: 'urdeck.widgets.shortcut', sizes: [{ width: 1, height: 1, preview: true }] },
  ],
});

// A renderer that writes small files and counts what it was asked to draw.
const fakeRenderer = () => ({
  canRender: true,
  catalog: async () => catalog,
  moves: async (e) => movers.has(e.kind === 'widgets' ? e.name : e.id),
  render: async (e, dest) => {
    renders.push(e.id);
    mkdirSync(dirname(dest.picture), { recursive: true });
    writeFileSync(dest.picture, e.id);
    if (dest.loop) {
      mkdirSync(dirname(dest.loop), { recursive: true });
      writeFileSync(dest.loop, e.id);
    }
  },
});

const generate = (...argv) => run({ repo, site, renderer: fakeRenderer(), argv, log: () => {} });
const staleIds = async () => {
  const p = await plan({ repo, site, catalog });
  return p.entries.filter((e) => e.state !== 'ok').map((e) => e.id).sort();
};

beforeEach(() => {
  root = mkdtempSync(join(tmpdir(), 'media-test-'));
  repo = join(root, 'repo');
  site = join(repo, 'site');
  put(repo, 'src/UrDeck.Engine/Draw.cs', 'draw v1');
  put(repo, 'src/UrDeck.Engine/Themes/Builtin/ember.json', '{"font":"F.ttf"}');
  put(repo, 'src/UrDeck.Engine/Themes/Builtin/F.ttf', 'font');
  put(repo, 'src/UrDeck.Engine/Themes/Builtin/paper.json', '{}');
  put(repo, 'src/UrDeck.Engine/Backgrounds/Builtin/embers/embers.sksl', 'half4 main() {}');
  put(repo, 'src/UrDeck.Engine/Backgrounds/Builtin/embers/sample.png', 'png1');
  put(repo, 'src/UrDeck.Engine/Backgrounds/Builtin/contour/contour.sksl', 'half4 main() {}');
  put(repo, 'widgets/UrDeck.Widgets.Clock/Clock.cs', 'clock');
  put(repo, 'providers/P/sample.json', '{}');
  put(site, 'showcase/urdeck-config.json', '{"pages":[]}');
  catalog = makeCatalog();
  renders = [];
  movers = new Set(['deck:ember', 'clock-4x2']);
});
afterEach(() => rmSync(root, { recursive: true, force: true }));

test('the matrix follows the catalog: decks, previewed sizes in every theme, backgrounds', () => {
  assert.deepEqual(matrix(catalog).map((e) => e.id), [
    'deck:ember', 'deck:paper',
    'widget:clock-4x2-ember', 'widget:clock-4x2-paper', // 1x1 has Preview = false
    'widget:shortcut-1x1-ember', 'widget:shortcut-1x1-paper',
    'background:contour', 'background:embers',
  ]);
});

test('a background is shown in the theme that defaults to it, else a mutual pairing, else a listing, else default-dark', () => {
  const themes = [
    { name: 'b', defaultBackground: 'x', pairsWith: [] },
    { name: 'a', defaultBackground: '', pairsWith: ['y', 'z'] },
    { name: 'c', defaultBackground: '', pairsWith: ['y', 'z'] },
  ];
  assert.equal(backgroundTheme({ name: 'x', pairsWith: [] }, themes), 'b');
  assert.equal(backgroundTheme({ name: 'y', pairsWith: ['c'] }, themes), 'c'); // mutual beats first by name
  assert.equal(backgroundTheme({ name: 'z', pairsWith: [] }, themes), 'a'); // listed one way
  assert.equal(backgroundTheme({ name: 'w', pairsWith: [] }, themes), 'default-dark');
});

test('a removed theme, an override and a widget configuration change the matrix', () => {
  catalog.themes.pop();
  const overrides = { decks: new Set(['ember']), widgets: { 'urdeck.widgets.clock': { config: { style: 'flap' }, loopStart: 26.5 } } };
  const entries = matrix(catalog, overrides);
  assert.equal(entries.filter((e) => e.theme === 'paper' && e.kind !== 'backgrounds').length, 0);
  const clock = entries.find((e) => e.id === 'widget:clock-4x2-ember');
  assert.equal(clock.loopStart, 26.5);
  assert.deepEqual(clock.config, { style: 'flap' });
  assert.equal(entries.find((e) => e.id === 'deck:ember').override, true);
});

test('a first run renders everything, with loops where things move; a second run renders nothing', async () => {
  await generate();
  assert.equal(renders.length, 8);
  assert.ok(existsSync(join(site, 'src/generated/decks/ember.webp')));
  assert.ok(existsSync(join(site, 'public/generated/decks/ember.mp4')));
  assert.ok(!existsSync(join(site, 'public/generated/decks/paper.mp4')));
  assert.ok(existsSync(join(site, 'public/generated/widgets/clock-4x2-paper.mp4')));
  assert.ok(!existsSync(join(site, 'public/generated/widgets/shortcut-1x1-paper.mp4')));
  assert.ok(existsSync(join(site, 'public/generated/backgrounds/contour.mp4')));

  const manifest = readFileSync(join(site, 'src/generated/media.json'), 'utf8');
  assert.ok(manifest.endsWith('\n'));
  renders = [];
  await generate();
  assert.deepEqual(renders, []);
  assert.equal(readFileSync(join(site, 'src/generated/media.json'), 'utf8'), manifest);
});

test('a theme file that changes stales only the entries that use it, its background posters included', async () => {
  await generate();
  put(repo, 'src/UrDeck.Engine/Themes/Builtin/ember.json', '{"font":"F.ttf","x":1}');
  assert.deepEqual(await staleIds(), ['background:embers', 'deck:ember', 'widget:clock-4x2-ember', 'widget:shortcut-1x1-ember']);
});

test('a font a theme names, a background folder and the showcase page stale their users; a sample.png does not', async () => {
  await generate();
  put(repo, 'src/UrDeck.Engine/Themes/Builtin/F.ttf', 'font 2');
  assert.deepEqual(await staleIds(), ['background:embers', 'deck:ember', 'widget:clock-4x2-ember', 'widget:shortcut-1x1-ember']);
  await generate();
  put(repo, 'src/UrDeck.Engine/Backgrounds/Builtin/embers/embers.sksl', 'half4 main() { return half4(1); }');
  assert.deepEqual(await staleIds(), ['background:embers', 'deck:ember', 'widget:clock-4x2-ember', 'widget:shortcut-1x1-ember']);
  await generate();
  put(repo, 'src/UrDeck.Engine/Backgrounds/Builtin/embers/sample.png', 'png2');
  assert.deepEqual(await staleIds(), []);
  put(site, 'showcase/urdeck-config.json', '{"pages":[{"name":"x"}]}');
  assert.deepEqual(await staleIds(), ['deck:ember', 'deck:paper']);
});

test('drawing code stales everything; a build output folder does not', async () => {
  await generate();
  put(repo, 'src/UrDeck.Engine/bin/Release/x.dll', 'built');
  assert.deepEqual(await staleIds(), []);
  put(repo, 'src/UrDeck.Engine/Draw.cs', 'draw v2');
  assert.equal((await staleIds()).length, 8);
  put(repo, 'src/UrDeck.Engine/Draw.cs', 'draw v1');
  put(repo, 'widgets/UrDeck.Widgets.Clock/Clock.cs', 'clock 2');
  assert.equal((await staleIds()).length, 8);
});

test('line endings do not change a key', async () => {
  put(repo, 'src/UrDeck.Engine/Other.cs', 'a\nb\n');
  await generate();
  put(repo, 'src/UrDeck.Engine/Other.cs', 'a\r\nb\r\n');
  assert.deepEqual(await staleIds(), []);
});

test('a removed widget size, a removed theme and a new one: files deleted and added', async () => {
  await generate();
  catalog.themes = catalog.themes.filter((t) => t.name !== 'paper');
  catalog.widgets[0].sizes = [{ width: 1, height: 1, preview: true }];
  catalog.themes.push({ name: 'pop', defaultBackground: '', pairsWith: [] });
  put(repo, 'src/UrDeck.Engine/Themes/Builtin/pop.json', '{}');
  const result = await generate();
  assert.ok(!existsSync(join(site, 'src/generated/decks/paper.webp')));
  assert.ok(!existsSync(join(site, 'src/generated/widgets/clock-4x2-ember.webp')));
  assert.ok(!existsSync(join(site, 'public/generated/widgets/clock-4x2-ember.mp4')));
  assert.ok(existsSync(join(site, 'src/generated/decks/pop.webp')));
  assert.ok(result.removed.length >= 5);
  assert.ok(result.rendered.some((e) => e.id === 'widget:clock-1x1-pop'));
  assert.deepEqual(await staleIds(), []);
});

test('a loop whose entry stopped moving is removed with it', async () => {
  await generate();
  movers.delete('clock-4x2');
  put(repo, 'widgets/UrDeck.Widgets.Clock/Clock.cs', 'clock static');
  await generate();
  assert.ok(!existsSync(join(site, 'public/generated/widgets/clock-4x2-paper.mp4')));
  assert.ok(existsSync(join(site, 'src/generated/widgets/clock-4x2-paper.webp')));
});

test('a per-theme deck override is used instead of the showcase page, and keyed', async () => {
  await generate();
  put(site, 'decks/paper/urdeck-config.json', '{"pages":[{"name":"Paper only"}]}');
  assert.deepEqual(await staleIds(), ['deck:paper']);
  await generate('decks');
  const { entries } = JSON.parse(readFileSync(join(site, 'src/generated/media.json'), 'utf8'));
  assert.equal(entries.find((e) => e.id === 'deck:paper').settings.config, 'site/decks/paper/urdeck-config.json');
});

test('--all renders everything, a filter only its names, --status nothing', async () => {
  await generate();
  renders = [];
  await generate('--all');
  assert.equal(renders.length, 8);
  renders = [];
  await generate('--all', 'widgets', 'clock-4x2');
  assert.deepEqual(renders, ['widget:clock-4x2-ember', 'widget:clock-4x2-paper']);
  renders = [];
  put(repo, 'src/UrDeck.Engine/Draw.cs', 'changed');
  await generate('--status');
  assert.deepEqual(renders, []);
});

test('a failed render leaves the previous files and the stale keys alone', async () => {
  await generate();
  put(repo, 'src/UrDeck.Engine/Themes/Builtin/paper.json', '{"x":1}');
  const failing = { ...fakeRenderer(), render: async () => { throw new Error('boom'); } };
  await assert.rejects(run({ repo, site, renderer: failing, argv: [], log: () => {} }), /boom/);
  assert.ok(existsSync(join(site, 'src/generated/decks/paper.webp')));
  assert.deepEqual(await staleIds(), ['background:contour', 'deck:paper', 'widget:clock-4x2-paper', 'widget:shortcut-1x1-paper']);
});

test('the manifest is sorted and records the options of each file', async () => {
  await generate();
  const { entries } = JSON.parse(readFileSync(join(site, 'src/generated/media.json'), 'utf8'));
  assert.deepEqual(entries.map((e) => e.id), entries.map((e) => e.id).sort());
  const clock = entries.find((e) => e.id === 'widget:clock-4x2-ember');
  assert.deepEqual(clock.files, ['src/generated/widgets/clock-4x2-ember.webp', 'public/generated/widgets/clock-4x2-ember.mp4']);
  assert.equal(clock.settings.widgetSize, '4x2');
  assert.equal(clock.settings.moment, '2026-10-13T21:47:32.000');
});

// ---- The cookbook ("Cookbook Pictures" of the site-media spec)

const recipe = (name, show = 'page', extra = {}) => {
  put(site, `cookbook/${name}/recipe.json`, JSON.stringify({ title: name, summary: 's', order: 1, show, background: null, ...extra }));
  put(site, `cookbook/${name}/theme.json`, `{"format":2,"meta":{"name":"${name}"}}`);
  put(site, `cookbook/${name}/README.md`, 'why');
};

test('a recipe folder adds one still picture, named by the folder, to the matrix', () => {
  recipe('hard-shadow');
  recipe('led-bars', { widget: 'urdeck.widgets.clock', size: '4x2' });
  const entries = matrix(catalog, { recipes: { 'hard-shadow': { show: 'page' }, 'led-bars': { show: { widget: 'urdeck.widgets.clock', size: '4x2' } } } });
  const rows = entries.filter((e) => e.kind === 'cookbook');
  assert.deepEqual(rows.map((e) => e.id), ['cookbook:hard-shadow', 'cookbook:led-bars']);
  assert.equal(rows[0].picture, 'cookbook/hard-shadow.webp');
  assert.equal(rows[0].loop, null);
  assert.equal(rows[1].widget, 'urdeck.widgets.clock');
  assert.equal(rows[1].show, 'widget');
});

test('a new recipe gets its picture and the media manifest names it; removing it removes both', async () => {
  await generate();
  renders = [];
  recipe('hard-shadow');
  const result = await generate();
  assert.deepEqual(renders, ['cookbook:hard-shadow']);
  assert.ok(existsSync(join(site, 'src/generated/cookbook/hard-shadow.webp')));
  assert.ok(!existsSync(join(site, 'public/generated/cookbook/hard-shadow.mp4')));
  const { entries } = JSON.parse(readFileSync(join(site, 'src/generated/media.json'), 'utf8'));
  const entry = entries.find((e) => e.id === 'cookbook:hard-shadow');
  assert.deepEqual(entry.files, ['src/generated/cookbook/hard-shadow.webp']);
  assert.equal(entry.settings.themeDir, 'site/cookbook/hard-shadow');
  assert.equal(result.rendered.length, 1);

  rmSync(join(site, 'cookbook/hard-shadow'), { recursive: true });
  const after = await generate();
  assert.ok(!existsSync(join(site, 'src/generated/cookbook/hard-shadow.webp')));
  assert.deepEqual(after.removed, ['src/generated/cookbook/hard-shadow.webp']);
});

test('a change inside one recipe folder stales only that recipe, an asset of it included', async () => {
  recipe('circular-dock', 'dock');
  recipe('hard-shadow');
  await generate();
  assert.deepEqual(await staleIds(), []);
  put(site, 'cookbook/circular-dock/theme.json', '{"format":2,"card":{"compact":{"shape":"circle"}}}');
  assert.deepEqual(await staleIds(), ['cookbook:circular-dock']);
  await generate();
  put(site, 'cookbook/hard-shadow/icons/mine/sun.svg', '<svg/>');
  assert.deepEqual(await staleIds(), ['cookbook:hard-shadow']);
  await generate();
  put(site, 'cookbook/hard-shadow/README.md', 'a better explanation');
  assert.deepEqual(await staleIds(), ['cookbook:hard-shadow']); // the folder is the input, the text included
});

test('what a recipe shows is part of its key, and only the cookbook filter renders recipes', async () => {
  recipe('circular-dock', 'dock');
  await generate();
  put(site, 'cookbook/circular-dock/recipe.json', JSON.stringify({ title: 'x', summary: 's', order: 1, show: 'page', background: null }));
  assert.deepEqual(await staleIds(), ['cookbook:circular-dock']);
  renders = [];
  await generate('decks');
  assert.deepEqual(renders, []);
  await generate('cookbook');
  assert.deepEqual(renders, ['cookbook:circular-dock']);
});
