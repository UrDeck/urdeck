// The real renderer for lib.mjs: `urdeck-cli preview render` (sample data, no host, no network), sharp for the pictures
// (lossless WebP) and ffmpeg for the loops (H.264, forwards then backwards). Runs on Linux and on Windows.
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import sharp from 'sharp';
import { COOKBOOK_SIZE, DECK_LOOP_SIZE, DECK_SIZE, FPS, LOOP_SECONDS, POSTER_SIZE, SAMPLE_MOMENT, addSeconds, deckConfigPath } from './lib.mjs';

const MB = 1024 * 1024;
// 'Media Limits': the most a loop of each kind may weigh, and how wide it may be.
const LIMITS = {
  decks: { bytes: 1.5 * MB, width: 540 },
  widgets: { bytes: 1 * MB, width: 800 },
  backgrounds: { bytes: 1 * MB, width: 540 },
};
const FRAMES = LOOP_SECONDS * FPS;

// Runs a command without blocking, so several renders can go at once; rejects with its output when it fails.
function exe(cmd, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { stdio: ['ignore', 'pipe', 'pipe'] });
    let out = '';
    let err = '';
    child.stdout.on('data', (d) => { out += d; });
    child.stderr.on('data', (d) => { err += d; });
    child.on('error', reject);
    child.on('close', (code) => (code === 0 ? resolve(out) : reject(new Error(`${cmd} ${args.join(' ')} failed (${code}):\n${err}${out}`))));
  });
}

export const hasFfmpeg = () => spawnSync('ffmpeg', ['-version'], { stdio: 'ignore' }).status === 0;
export const hasDotnet = () => spawnSync('dotnet', ['--version'], { stdio: 'ignore' }).status === 0;

export function createRenderer({ repo, site, canRender = true }) {
  const dll = join(repo, 'tools', 'UrDeck.Cli', 'bin', 'Release', 'net10.0', 'urdeck-cli.dll');
  let building = null;
  const tool = async (args) => {
    building ??= (console.log('Building urdeck-cli'), exe('dotnet', ['build', join(repo, 'tools', 'UrDeck.Cli'), '-c', 'Release', '-v', 'q', '--nologo']));
    await building;
    return exe('dotnet', [dll, ...args]);
  };

  // One widget at a size, with the site's settings for it (showcase/widgets.json) when it has any; `dir` holds the file written for them.
  function widgetArgs(entry, dir, moment) {
    const [cols, rows] = entry.size.split('x').map(Number);
    const args = ['--widget', entry.widget, '--widget-size', entry.size, '--time', moment ?? addSeconds(SAMPLE_MOMENT, entry.loopStart)];
    if (Object.keys(entry.config).length > 0) {
      mkdirSync(dir, { recursive: true });
      const config = join(dir, 'widget-config.json');
      writeFileSync(config, JSON.stringify({ pages: [{ name: 'Media', widgets: [{ typeId: entry.widget, col: 0, row: 0, width: cols, height: rows, ...entry.config }] }], activePage: 0 }));
      args.push('--config', config);
    }
    return args;
  }

  // The arguments of `preview render` for an entry, besides the output; `dir` holds the configuration files it writes.
  function previewArgs(entry, dir, { moment, size } = {}) {
    if (entry.kind === 'cookbook') {
      // The recipe's own folder is the theme (`--theme-dir`); it shows the page, the dock or one widget, over its background or none.
      const base = ['preview', 'render', '--theme-dir', join(site, 'cookbook', entry.name), '--background', entry.background ?? 'none'];
      if (entry.show === 'widget') return [...base, ...widgetArgs(entry, dir, moment), '--width', '1100'];
      return [...base, ...(entry.show === 'dock' ? ['--page', 'dock'] : []), '--size', COOKBOOK_SIZE];
    }
    const common = ['--theme', entry.theme];
    if (entry.kind === 'decks') {
      return ['preview', 'render', '--config', deckConfigPath(site, entry), ...common, '--background', 'theme', '--size', size ?? DECK_SIZE, '--page', 'dock'];
    }
    if (entry.kind === 'widgets') {
      return ['preview', 'render', ...widgetArgs(entry, dir, moment), '--width', '1100', ...common, '--background', 'theme'];
    }
    mkdirSync(dir, { recursive: true });
    const config = join(dir, 'empty-page.json');
    writeFileSync(config, JSON.stringify({ pages: [{ name: 'Media', widgets: [] }], activePage: 0 }));
    return ['preview', 'render', '--config', config, '--page', '0', ...common, '--background', entry.background, '--size', POSTER_SIZE];
  }

  const frames = async (entry, dir, count, fps, extra = [], options = {}) => {
    mkdirSync(dir, { recursive: true });
    const out = join(dir, 'frames');
    await tool([...previewArgs(entry, dir, options), '--frames', String(count), '--fps', String(fps), ...extra, '-o', out]);
    return out;
  };
  const frame = (dir, i) => join(dir, `frame-${String(i).padStart(4, '0')}.png`);

  return {
    canRender,

    async catalog() {
      const out = join(site, 'src', 'generated');
      mkdirSync(out, { recursive: true });
      const file = join(out, 'catalog.json');
      await tool(['catalog', 'export', '-o', file]);
      return JSON.parse(readFileSync(file, 'utf8'));
    },

    // Whether the media of an entry move by themselves, apart from the background (the deck: the theme or its background).
    // A widget: a few seconds of frames at motion off (the background and borders still), any of which differs from the
    // first. A deck: the second frame at full motion and at off differ.
    async moves(entry) {
      if (entry.kind === 'backgrounds') return true;
      mkdirSync(join(site, 'src', 'generated'), { recursive: true });
      const dir = mkdtempSync(join(site, 'src', 'generated', '.probe-'));
      try {
        if (entry.kind === 'decks') {
          // Frames, because a still drawing never moves the background or a border: the second of two frames, 2 s on.
          const [full, off] = await Promise.all(['full', 'off'].map(async (motion) => readFileSync(frame(await frames(entry, join(dir, motion), 2, 0.5, ['--motion', motion], { size: DECK_LOOP_SIZE }), 1))));
          return !full.equals(off);
        }
        const out = await frames(entry, dir, 30, 10, ['--motion', 'off']);
        const first = readFileSync(frame(out, 0));
        for (let i = 1; i < 30; i++) if (!readFileSync(frame(out, i)).equals(first)) return true;
        return false;
      } finally {
        rmSync(dir, { recursive: true, force: true });
      }
    },

    async render(entry, dest, work) {
      const limit = LIMITS[entry.kind];
      const png = join(work, 'picture.png');
      mkdirSync(work, { recursive: true });
      if (!dest.loop) {
        await tool([...previewArgs(entry, work), '-o', png]);
        await toWebp(png, dest.picture, entry);
        return;
      }
      let t = Date.now();
      const lap = (what) => { console.log(`    ${what} ${((Date.now() - t) / 1000).toFixed(1)} s`); t = Date.now(); };
      // A deck's loop is drawn at the size it ships at, its picture at full size; the other loops start from the picture's own frame.
      const small = entry.kind === 'decks';
      if (small) await tool([...previewArgs(entry, work), '-o', png]);
      const out = await frames(entry, work, FRAMES, FPS, [], small ? { size: DECK_LOOP_SIZE } : {});
      lap('frames');
      // The picture is the loop's first frame, so the page does not jump when the loop starts.
      await toWebp(small ? png : frame(out, 0), dest.picture, entry);
      lap('webp');
      await encode(out, dest.loop, entry, limit);
      lap('encode');
    },
  };
}

