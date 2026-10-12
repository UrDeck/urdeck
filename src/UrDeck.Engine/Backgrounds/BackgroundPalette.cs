// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Backgrounds;

/// <summary>The four opaque colours every shader receives as <c>iColor0</c> to <c>iColor3</c>.</summary>
public static class BackgroundPalette
{
    public const int Size = 4;

    /// <summary>Shares of the way from the theme's background to its accent, for a theme without a palette of its own.</summary>
    private static readonly float[] DerivedMix = [0f, 0.2f, 0.4f, 0.6f];

    /// <summary>
    /// The configuration's colours, else the theme's <c>colors.backgroundPalette</c>, else colours derived from the theme's
    /// background and accent. Fewer than four repeat the last one; alpha is ignored. <paramref name="configColors"/> is
    /// expected to be valid (see <see cref="BackgroundSettings"/>); an invalid entry is skipped.
    /// </summary>
    public static SKColor[] Resolve(IReadOnlyList<string>? configColors, LoadedTheme theme)
    {
        var colors = Parse(configColors);
        if (colors.Count == 0)
            colors = Parse(theme.Definition.Colors?.BackgroundPalette);
        if (colors.Count == 0)
            colors = Derive(theme);

        var result = new SKColor[Size];
        for (int i = 0; i < Size; i++)
            result[i] = colors[Math.Min(i, colors.Count - 1)].WithAlpha(255);
        return result;
    }

    private static List<SKColor> Parse(IReadOnlyList<string>? texts)
    {
        var colors = new List<SKColor>();
        if (texts == null)
            return colors;
        foreach (string text in texts)
            if (BackgroundSettings.TryParseColor(text, out var color))
                colors.Add(color);
        return colors.Count == texts.Count ? colors : [];
    }

    private static List<SKColor> Derive(LoadedTheme theme)
    {
        var colors = theme.Definition.Colors!;
        var from = SKColor.Parse(colors.Background);
        var to = SKColor.Parse(colors.Accent);
        return DerivedMix.Select(t => new SKColor(Mix(from.Red, to.Red, t), Mix(from.Green, to.Green, t), Mix(from.Blue, to.Blue, t))).ToList();
    }

    private static byte Mix(byte from, byte to, float t) => (byte)Math.Round(from + ((to - from) * t));
}
