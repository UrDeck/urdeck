# Third-party notices

UrDeck includes or uses the following third-party material. The licences of UrDeck itself are in the
[README](README.md#license).

## Lucide icons (the built-in `outline` set)

Used for the weather and shortcut glyphs of themes that name the `outline` icon set. The SVG files are copied unchanged from
the `lucide-static` package (release 1.60.0) into `src/UrDeck.Engine/Icons/Builtin/outline/` and embedded in the engine, with the
full licence text (`LICENSE-lucide.txt`, which also lists the icons derived from Feather). The running application draws no credit.
Source: <https://lucide.dev>.

ISC License

Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

The icons derived from Feather (listed in the licence file in the set) are under the MIT licence:

The MIT License (MIT) (for the icons listed above)

Copyright (c) 2013-present Cole Bemis

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Meteocons (weather icons)

Used by the weather widget. The Lottie animations (the `fill` style of `@meteocons/lottie`) are embedded in
`UrDeck.Widgets.Weather`, and the licence text ships with them
(`widgets/UrDeck.Widgets.Weather/Icons/Meteocons-LICENSE.txt`). Source: <https://github.com/basmilius/weather-icons>.

MIT License

Copyright (c) 2020-present Bas Milius

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Open-Meteo (weather data)

Weather data by [Open-Meteo.com](https://open-meteo.com/), offered under the
[Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/) licence
(<https://open-meteo.com/en/licence>). The data is converted before it is shown (temperatures to the display unit, weather
codes to condition classes, instants to the place's local time). The weather widget and every widget that shows a weather
reading draw the credit `Weather data by Open-Meteo.com`.

The free service is for non-commercial use (<https://open-meteo.com/en/terms>); commercial use needs an Open-Meteo
subscription.

## GeoNames (place search)

Places are found with Open-Meteo's geocoding service, whose location database is from
[GeoNames](https://www.geonames.org/), licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).

## SkiaSharp and Skottie

Drawing is done with [SkiaSharp](https://github.com/mono/SkiaSharp) and `SkiaSharp.Skottie` (Lottie playback), both under
the MIT licence:

Copyright (c) 2015-2016 Xamarin, Inc.
Copyright (c) 2017-2018 Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Fonts (Inter, Instrument Sans, JetBrains Mono, Bricolage Grotesque, Bodoni Moda, Jost, Barlow Condensed, IBM Plex Mono)

Eight font families ship with the built-in themes (six variable, two static), each under the SIL Open Font License 1.1. The licence texts
are in `src/UrDeck.Engine/Themes/Builtin/` (`Inter-OFL.txt`, `InstrumentSans-OFL.txt`, `JetBrainsMono-OFL.txt`, `BricolageGrotesque-OFL.txt`, `BodoniModa-OFL.txt`, `Jost-OFL.txt`, `BarlowCondensed-OFL.txt`, `IBMPlexMono-OFL.txt`) and are
embedded with the fonts.

| Family | Copyright | Version | Used by |
|---|---|---|---|
| Inter | Copyright (c) 2016 The Inter Project Authors (<https://github.com/rsms/inter>) | `InterVariable.ttf` | `default-dark`, `default-light`, `glass` |
| Instrument Sans | Copyright 2022 The Instrument Sans Project Authors (<https://github.com/Instrument/instrument-sans>) | 1.000 (Google Fonts release) | `carbon` (body text), `paper` (values, body text) |
| JetBrains Mono | Copyright 2020 The JetBrains Mono Project Authors (<https://github.com/JetBrains/JetBrainsMono>) | 2.211 (Google Fonts release) | `carbon` (values, units, labels), `paper` (units, labels) |
| Bricolage Grotesque | Copyright 2022 The Bricolage Grotesque Project Authors (<https://github.com/ateliertriay/bricolage>) | 1.001 (Google Fonts release) | `prism` (all text) |
| Bodoni Moda | Copyright 2020 The Bodoni Moda Project Authors (<https://github.com/indestructible-type/Bodoni>) | 2.005 (Google Fonts release) | `obsidian` (values) |
| Jost | Copyright 2020 The Jost Project Authors (<https://github.com/indestructible-type/Jost>) | 3.710 (Google Fonts release) | `obsidian` (units, labels, body text) |
| Barlow Condensed | Copyright 2017 The Barlow Project Authors (<https://github.com/jpt/barlow>) | 1.408, Bold only (Google Fonts release) | `ember` (all text) |
| IBM Plex Mono | Copyright (c) 2017 IBM Corp. with Reserved Font Name "Plex" (<https://github.com/IBM/plex>) | 2.3, Regular only (Google Fonts release) | `blueprint` (all text) |