async function toWebp(png, file, entry) {
  const info = await sharp(png).metadata();
  const expected = entry.kind === 'decks' ? DECK_SIZE : entry.kind === 'backgrounds' ? POSTER_SIZE : null;
  if (expected && `${info.width}x${info.height}` !== expected) {
    throw new Error(`${entry.picture} is ${info.width}x${info.height}, the limit is exactly ${expected}`);
  }
  await sharp(png).webp({ lossless: true, effort: 6 }).toFile(file);
}

// Frames 0..N-1 then N-2..1, so the loop has no seam; a concat list names them, nothing is copied.
async function encode(dir, file, entry, limit) {
  const order = [...Array(FRAMES).keys(), ...[...Array(FRAMES - 2).keys()].map((i) => FRAMES - 2 - i)];
  const seconds = order.length / FPS;
  if (seconds < 4 || seconds > 6) throw new Error(`${entry.loop} would last ${seconds.toFixed(1)} s, the limit is 4 to 6 s`);
  const list = join(dir, 'list.txt');
  const name = (i) => `file '${join(dir, `frame-${String(i).padStart(4, '0')}.png`).replaceAll('\\', '/')}'\nduration ${(1 / FPS).toFixed(6)}`;
  writeFileSync(list, order.map(name).join('\n') + '\n');
  const filter = entry.kind === 'backgrounds' ? [] : ['-vf', `scale='trunc(min(${limit.width},iw)/2)*2':-2`];
  let size = 0;
  for (const crf of [28, 32]) {
    await exe('ffmpeg', ['-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', list, '-r', String(FPS), ...filter,
      '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', String(crf), '-movflags', '+faststart', '-an', file]);
    size = statSync(file).size;
    if (size <= limit.bytes) return;
  }
  throw new Error(`${entry.loop} is ${(size / MB).toFixed(2)} MB at crf 32, the limit is ${(limit.bytes / MB).toFixed(1)} MB`);
}

