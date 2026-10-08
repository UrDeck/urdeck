// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;

namespace UrDeck.Engine.Themes;

/// <summary>Turns a loaded theme into the pixel-sized <see cref="Theme"/> widgets receive.</summary>
public static class ThemeResolver
{
    private static readonly SKFourByteTag WeightAxis = SKFourByteTag.Parse("wght");

    /// <summary>
    /// Resolves <paramref name="loaded"/> for a surface whose grid cell is <paramref name="cellPx"/> physical pixels
    /// wide. The caller owns the result and must dispose it; it owns the typefaces.
    /// </summary>
    public static Theme Resolve(LoadedTheme loaded, float cellPx)
    {
        var d = loaded.Definition;
        var c = d.Colors!;
        var card = d.Card!;
        var type = d.Typography!;
        var stroke = d.Stroke!;

        return new Theme
        {
            Background = Color(c.Background),
            CardFill = Color(c.CardFill),
            CardBorder = Color(c.CardBorder),
            Text = Color(c.Text),
            TextMuted = Color(c.TextMuted),
            Accent = Color(c.Accent),
            AccentDim = Color(c.AccentDim),
            Good = Color(c.Good),
            Warning = Color(c.Warning),
            Critical = Color(c.Critical),
            CardRadius = Px(card.Radius, cellPx),
            CardBorderWidth = Px(card.BorderWidth, cellPx),
            Gap = Px(card.Gap, cellPx),
            Padding = Px(card.Padding, cellPx),
            LabelSize = Px(type.LabelSize, cellPx),
            BodySize = Px(type.BodySize, cellPx),
            TitleSize = Px(type.TitleSize, cellPx),
            UnitRatio = (float)type.UnitRatio!.Value,
            StrokeRatio = (float)stroke.Thickness!.Value,
            StrokeCap = string.Equals(stroke.Cap, "square", StringComparison.OrdinalIgnoreCase)
                ? SKStrokeCap.Square
                : SKStrokeCap.Round,
            GaugeStyle = ThemeGaugeDefinition.Parse(d.Gauge?.Style) ?? GaugeStyle.Ring,
            Typefaces = LoadTypefaces(loaded),
        };
    }

    private static SKColor Color(string? hex) => SKColor.Parse(hex);

    private static float Px(double? fraction, float cellPx) => (float)fraction!.Value * cellPx;

    private static Dictionary<TextRole, SKTypeface> LoadTypefaces(LoadedTheme loaded)
    {
        var w = loaded.Definition.Typography!.Weights!;
        var weights = new (TextRole Role, int Weight)[]
        {
            (TextRole.Value, (int)w.Value!.Value),
            (TextRole.Unit, (int)w.Unit!.Value),
            (TextRole.Label, (int)w.Label!.Value),
            (TextRole.Body, (int)w.Body!.Value),
            (TextRole.Title, (int)w.Title!.Value),
        };

        // One typeface per distinct weight, shared by the roles that use it.
        // The font file is read once per resolve; each weight is a clone of that one base typeface.
        var bases = new Dictionary<string, SKTypeface?>();
        var byWeight = new Dictionary<int, SKTypeface>();
        var result = new Dictionary<TextRole, SKTypeface>();
        foreach (var (role, weight) in weights)
        {
            if (!byWeight.TryGetValue(weight, out var typeface))
                byWeight[weight] = typeface = CreateTypeface(loaded, weight, bases);
            result[role] = typeface;
        }

        var kept = result.Values.ToHashSet();
        foreach (var baseFace in bases.Values)
            if (baseFace != null && !kept.Contains(baseFace))
                baseFace.Dispose();
        return result;
    }

    // Fallback chain: the theme's font, then the default theme's font, then the system default.
    private static SKTypeface CreateTypeface(LoadedTheme loaded, int weight, Dictionary<string, SKTypeface?> bases)
    {
        var typeface = TryCreate(loaded.Font, weight, bases);
        if (typeface != null)
            return typeface;

        UrDeckLog.Warn($"Theme '{loaded.Name}': font '{loaded.Font.Spec}' could not be loaded; using the default theme's font.");
        typeface = TryCreate(ThemeStore.BuiltInDefaultFont, weight, bases);
        if (typeface != null)
            return typeface;

        UrDeckLog.Warn("The default theme's font could not be loaded; using the system default font.");
        return SKTypeface.FromFamilyName(null, ToSkWeight(weight), SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
               ?? SKTypeface.Default;
    }

    private static SKTypeface? TryCreate(ThemeFont font, int weight, Dictionary<string, SKTypeface?> bases)
    {
        try
        {
            if (!ThemeStore.IsFontFile(font.Spec))
                return SKTypeface.FromFamilyName(font.Spec, ToSkWeight(weight), SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) is { } family
                       && string.Equals(family.FamilyName, font.Spec, StringComparison.OrdinalIgnoreCase)
                    ? family
                    : null;

            // Keyed by the font's origin as well as its name: a theme's own "x.ttf" is not the built-in "x.ttf".
            string key = $"{font.Open.GetHashCode()}:{font.Spec}";
            if (!bases.TryGetValue(key, out var baseFace))
            {
                using var stream = font.Open(font.Spec);
                if (stream != null)
                {
                    using var data = SKData.Create(stream);
                    baseFace = SKTypeface.FromData(data);
                }
                bases[key] = baseFace;
            }
            return baseFace == null ? null : ApplyWeight(baseFace, weight);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            UrDeckLog.Warn($"Font '{font.Spec}' could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>Sets the weight axis of a variable font; a static font is returned unchanged.</summary>
    private static SKTypeface ApplyWeight(SKTypeface baseFace, int weight)
    {
        var axis = baseFace.VariationDesignParameters.FirstOrDefault(a => a.Tag == WeightAxis);
        if (axis.Tag != WeightAxis)
            return baseFace;

        float value = Math.Clamp(weight, axis.Min, axis.Max);
        var varied = baseFace.Clone([new SKFontVariationPositionCoordinate { Axis = WeightAxis, Value = value }]);
        return varied ?? baseFace;
    }

    private static SKFontStyleWeight ToSkWeight(int weight) =>
        (SKFontStyleWeight)Math.Clamp((int)Math.Round(weight / 100.0) * 100, 100, 1000);
}
