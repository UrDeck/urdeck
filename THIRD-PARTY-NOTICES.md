# Third-party notices

UrDeck includes or uses the following third-party material. The licences of UrDeck itself are in the
[README](README.md#license).

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
