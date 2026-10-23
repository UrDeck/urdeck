// The site's accent for a theme (spec "site-theme-picker", "A Choice Changes The Page"): the theme's accent, or, when it
// reads under 4.5:1 on the site's background, the same hue with its OKLCH lightness raised until it does.
export const INK = '#080918';
export const MIN_CONTRAST = 4.5;

const hexToRgb = (hex) => {
  const h = hex.replace('#', '');
  return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16));
};
const toLinear = (v) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4);
const fromLinear = (v) => (v <= 0.0031308 ? v * 12.92 : 1.055 * v ** (1 / 2.4) - 0.055);

/** WCAG relative luminance of an `[r, g, b]` (0-255). */
const luminance = ([r, g, b]) => 0.2126 * toLinear(r / 255) + 0.7152 * toLinear(g / 255) + 0.0722 * toLinear(b / 255);

export function contrast(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

function toOklch(rgb) {
  const [r, g, b] = rgb.map((v) => toLinear(v / 255));
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  const L = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s;
  const A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
  const B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
  return { l: L, c: Math.hypot(A, B), h: Math.atan2(B, A) };
}

/** Back to sRGB, keeping the lightness and hue and lowering chroma until the colour is inside the gamut. */
function fromOklch({ l, c, h }) {
  for (let chroma = c; ; chroma -= 0.002) {
    const A = Math.max(chroma, 0) * Math.cos(h);
    const B = Math.max(chroma, 0) * Math.sin(h);
    const lc = (l + 0.3963377774 * A + 0.2158037573 * B) ** 3;
    const mc = (l - 0.1055613458 * A - 0.0638541728 * B) ** 3;
    const sc = (l - 0.0894841775 * A - 1.291485548 * B) ** 3;
    const lin = [
      4.0767416621 * lc - 3.3077115913 * mc + 0.2309699292 * sc,
      -1.2684380046 * lc + 2.6097574011 * mc - 0.3413193965 * sc,
      -0.0041960863 * lc - 0.7034186147 * mc + 1.707614701 * sc,
    ];
    if (chroma <= 0 || lin.every((v) => v >= -0.0005 && v <= 1.0005)) {
      return lin.map((v) => Math.round(Math.min(Math.max(fromLinear(Math.min(Math.max(v, 0), 1)), 0), 1) * 255));
    }
  }
}

const hex = (rgb) => '#' + rgb.map((v) => v.toString(16).padStart(2, '0')).join('');

/** The accent for the site: `accent` (readable on `ink`), `hover` (lightness +0.08), and `rgb` for translucent glows. */
export function siteAccent(accentHex, inkHex = INK) {
  const ink = hexToRgb(inkHex);
  let rgb = hexToRgb(accentHex);
  const lch = toOklch(rgb);
  let lifted = false;
  for (let l = lch.l; contrast(rgb, ink) < MIN_CONTRAST && l < 1; ) {
    l = Math.min(1, +(l + 0.01).toFixed(2));
    rgb = fromOklch({ ...lch, l });
    lifted = true;
  }
  const base = lifted ? toOklch(rgb) : lch;
  const hover = fromOklch({ ...base, l: Math.min(1, base.l + 0.08) });
  return { accent: hex(rgb), hover: hex(hover), rgb, lifted };
}

/** `:root[data-theme="<name>"]` rules for the given `{ name, accent }` themes; glass is the site's own look and gets none. */
export function accentCss(themes) {
  return themes
    .filter((t) => t.name !== 'glass')
    .map((t) => {
      const a = siteAccent(t.accent);
      const rgb = a.rgb.join(' ');
      return `:root[data-theme="${t.name}"]{--accent:${a.accent};--accent-hover:${a.hover};--accent-rgb:${rgb};--glow-rgb:${rgb};--glow:rgb(${rgb} / 0.35)}`;
    })
    .join('\n');
}
