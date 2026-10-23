// Renders the site's media with UrDeck itself: the pictures and loops of every theme, widget size and background, from the
// product's catalog, into site/src/generated/ and site/public/generated/ (see site/README.md, "Media").
//
//   npm run media                          render what is missing or stale
//   npm run media -- --status              say what is missing, stale and unneeded; render nothing
//   npm run media -- --all                 render everything, keys or not
//   npm run media -- decks|widgets|backgrounds [name...]   only those kinds / names (carbon, clock-4x2, aurora, clock)
//   npm run media -- --report file.md      also write the added / changed / removed lists (the pull request's body)
//
// Needs .NET (the tool is built and run from source), Node and, for loops, ffmpeg on the PATH. Linux and Windows.
// With --status and no .NET it reads the committed catalog.json and says so.
import { existsSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs, run } from './media/lib.mjs';
import { createRenderer, hasDotnet, hasFfmpeg } from './media/renderer.mjs';

const site = join(dirname(fileURLToPath(import.meta.url)), '..');
const repo = join(site, '..');

try {
  const opts = parseArgs(process.argv.slice(2));
  const committed = join(site, 'src', 'generated', 'catalog.json');
  let argv = process.argv.slice(2);
  let canRender = hasDotnet();
  if (!canRender) {
    if (!opts.status || !existsSync(committed)) throw new Error('npm run media needs .NET (dotnet on the PATH).');
    console.log('No .NET here: the report uses the committed site/src/generated/catalog.json and cannot tell which entries move.');
    argv = [...argv, '--catalog', committed];
  }
  if (!opts.status && !hasFfmpeg()) throw new Error('npm run media needs ffmpeg on the PATH (apt install ffmpeg, winget install Gyan.FFmpeg).');
  mkdirSync(join(site, 'src', 'generated'), { recursive: true });
  await run({ repo, site, renderer: createRenderer({ repo, site, canRender }), argv });
} catch (e) {
  console.error(String(e.message ?? e));
  process.exit(1);
}
