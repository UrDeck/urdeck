import assert from 'node:assert/strict';
import { test } from 'node:test';
import { INK, accentCss, contrast, siteAccent } from './accent.mjs';

const rgbOf = (hex) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));

test('an accent that already reads at 4.5:1 is unchanged', () => {
  for (const hex of ['#8ab8ff', '#ffd34d', '#4ff0c8', '#ffffff']) {
    const a = siteAccent(hex);
    assert.equal(a.accent, hex);
    assert.equal(a.lifted, false);
  }
});

test("Paper's accent already reads at 4.5:1 and is unchanged", () => {
  const a = siteAccent('#e5502b');
  assert.equal(a.lifted, false);
  assert.equal(a.accent, '#e5502b');
});

test("Pop's near-black accent is lifted to 4.5:1", () => {
  const a = siteAccent('#111111');
  assert.equal(a.lifted, true);
  assert.ok(contrast(rgbOf(a.accent), rgbOf(INK)) >= 4.5);
  assert.notEqual(a.hover, a.accent);
  const [r, g, b] = rgbOf(a.accent);
  assert.ok(r === g && g === b, `still a grey: ${a.accent}`);
});

test('hover is lighter than the accent', () => {
  const a = siteAccent('#111111');
  const sum = (hex) => rgbOf(hex).reduce((x, y) => x + y, 0);
  assert.ok(sum(a.hover) > sum(a.accent));
});

test('glass gets no rule, every other theme gets one', () => {
  const css = accentCss([
    { name: 'glass', accent: '#8ab8ff' },
    { name: 'paper', accent: '#e5502b' },
    { name: 'ember', accent: '#ffb347' },
  ]);
  assert.ok(!css.includes('"glass"'));
  assert.ok(css.includes(':root[data-theme="paper"]'));
  assert.ok(css.includes(':root[data-theme="ember"]'));
});
