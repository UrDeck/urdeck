// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;

namespace UrDeck.Preview;

/// <summary>The sample page drawn in a theme, with the page's own renderer, so a picture of a theme is what the deck draws.</summary>
public static class ThemePreview
{
    /// <summary>
    /// Draws <paramref name="page"/> at <paramref name="width"/> by <paramref name="height"/> pixels at the page's fixed time.
    /// With <paramref name="dock"/> the dock's four slots and the page indicator are drawn as well.
    /// </summary>
    public static SKBitmap Render(LoadedTheme theme, SamplePage page, int width, int height, bool dock = false) =>
        PageRenderer.RenderToBitmap(
            width, height, page.Widgets, theme, SamplePage.Time,
            dock ? new PageRenderer.Chrome(IndicatorMode.Always, 0, 2) : null,
            dock ? page.Dock : null);
}
