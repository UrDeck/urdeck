import test from 'node:test';
import assert from 'node:assert/strict';
import { describeRelease, latestRelease, installerName, portableName } from './release.mjs';

const asset = (name) => ({ name, browser_download_url: `https://example.test/${name}` });
const stable = {
  tag_name: 'v0.2.0', draft: false, prerelease: false, published_at: '2026-11-01T10:00:00Z',
  html_url: 'https://github.com/UrDeck/urdeck/releases/tag/v0.2.0', assets: [asset(installerName), asset(portableName)],
};
const answer = (status, body) => async () => ({ ok: status < 300, status, json: async () => body });

test('a stable release is described', () => {
  assert.deepEqual(describeRelease(stable), {
    version: '0.2.0', date: '2026-11-01', notesUrl: stable.html_url, portableUrl: `https://example.test/${portableName}`,
  });
});

test('a prerelease or a draft is never chosen', () => {
  assert.equal(describeRelease({ ...stable, prerelease: true }), null);
  assert.equal(describeRelease({ ...stable, draft: true }), null);
});

test('a missing portable zip is only left out', () => {
  assert.equal(describeRelease({ ...stable, assets: [asset(installerName)] }).portableUrl, null);
});

test('a release without the installer fails', () => {
  assert.throws(() => describeRelease({ ...stable, assets: [asset(portableName)] }), /v0\.2\.0 has no UrDeck-win-Setup\.exe/);
});

test('no release yet, a rate limit and a network error give null', async () => {
  assert.equal(await latestRelease(answer(404, {}), ''), null);
  assert.equal(await latestRelease(answer(403, {}), ''), null);
  assert.equal(await latestRelease(async () => { throw new Error('offline'); }, ''), null);
});

test('the token is sent when there is one, and the answer is read', async () => {
  let seen;
  const release = await latestRelease(async (url, init) => { seen = init.headers; return { ok: true, status: 200, json: async () => stable }; }, 'tok');
  assert.equal(seen.authorization, 'Bearer tok');
  assert.equal(release.version, '0.2.0');
});
