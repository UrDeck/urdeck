// Renders the site's media with UrDeck itself, so the pictures can be rebuilt after the app's look changes.
//
//   npm run media                        everything it can
//   npm run media -- widgets             the widget pictures (src/assets/widgets/)
//   npm run media -- posters             the background posters (src/assets/backgrounds/)
//   npm run media -- loops               the loops (public/media/backgrounds/ and public/media/widgets/), needs ffmpeg on the PATH
//   npm run media -- widgets clock-4x2   only the named pictures / backgrounds
//
// Windows only: it builds the host and the tool, and renders with the host's --snapshot and `urdeck-cli shader render`.
// The pictures use the glass theme over the aurora background. Weather needs the network and the readings are this
// machine's: look at each picture before committing it (site/MEDIA-CHECKLIST.md).
import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const site = join(dirname(fileURLToPath(import.meta.url)), '..');
const repo = join(site, '..');
const cell = 275; // a grid cell on the 1100 px wide panel
const theme = 'glass';
const builtIn = join(repo, 'src', 'UrDeck.Engine', 'Backgrounds', 'Builtin');
const backgrounds = ['aurora', 'plasma', 'gradient', 'julia'];

const args = process.argv.slice(2);
const kinds = ['widgets', 'posters', 'loops'];
const wantedKinds = kinds.filter((k) => args.includes(k));
const run = wantedKinds.length > 0 ? wantedKinds : kinds;
const names = args.filter((a) => !kinds.includes(a));
const wanted = (name) => names.length === 0 || names.includes(name);

const stats4x4 = [
  { reading: 'system:cpu/load' }, { reading: 'system:gpu/temperature' },
  { reading: 'system:gpu/load' }, { reading: 'system:memory/load', label: 'RAM' },
  { reading: 'system:gpu/power' }, { reading: 'system:gpu/clock' }, { reading: 'system:cpu/core/1/load' },
];
const stats4x2 = [
  { reading: 'system:cpu/load', style: 'bar' }, { reading: 'system:memory/load', label: 'RAM', style: 'ring' },
  { reading: 'system:cpu/core/1/load' }, { reading: 'system:cpu/core/2/load' }, { reading: 'system:cpu/core/3/load' },
];
const shortcuts = [
  { typeId: 'urdeck.widgets.shortcut', target: 'https://github.com', label: 'GitHub' },
  { typeId: 'urdeck.widgets.shortcut', target: 'https://en.wikipedia.org', label: 'Wikipedia' },
  { typeId: 'urdeck.widgets.shortcut', target: 'C:\\Program Files (x86)\\Steam\\steam.exe', label: 'Steam' },
  { typeId: 'urdeck.widgets.shortcut', target: '%LOCALAPPDATA%\\Programs\\Microsoft VS Code\\Code.exe', label: 'VS Code' },
];

// One picture per widget size: the page holds only that widget, `cols` by `rows` cells at the top left. The picture is
// cropped to the card's edge (the gap the theme leaves around a card).
const widgetPictures = {
  // A loop starts a second and a half before a minute changes, so the digits flip a little after it begins.
  'clock-4x2': { cols: 4, rows: 2, widget: { typeId: 'urdeck.widgets.clock', format: '24h', showDate: true, style: 'flap' }, loop: { seconds: 3, beforeMinute: 1.5 } },
  'stats-4x4': { cols: 4, rows: 4, widget: { typeId: 'urdeck.widgets.stats', slots: stats4x4 } },
  'stats-4x2': { cols: 4, rows: 2, widget: { typeId: 'urdeck.widgets.stats', slots: stats4x2 } },
  'weather-4x4': { cols: 4, rows: 4, widget: { typeId: 'urdeck.widgets.weather', location: 'Zurich', unit: 'celsius', days: 5, motion: 'full', animateForecast: true }, loop: { seconds: 4 } },
  'weather-4x2': { cols: 4, rows: 2, widget: { typeId: 'urdeck.widgets.weather', location: 'Zurich', unit: 'celsius', motion: 'full' }, loop: { seconds: 4 } },
  // The dock is the one place 1x1 shortcuts sit together: the page is empty and the picture is cut to the dock.
  'shortcut-1x1': { cols: 4, rows: 1, dock: shortcuts, crop: { width: 800, height: 200, fromBottom: 0 } },
};

