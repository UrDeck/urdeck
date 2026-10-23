`gauge.style` `tach` is an arc with ticks, a redline and a needle. The other keys decide how much of that you see.

- `gauge.sweep` is the arc: `start` in degrees (0 is 3 o'clock, clockwise) and `span`, how many degrees it covers. 135 and 270
  are the ring's own; a smaller span gives a speedometer.
- `gauge.ticks` sets the marks: `count` of them, every `majorEvery`-th one longer. A tach has 28 ticks and a redline from 0.85
  even if the theme sets none; writing the group is for changing them.
- `gauge.redline.from` is where the zone starts, as a fraction of the scale. With `lightsAbove` the zone lights only while the
  value is inside it, so it is dark most of the time and a warning when it matters. `glow` blurs a copy of it.
- `gauge.marker` `needle` draws the pointer; the other shape is a `diamond`.

The needle's `color` and `colors.accent` are the same amber so the dial reads as one object. `colors.critical` is the colour a
gauge turns once a reading passes its critical level, which a slot's `critical` setting decides.
