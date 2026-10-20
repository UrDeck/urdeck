// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;

namespace UrDeck.Engine.Themes;

public static partial class ThemeLint
{
    private const double MinTextRatio = 4.5;
    private const double MinMutedRatio = 3;

    private const string MovingBackground =
        "A translucent fill is composited over colors.background; the page behind the cards may be a moving background, which this check does not know.";

    /// <summary>The text on the card fill, on each gradient stop of it, and on each card tint; the muted text on the fill.</summary>
    private static void CheckContrast(ThemeDefinition definition, List<ThemeDiagnostic> found)
    {
        string file = ThemeStore.SettingsFileName;
        var colors = definition.Colors!;
        var background = Parse(colors.Background!).WithAlpha(0xFF);
        var text = Parse(colors.Text!);
        var muted = Parse(colors.TextMuted!);

        // The fill the cards draw: the gradient's stops when there is one (checked at its darkest and lightest), else the colour.
        var fills = new List<(string Name, SKColor Color)>();
        if (definition.Card?.FillGradient?.Colors is { Count: >= 2 } stops)
        {
            var parsed = stops.Select((c, i) => (Name: $"card.fillGradient stop {i + 1}", Color: Parse(c))).ToList();
            fills.Add(parsed.MinBy(s => ColorContrast.Luminance(ColorContrast.Over(s.Color, background))));
            fills.Add(parsed.MaxBy(s => ColorContrast.Luminance(ColorContrast.Over(s.Color, background))));
        }
        else
        {
            fills.Add(("the card fill", Parse(colors.CardFill!)));
        }

        var worst = Worst(fills, text, background);
        if (worst.Ratio < MinTextRatio)
        {
            found.Add(Warning(ThemeCodes.ContrastText,
                $"colors.text {Hex(text)} on {worst.Name} {Hex(worst.Color)} has a contrast of {Ratio(worst.Ratio)}:1, under {MinTextRatio}:1; make them further apart in lightness.{Assumed(worst.Color)}",
                file, "colors.text"));
        }

        var mutedWorst = Worst(fills, muted, background);
        if (mutedWorst.Ratio < MinMutedRatio)
        {
            found.Add(new ThemeDiagnostic(ThemeCodes.ContrastMuted, ThemeSeverity.Info,
                $"colors.textMuted {Hex(muted)} on {mutedWorst.Name} {Hex(mutedWorst.Color)} has a contrast of {Ratio(mutedWorst.Ratio)}:1, under {MinMutedRatio}:1; lighten or darken it if it carries information.{Assumed(mutedWorst.Color)}",
                file, "colors.textMuted"));
        }

        // A tinted card keeps the theme's text colour; tints apply only when the fill is not a gradient.
        if (definition.Card?.FillGradient?.Colors is not { Count: >= 2 } && colors.CardTints is { } tints)
        {
            for (int i = 0; i < tints.Count; i++)
            {
                var tint = Parse(tints[i]);
                var seen = ColorContrast.Over(tint, background);
                double ratio = ColorContrast.Ratio(ColorContrast.Over(text, seen), seen);
                if (ratio < MinTextRatio)
                {
                    found.Add(Warning(ThemeCodes.ContrastTint,
                        $"colors.cardTints[{i}] {Hex(tint)} under colors.text {Hex(text)} has a contrast of {Ratio(ratio)}:1, under {MinTextRatio}:1; the tinted card keeps the theme's text colour, so change the tint.{Assumed(tint)}",
                        file, $"colors.cardTints[{i}]"));
                }
            }
        }
    }

    /// <summary>The fill with the lowest contrast for <paramref name="ink"/>, each fill seen over the page colour.</summary>
    private static (string Name, SKColor Color, double Ratio) Worst(List<(string Name, SKColor Color)> fills, SKColor ink, SKColor background) =>
        fills
            .Select(f =>
            {
                var seen = ColorContrast.Over(f.Color, background);
                return (f.Name, f.Color, Ratio: ColorContrast.Ratio(ColorContrast.Over(ink, seen), seen));
            })
            .MinBy(f => f.Ratio);

    private static string Assumed(SKColor fill) => fill.Alpha < 255 ? " " + MovingBackground : "";

    private static string Ratio(double ratio) => ratio.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Hex(SKColor color) =>
        color.Alpha == 255 ? $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}" : $"#{color.Alpha:x2}{color.Red:x2}{color.Green:x2}{color.Blue:x2}";
}