const fps = 30;

const pad = (n) => String(n).padStart(2, '0');
// A local date and time as the host reads it (--start), with milliseconds.
const localIso = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}.${String(d.getMilliseconds()).padStart(3, '0')}`;

// Frames f0000.png ... in `dir`, played forwards and then backwards (the turning frames not repeated), so a loop has no
// seam, encoded as H.264 without audio.
function encodeLoop(dir, frames, file, filters = []) {
  for (let i = 0; i < frames - 2; i++) {
    cpSync(join(dir, `f${String(frames - 2 - i).padStart(4, '0')}.png`), join(dir, `f${String(frames + i).padStart(4, '0')}.png`));
  }
  exe('ffmpeg', ['-y', '-v', 'error', '-framerate', String(fps), '-i', join(dir, 'f%04d.png'), ...filters, '-c:v', 'libx264', '-pix_fmt', 'yuv420p',
    '-crf', '28', '-movflags', '+faststart', '-an', file]);
}

const exe = (cmd, a, options = {}) => {
  const r = spawnSync(cmd, a, { stdio: 'inherit', ...options });
  if (r.status !== 0) throw new Error(`${cmd} ${a.join(' ')} failed with ${r.status}`);
};

function build(project) {
  console.log(`Building ${project}`);
  exe('dotnet', ['build', join(repo, project), '-c', 'Release', '-v', 'q', '--nologo']);
}

function hasFfmpeg() {
  return spawnSync('ffmpeg', ['-version'], { stdio: 'ignore' }).status === 0;
}

async function renderWidgets(mode) {
  // `pictures` renders every wanted widget's still; `loops` the loop of those that have one (and its first frame as the still).
  const wantedPictures = Object.keys(widgetPictures).filter((n) => wanted(n) && (mode === 'pictures' || widgetPictures[n].loop));
  if (wantedPictures.length === 0) return;
  if (mode === 'loops' && !hasFfmpeg()) {
    console.error('Loops need ffmpeg on the PATH (winget install Gyan.FFmpeg). Skipped.');
    return;
  }
  build('src/UrDeck.Host');
  const bin = join(repo, 'src', 'UrDeck.Host', 'bin', 'Release', 'net10.0-windows10.0.19041.0');
  // A copy of the build output: --snapshot reads the config next to the executable, and a build replaces the one in bin/.
  const stage = mkdtempSync(join(tmpdir(), 'urdeck-site-media-'));
  try {
    cpSync(bin, stage, { recursive: true, filter: (src) => !/[\\/](cache|urdeck\.log(\.1)?)$/.test(src) });
    const out = join(site, 'src', 'assets', 'widgets');
    mkdirSync(out, { recursive: true });
    for (const name of wantedPictures) {
      const p = widgetPictures[name];
      const config = {
        pages: [{ name: 'Media', widgets: p.widget ? [{ ...p.widget, col: 0, row: 0, width: p.cols, height: p.rows }] : [] }],
        activePage: 0,
        theme,
        background: { type: 'shader', name: 'aurora' },
        monitorName: 'tallest',
      };
      if (p.dock) config.dock = p.dock;
      writeFileSync(join(stage, 'urdeck-config.json'), JSON.stringify(config, null, 2));
      const width = p.cols * cell;
      const height = p.rows * cell;
      if (mode === 'pictures') {
        const raw = join(stage, `${name}.raw.png`);
        console.log(`Rendering ${name}`);
        exe(join(stage, 'UrDeck.Host.exe'), ['--snapshot', raw, '--size', `${width}x${height}`]);
        await cropAndSave(raw, join(out, `${name}.png`), p, width, height);
        continue;
      }

      // The widgets draw from the time they are given, so a loop is the host asked for N frames of time.
      const frames = p.loop.seconds * fps;
      const start = new Date();
      if (p.loop.beforeMinute) {
        start.setSeconds(0, 0);
        start.setMinutes(start.getMinutes() + 1);
        start.setTime(start.getTime() - p.loop.beforeMinute * 1000);
      }
      const raw = join(stage, `${name}-frames`);
      console.log(`Loop ${name}: ${frames} frames`);
      exe(join(stage, 'UrDeck.Host.exe'), ['--snapshot', raw, '--size', `${width}x${height}`, '--frames', String(frames), '--fps', String(fps), '--start', localIso(start)]);
      const cropped = join(stage, `${name}-cropped`);
      mkdirSync(cropped);
      for (let i = 0; i < frames; i++) {
        const f = `frame-${String(i).padStart(4, '0')}.png`;
        await cropAndSave(join(raw, f), join(cropped, `f${String(i).padStart(4, '0')}.png`), p, width, height);
      }
      // The picture is the loop's first frame, so the page does not jump when the loop starts.
      cpSync(join(cropped, 'f0000.png'), join(out, `${name}.png`));
      const target = join(site, 'public', 'media', 'widgets');
      mkdirSync(target, { recursive: true });
      encodeLoop(cropped, frames, join(target, `${name}.mp4`), ['-vf', 'scale=800:-2']);
    }
  } finally {
    rmSync(stage, { recursive: true, force: true });
  }
}

// The theme leaves half a gap around a card (8 px at this size); cut it off so the picture ends at the card.
async function cropAndSave(raw, file, p, width, height) {
  const gap = 8;
  const box = p.crop
    ? { left: Math.round((width - p.crop.width) / 2), top: height - p.crop.height - p.crop.fromBottom, width: p.crop.width, height: p.crop.height }
    : { left: gap, top: gap, width: width - 2 * gap, height: height - 2 * gap };
  await sharp(raw).extract(box).png({ compressionLevel: 9 }).toFile(file);
}

function renderBackground(name, file, size, time) {
  exe('dotnet', [join(repo, 'tools', 'UrDeck.Cli', 'bin', 'Release', 'net10.0', 'urdeck-cli.dll'),
    'shader', 'render', join(builtIn, name), '-o', file, '--size', size, '--time', String(time), '--theme', theme, '--fail-on-warnings'],
  { stdio: ['ignore', 'ignore', 'inherit'] });
}

function renderPosters() {
  const list = backgrounds.filter(wanted);
  if (list.length === 0) return;
  build('tools/UrDeck.Cli');
  const out = join(site, 'src', 'assets', 'backgrounds');
  mkdirSync(out, { recursive: true });
  for (const name of list) {
    console.log(`Poster ${name}`);
    renderBackground(name, join(out, `${name}.png`), '540x960', 10);
  }
}

// A loop is the shader played forwards and then backwards, so it needs no seam. Frames are 30 per second.
function renderLoops() {
  const list = backgrounds.filter(wanted);
  if (list.length === 0) return;
  if (!hasFfmpeg()) {
    console.error('Loops need ffmpeg on the PATH (winget install Gyan.FFmpeg). Skipped.');
    return;
  }
  build('tools/UrDeck.Cli');
  const out = join(site, 'public', 'media', 'backgrounds');
  mkdirSync(out, { recursive: true });
  const frames = 3 * fps;
  for (const name of list) {
    const dir = mkdtempSync(join(tmpdir(), `urdeck-loop-${name}-`));
    try {
      console.log(`Loop ${name}: ${frames} frames`);
      for (let i = 0; i < frames; i++) renderBackground(name, join(dir, `f${String(i).padStart(4, '0')}.png`), '540x960', (i / fps).toFixed(4));
      encodeLoop(dir, frames, join(out, `${name}.mp4`));
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }
}

if (process.platform !== 'win32') {
  console.error('npm run media renders with the Windows host and runs on Windows only.');
  process.exit(1);
}

try {
  if (run.includes('widgets')) await renderWidgets('pictures');
  if (run.includes('posters')) renderPosters();
  if (run.includes('loops')) {
    await renderWidgets('loops');
    renderLoops();
  }
} catch (e) {
  console.error(String(e.message ?? e));
  process.exit(1);
}
