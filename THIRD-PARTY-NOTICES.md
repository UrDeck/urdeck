# Third-party notices

UrDeck includes or uses the following third-party material. The licences of UrDeck itself are in the
[README](README.md#license).

## Windows Runtime support code (C#/WinRT, in the `media` provider)

The `media` data provider (`providers/UrDeck.Providers.Media`) reads the Windows media session through the Windows Runtime. The
projection of the Windows API and the support code of Microsoft's C#/WinRT (package `Microsoft.Windows.CsWinRT` 2.1.6) are
compiled into the provider's assembly, so that it needs no runtime library from the host. C#/WinRT is under the MIT licence:

MIT License

Copyright (c) Microsoft Corporation.

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

## Lucide icons (the built-in `outline` set)

Used for the weather, shortcut and media glyphs of themes that name the `outline` icon set. The SVG files are copied unchanged from
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

## .NET runtime

The application is self-contained: the published folder holds the .NET runtime and its libraries (`coreclr.dll`, `System.*.dll`
and the others), under the MIT licence. Source: <https://github.com/dotnet/runtime>.

Copyright (c) .NET Foundation and Contributors

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

## Windows App SDK

The window, the controls and the composition come from the [Windows App SDK](https://github.com/microsoft/WindowsAppSDK),
whose runtime is part of the published folder (`Microsoft.*.dll`, `Microsoft.UI.Xaml`, `DWriteCore.dll`, `MRM.dll`,
`WebView2Loader.dll` and the others, with `onnxruntime.dll` and `DirectML.dll` from its machine-learning components). It is
Microsoft's software under the Microsoft Software License Terms for the Windows App SDK
(`licenses/WindowsAppSDK-LICENSE.txt`). The notices of the open source it includes are in
`licenses/WindowsAppSDK-NOTICE.txt`.

## SkiaSharp, Skia and Skottie

Drawing is done with [SkiaSharp](https://github.com/mono/SkiaSharp) and `SkiaSharp.Skottie` (Lottie playback), both under
the MIT licence, with the native Skia library (`libSkiaSharp.dll`, BSD 3-clause licence, Copyright 2006 Google Inc. and The
Skia Authors) and the ANGLE libraries (`libEGL.dll`, `libGLESv2.dll`, BSD licence) that the SkiaSharp view uses. The native
library incorporates further components (HarfBuzz, FreeType, libpng, expat and others); their licences are in
`licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt`.

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

## Velopack (installer and updates)

The installer, the updater (`Update.exe`) and the update check use [Velopack](https://github.com/velopack/velopack)
(`Velopack.dll` in the published folder), under the MIT licence:

Copyright (c) Velopack Ltd.

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

## Fonts (Inter, Instrument Sans, JetBrains Mono, Bricolage Grotesque, Bodoni Moda, Jost, Barlow Condensed, IBM Plex Mono, Archivo, Chakra Petch)

Ten font families ship with the built-in themes (seven variable, three static), each under the SIL Open Font License 1.1. The licence texts
are in `src/UrDeck.Engine/Themes/Builtin/` (`Inter-OFL.txt`, `InstrumentSans-OFL.txt`, `JetBrainsMono-OFL.txt`, `BricolageGrotesque-OFL.txt`, `BodoniModa-OFL.txt`, `Jost-OFL.txt`, `BarlowCondensed-OFL.txt`, `IBMPlexMono-OFL.txt`, `Archivo-OFL.txt`, `ChakraPetch-OFL.txt`) and are
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
| Archivo | Copyright 2020 The Archivo Project Authors (<https://github.com/Omnibus-Type/Archivo>) | 2.001, variable weight and width (Google Fonts release) | `pop` (all text, values at weight 900) |
| Chakra Petch | Copyright 2018 The Chakra Petch Project Authors (<https://github.com/m4rc1e/Chakra-Petch.git>) | 1.000, Bold only (Google Fonts release) | `rigline` (all text, slanted by the theme) |
