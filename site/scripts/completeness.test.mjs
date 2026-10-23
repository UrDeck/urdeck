// The completeness check on a fixture catalog, with a deliberate gap in each place it looks. `npm test`.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { completenessProblems } from './completeness.mjs';

const catalog = () => ({
  themes: [{ name: 'carbon' }, { name: 'paper' }],
  backgrounds: [{ name: 'aurora' }],
  widgets: [
    { typeId: 'urdeck.widgets.clock', settings: [{ name: 'format' }, { name: 'style' }] },
    { typeId: 'urdeck.widgets.shortcut', settings: [{ name: 'target' }] },
  ],
  findings: [{ code: 'THEME001' }, { code: 'SHADER001' }],
});
const schemas = [{ name: 'v1/theme.json', paths: ['card', 'card.radius', 'colors'] }];
const records = () => [
  { kind: 'schema', schema: 'v1/theme.json', paths: ['card', 'card.radius'] },
  { kind: 'schema', schema: 'v1/theme.json', paths: ['colors'] },
  { kind: 'builtin', what: 'themes', names: ['carbon', 'paper'] },
  { kind: 'builtin', what: 'backgrounds', names: ['aurora'] },
  { kind: 'builtin', what: 'widgets', names: ['urdeck.widgets.clock', 'urdeck.widgets.shortcut'] },
  { kind: 'settings', widget: 'urdeck.widgets.clock', names: ['format', 'style'] },
  { kind: 'settings', widget: 'urdeck.widgets.shortcut', names: ['target'] },
  { kind: 'findings', codes: ['THEME001', 'SHADER001'] },
];
const check = (over = {}) => completenessProblems({ catalog: catalog(), schemas, records: records(), troubleshooting: 'THEME001 SHADER001', ...over });

test('a documentation that shows everything has no problem', () => {
  assert.deepEqual(check(), []);
});

test('a widget the catalog has and no page lists fails, naming it', () => {
  const c = catalog();
  c.widgets.push({ typeId: 'urdeck.widgets.notes', settings: [{ name: 'text' }] });
  const problems = check({ catalog: c });
  assert.equal(problems.length, 2);
  assert.match(problems[0], /widget "urdeck\.widgets\.notes" is on no documentation page/);
  assert.match(problems[1], /urdeck\.widgets\.notes has no settings table/);
});

test('a theme, a background, a setting, a finding code and a schema key that no page shows each fail', () => {
  const c = catalog();
  c.themes.push({ name: 'neon' });
  c.backgrounds.push({ name: 'waves' });
  c.widgets[0].settings.push({ name: 'showSeconds' });
  c.findings.push({ code: 'THEME999' });
  const problems = check({
    catalog: c,
    schemas: [{ name: 'v1/theme.json', paths: ['card', 'card.radius', 'colors', 'card.cut'] }],
  });
  const text = problems.join('\n');
  assert.match(text, /theme "neon"/);
  assert.match(text, /background "waves"/);
  assert.match(text, /"showSeconds" of urdeck\.widgets\.clock/);
  assert.match(text, /THEME999 is in no findings table/);
  assert.match(text, /v1\/theme\.json: no documentation page shows 1 key\(s\): card\.cut/);
});

test('a finding that is in a table but not on the troubleshooting page fails', () => {
  assert.match(check({ troubleshooting: 'THEME001' }).join(), /SHADER001 does not appear on the troubleshooting page/);
});

test('no record at all is a problem of its own', () => {
  assert.match(check({ records: null })[0], /no record/);
});
