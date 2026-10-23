// The visitor's theme, decided in <head> before the first paint (spec "site-theme-picker"). Inlined by Base.astro with
// `define:vars` (names: the themes that have media, titles: name -> display name). Plain script, no module: it must run
// before the page is parsed, and must never throw.
var root = document.documentElement;
var key = 'urdeck-theme';
var reduced = false;
try { reduced = matchMedia('(prefers-reduced-motion: reduce)').matches; } catch (e) {}
var listeners = [];

function valid(name) { return typeof name === 'string' && names.indexOf(name) > -1; }
function save(name) { try { localStorage.setItem(key, name); } catch (e) {} }

var current = 'glass';
var asked = null;
try { asked = new URLSearchParams(location.search).get('theme'); } catch (e) {}
var stored = null;
try { stored = localStorage.getItem(key); } catch (e) {}
if (valid(asked)) { current = asked; save(asked); }
else if (valid(stored)) current = stored;
root.dataset.theme = current;

// Point one slot at a theme's file. A loop that was playing plays on, a paused one stays paused, and with reduced motion
// nothing is ever started. A theme with a loop where glass has a picture (or the other way round) replaces the element.
function build(old, v) {
  var el;
  var alt = old.getAttribute('alt') || old.getAttribute('aria-label') || '';
  if (v.poster) {
    el = document.createElement('video');
    el.muted = true;
    el.loop = true;
    el.setAttribute('playsinline', '');
    el.preload = old.hasAttribute('data-autoplay') ? 'auto' : 'none';
    el.setAttribute('aria-label', alt);
    el.poster = v.poster;
  } else {
    el = document.createElement('img');
    el.alt = alt;
    if (old.dataset.sizes) el.sizes = old.dataset.sizes;
    if (v.srcset) el.srcset = v.srcset;
    el.decoding = 'async';
  }
  ['width', 'height', 'class', 'data-slot', 'data-variants', 'data-sizes', 'data-autoplay'].forEach(function (name) {
    if (old.hasAttribute(name)) el.setAttribute(name, old.getAttribute(name));
  });
  el.src = v.src;
  old.replaceWith(el);
  return el;
}

function swap(el, theme) {
  var variants;
  try { variants = JSON.parse(el.getAttribute('data-variants')); } catch (e) { return; }
  var v = variants && (variants[theme] || variants.glass);
  if (!v || el.getAttribute('src') === v.src) return;
  var wasVideo = el.tagName === 'VIDEO';
  var playing = wasVideo && !el.paused && !el.ended;
  if (wasVideo !== Boolean(v.poster)) {
    el = build(el, v);
    if (v.poster && el.hasAttribute('data-autoplay') && !reduced) { var q = el.play(); if (q && q.catch) q.catch(function () {}); }
    return;
  }
  if (wasVideo) {
    el.poster = v.poster;
    el.src = v.src;
    if (playing && !reduced) { var p = el.play(); if (p && p.catch) p.catch(function () {}); }
  } else {
    if (v.srcset) el.srcset = v.srcset;
    el.src = v.src;
  }
}

function swapAll(theme) {
  var slots = document.querySelectorAll('[data-slot]');
  for (var i = 0; i < slots.length; i++) swap(slots[i], theme);
}

window.urdeckTheme = {
  names: names,
  titles: titles,
  get current() { return current; },
  swap: function (el) { swap(el, current); },
  set: function (name) {
    if (!valid(name)) return;
    current = name;
    root.dataset.theme = name;
    save(name);
    swapAll(name);
    document.dispatchEvent(new CustomEvent('urdeck:theme', { detail: { theme: name } }));
  },
  // Calls back now with the chosen theme, and again on every choice.
  on: function (callback) {
    callback(current);
    document.addEventListener('urdeck:theme', function (event) { callback(event.detail.theme); });
  },
};

// The slots that are not yet fetched (lazy) are swapped once they are parsed; the hero is swapped by an inline call right
// after its element.
if (current !== 'glass') document.addEventListener('DOMContentLoaded', function () { swapAll(current); });
