Three keys make an LED meter out of a plain bar.

- `gauge.style` `bar` is what a widget gets when it asks for "the theme's gauge" and nothing more specific. A widget that sets its
  own `style` on a slot still wins.
- `gauge.segments` cuts the fill into blocks: `length` along the bar (a fraction of a cell, for a ring the arc length in cells)
  and `gap` between blocks. The track is cut the same way, so unlit blocks show.
- `gauge.colorBy` `ramp` colours the fill by its value along `colors.ramp` instead of the accent, warning and critical steps. The
  ramp has two to five colours from low to high; the default is good, warning, critical.

`stroke.thickness` makes the bar taller and `stroke.cap` `square` gives the line blunt ends. `gauge.track.color` is the unlit block
colour. Add `gauge.valueTint` `true` to colour the number like the fill. For a zone that lights in a colour of its own, see
[the tach recipe](/docs/cookbook/tach-redline/).
