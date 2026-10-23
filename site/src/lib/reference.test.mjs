// The reference tables' rules on small fixture schemas: a key without a description fails, a `$ref` lends none. `npm test`.
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, before, test } from 'node:test';
import { allPaths, inline, readSchema, rowsOf, settingDefault, settingValue } from './reference.mjs';

let base;
let root;

const schema = {
  $defs: {
    color: { type: 'string', pattern: '^#', description: 'A colour.' },
    glow: { type: 'object', description: 'A glow.', properties: { blur: { type: 'number', minimum: 0, maximum: 1, description: 'Blur.' } } },
  },
  properties: {
    format: { enum: [1, 2], description: 'The format.' },
    card: {
      type: 'object',
      description: 'The card.',
      properties: {
        radius: { type: 'number', minimum: 0, maximum: 0.5, default: 0.08, description: 'Corner radius.' },
        cutCorners: { type: 'array', items: { enum: ['tl', 'tr'] }, description: 'Corners.' },
        fill: { $ref: '#/$defs/color', description: 'The fill.' },
        glow: { $ref: '#/$defs/glow', description: 'The glow.' },
        stops: { type: 'array', minItems: 2, maxItems: 6, items: { $ref: '#/$defs/color' }, description: 'Stops.' },
      },
    },
    parameters: { type: 'object', description: 'Per name.', additionalProperties: { type: 'object', properties: { label: { type: 'string', description: 'Label.' } } } },
    samples: { type: 'array', description: 'Samples.', items: { type: 'object', properties: { file: { type: 'string', description: 'File.' } } } },
  },
};

before(() => {
  base = mkdtempSync(join(tmpdir(), 'reference-test-'));
  root = join(base, 'site');
  mkdirSync(join(base, 'docs', 'schemas', 'v9'), { recursive: true });
  mkdirSync(root);
  writeFileSync(join(base, 'docs', 'schemas', 'v9', 'a.json'), JSON.stringify(schema));
});
after(() => rmSync(base, { recursive: true, force: true }));

test('a group is a row with its keys below it, and a key says what it accepts and its default', () => {
  const rows = rowsOf('v9/a.json', 'card', root);
  assert.deepEqual(rows.map((r) => r.path), ['card', 'card.radius', 'card.cutCorners', 'card.fill', 'card.glow', 'card.glow.blur', 'card.stops']);
  const byPath = Object.fromEntries(rows.map((r) => [r.path, r]));
  assert.equal(byPath['card.radius'].value, 'number, 0 to 0.5');
  assert.equal(byPath['card.radius'].default, '`0.08`');
  assert.equal(byPath['card.cutCorners'].value, 'list of names of `tl`, `tr`');
  assert.equal(byPath['card.fill'].value, 'colour');
  assert.equal(byPath['card.stops'].value, 'list of 2 to 6 colours');
  assert.equal(byPath['card.glow'].group, true);
  assert.equal(byPath['card.glow.blur'].depth, 2);
});

test('maps and lists of groups get <name> and [] paths, and allPaths lists every key', () => {
  assert.deepEqual(allPaths(readSchema('v9/a.json', root)), [
    'format', 'card', 'card.radius', 'card.cutCorners', 'card.fill', 'card.glow', 'card.glow.blur', 'card.stops',
    'parameters', 'parameters.<name>.label', 'samples', 'samples[].file',
  ]);
  assert.deepEqual(rowsOf('v9/a.json', 'samples', root).map((r) => r.path), ['samples', 'samples[].file']);
});

test('a key without a description fails the build and names the key; a $ref does not lend one', () => {
  const broken = structuredClone(schema);
  broken.properties.card.properties.fill = { $ref: '#/$defs/color' };
  writeFileSync(join(base, 'docs', 'schemas', 'v9', 'b.json'), JSON.stringify(broken));
  assert.throws(() => rowsOf('v9/b.json', 'card', root), /the key "card\.fill" has no description/);
  const blank = structuredClone(schema);
  blank.properties.format.description = '  ';
  writeFileSync(join(base, 'docs', 'schemas', 'v9', 'c.json'), JSON.stringify(blank));
  assert.throws(() => rowsOf('v9/c.json', '', root), /the key "format" has no description/);
});

test('a path that is not in the schema fails instead of rendering nothing', () => {
  assert.throws(() => rowsOf('v9/a.json', 'card.nothing', root), /no key "card\.nothing"/);
});

test('settings read as the catalog names their types, and inline text escapes everything but code spans', () => {
  assert.equal(settingValue({ type: 'values', values: ['24h', '12h'] }), '`24h`, `12h`');
  assert.equal(settingValue({ type: 'onOff' }), 'true or false');
  assert.equal(settingDefault({ default: null }), '');
  assert.equal(settingDefault({ default: [] }), '');
  assert.equal(settingDefault({ default: '24h' }), '`"24h"`');
  assert.equal(inline('a <b> & `c<d>`'), 'a &lt;b&gt; &amp; <code>c&lt;d&gt;</code>');
});
