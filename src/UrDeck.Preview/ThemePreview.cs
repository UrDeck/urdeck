// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Preview;

/// <summary>The sample page drawn in a theme with no background, as the picture tests and <c>theme render</c> draw it.</summary>
public static class ThemePreview
{
    private static readonly PreviewRenderer Renderer = new();

    /// <summary>
    /// Draws the sample page at <paramref name="width"/> by <paramref name="height"/> pixels at its moment. With
    /// <paramref name="dock"/> the page is drawn as the deck shows it: with the dock's four slots and the page indicator.
    /// </summary>
    public static SKBitmap Render(LoadedTheme theme, int width, int height, bool dock = false) =>
        Renderer.Render(new PreviewRequest
        {
            Theme = theme,
            Background = PreviewBackground.None,
            Width = width,
            Height = height,
            WithDock = dock,
        });
}
