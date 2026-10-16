// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;

namespace UrDeck.Engine.Themes;

/// <summary>Turns a loaded theme into the pixel-sized <see cref="Theme"/> widgets receive.</summary>
public static class ThemeResolver
{
    private static readonly SKFourByteTag WeightAxis = SKFourByteTag.Parse("wght");
    private static readonly SKFourByteTag OpticalSizeAxis = SKFourByteTag.Parse("opsz");

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
            TextStyle = Style(type, cellPx),
            Typefaces = LoadTypefaces(loaded, cellPx),
        };
    }

    private static TextStyle Style(ThemeTypographyDefinition type, float cellPx) => new()
    {
        LabelCase = Case(type.Case?.Label),
        TitleCase = Case(type.Case?.Title),
        ValueTracking = (float)(type.Tracking?.Value ?? 0),
        LabelTracking = (float)(type.Tracking?.Label ?? 0),
        TitleTracking = (float)(type.Tracking?.Title ?? 0),
        ValueSlantDegrees = (float)(type.Slant ?? 0),
        ValueScale = (float)(type.ValueScale ?? 1),
        ValueFill = type.ValueFill is { Colors: { Count: >= 2 } colors } fill
            ? new TextFill { Colors = colors.Select(Color).ToList(), AngleDegrees = (float)(fill.Angle ?? 0) }
            : null,
        ValueGlow = type.ValueGlow is { Blur: > 0 } glow
            ? new TextGlow
            {
                BlurPx = Px(glow.Blur, cellPx),
                Color = glow.Color == null ? null : Color(glow.Color),
                Alpha = (float)(glow.Alpha ?? 0.6),
            }
            : null,
    };

    private static TextCase Case(string? name) => name switch
    {
        "upper" => TextCase.Upper,
        "lower" => TextCase.Lower,
        _ => TextCase.None,
    };

    private static SKColor Color(string? hex) => SKColor.Parse(hex);

    private static float Px(double? fraction, float cellPx) => (float)fraction!.Value * cellPx;

    private static Dictionary<TextRole, SKTypeface> LoadTypefaces(LoadedTheme loaded, float cellPx)
    {
        var type = loaded.Definition.Typography!;
        var w = type.Weights!;
        var roles = new (TextRole Role, int Weight, float SizePx)[]
        {
            (TextRole.Value, (int)w.Value!.Value, 0f),
            (TextRole.Unit, (int)w.Unit!.Value, 0f),
            (TextRole.Label, (int)w.Label!.Value, Px(type.LabelSize, cellPx)),
            (TextRole.Body, (int)w.Body!.Value, Px(type.BodySize, cellPx)),
            (TextRole.Title, (int)w.Title!.Value, Px(type.TitleSize, cellPx)),
        };

        // One typeface per distinct (font, weight, optical size), shared by the roles that agree. Each font file is read
        // once per resolve; each typeface is a clone of that one base typeface.
        var context = new TypefaceContext(loaded, type.OpticalSize == "auto");
        var result = new Dictionary<TextRole, SKTypeface>();
        foreach (var (role, weight, sizePx) in roles)
            result[role] = context.Create(role, weight, sizePx);

        var kept = result.Values.ToHashSet();
        foreach (var baseFace in context.Bases.Values)
            if (baseFace != null && !kept.Contains(baseFace))
                baseFace.Dispose();
        return result;
    }

    private static readonly AsyncLocal<Action<SKTypeface>?> TypefaceCreatedHook = new();

    /// <summary>Called with each typeface the resolver creates on this flow (tests check that every one is disposed).</summary>
    internal static Action<SKTypeface>? TypefaceCreated
    {
        get => TypefaceCreatedHook.Value;
        set => TypefaceCreatedHook.Value = value;
    }

    private static SKTypeface? Created(SKTypeface? typeface)
    {
        if (typeface != null)
            TypefaceCreated?.Invoke(typeface);
        return typeface;
    }

    private sealed class TypefaceContext(LoadedTheme loaded, bool autoOpticalSize)
    {
        private readonly Dictionary<(string Font, int Weight, float? OpticalSize), SKTypeface> _made = [];

        /// <summary>The base typeface of each font file read, or null for a file that could not be read.</summary>
        public Dictionary<string, SKTypeface?> Bases { get; } = [];

        // Fallback chain: the role's font, the theme's font, the default theme's font, the system default.
        public SKTypeface Create(TextRole role, int weight, float sizePx)
        {
            if (loaded.RoleFonts.TryGetValue(role, out var roleFont))
            {
                if (TryCreate(roleFont, role, weight, sizePx) is { } own)
                    return own;
                string name = ThemeStore.RoleName(role);
                loaded.Warnings.Once($"font:{name}:{roleFont.Spec}",
                    $"Theme '{loaded.Name}': font '{roleFont.Spec}' for the {name} role (typography.fonts.{name}) could not be loaded; using typography.font.");
            }

            if (TryCreate(loaded.Font, role, weight, sizePx) is { } typeface)
                return typeface;

            loaded.Warnings.Once($"font::{loaded.Font.Spec}",
                $"Theme '{loaded.Name}': font '{loaded.Font.Spec}' could not be loaded; using the default theme's font.");
            if (TryCreate(ThemeStore.BuiltInDefaultFont, role, weight, sizePx) is { } fallback)
                return fallback;

            loaded.Warnings.Once("font:default", "The default theme's font could not be loaded; using the system default font.");
            var key = ("system:", weight, (float?)null);
            if (!_made.TryGetValue(key, out var system))
                _made[key] = system = Created(SKTypeface.FromFamilyName(null, ToSkWeight(weight), SKFontStyleWidth.Normal, SKFontStyleSlant.Upright))
                                      ?? SKTypeface.Default;
            return system;
        }

        private SKTypeface? TryCreate(ThemeFont font, TextRole role, int weight, float sizePx)
        {
            try
            {
                if (!ThemeStore.IsFontFile(font.Spec))
                    return Family(font.Spec, weight);

                // Keyed by the font's origin as well as its name: a theme's own "x.ttf" is not the built-in "x.ttf".
                string fontKey = $"{font.Open.GetHashCode()}:{font.Spec}";
                if (!Bases.TryGetValue(fontKey, out var baseFace))
                {
                    using var stream = font.Open(font.Spec);
                    if (stream != null)
                    {
                        using var data = SKData.Create(stream);
                        baseFace = Created(SKTypeface.FromData(data));
                    }
                    Bases[fontKey] = baseFace;
                }
                if (baseFace == null)
                    return null;

                CheckWeight(baseFace, font, role, weight);
                float? opticalSize = OpticalSize(baseFace, role, sizePx);
                var key = (fontKey, weight, opticalSize);
                if (!_made.TryGetValue(key, out var typeface))
                    _made[key] = typeface = Vary(baseFace, weight, opticalSize);
                return typeface;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                loaded.Warnings.Once($"read:{font.Spec}", $"Font '{font.Spec}' could not be read: {ex.Message}");
                return null;
            }
        }

        private SKTypeface? Family(string spec, int weight)
        {
            var key = ("family:" + spec, weight, (float?)null);
            if (_made.TryGetValue(key, out var made))
                return made;
            if (SKTypeface.FromFamilyName(spec, ToSkWeight(weight), SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) is not { } family)
                return null;
            if (!string.Equals(family.FamilyName, spec, StringComparison.OrdinalIgnoreCase))
            {
                family.Dispose();
                return null;
            }
            Created(family);
            return _made[key] = family;
        }

        /// <summary>The <c>opsz</c> value for the role with <c>auto</c>, or null to leave the axis at the font's default.</summary>
        private float? OpticalSize(SKTypeface baseFace, TextRole role, float sizePx)
        {
            if (!autoOpticalSize || Axis(baseFace, OpticalSizeAxis) is not { } axis)
                return null;
            return role is TextRole.Value or TextRole.Unit ? axis.Max : Math.Clamp(sizePx, axis.Min, axis.Max);
        }

        /// <summary>A weight the file cannot make is drawn at the nearest it can; says so once per role.</summary>
        private void CheckWeight(SKTypeface baseFace, ThemeFont font, TextRole role, int weight)
        {
            string name = ThemeStore.RoleName(role);
            if (Axis(baseFace, WeightAxis) is { } axis)
            {
                if (weight < axis.Min || weight > axis.Max)
                    loaded.Warnings.Once($"weight:{name}",
                        $"Theme '{loaded.Name}': the {name} weight {weight} is outside the weight range {axis.Min:0}..{axis.Max:0} of font '{font.Spec}'; " +
                        $"it is drawn at {Math.Clamp(weight, axis.Min, axis.Max):0}.");
                return;
            }

            int own = (int)Math.Round(baseFace.FontStyle.Weight / 100.0) * 100;
            if (weight != own)
                loaded.Warnings.Once($"weight:{name}",
                    $"Theme '{loaded.Name}': the {name} weight {weight} has no effect: font '{font.Spec}' has one weight ({own}).");
        }
    }

    private static SKFontVariationAxis? Axis(SKTypeface typeface, SKFourByteTag tag)
    {
        foreach (var axis in typeface.VariationDesignParameters)
            if (axis.Tag == tag)
                return axis;
        return null;
    }

    /// <summary>
    /// Sets the weight axis of a variable font, and the optical size axis when one is given, in one clone; a static font
    /// is returned unchanged.
    /// </summary>
    private static SKTypeface Vary(SKTypeface baseFace, int weight, float? opticalSize)
    {
        var coordinates = new List<SKFontVariationPositionCoordinate>(2);
        if (Axis(baseFace, WeightAxis) is { } weightAxis)
            coordinates.Add(new SKFontVariationPositionCoordinate { Axis = WeightAxis, Value = Math.Clamp(weight, weightAxis.Min, weightAxis.Max) });
        if (opticalSize is { } opsz)
            coordinates.Add(new SKFontVariationPositionCoordinate { Axis = OpticalSizeAxis, Value = opsz });
        if (coordinates.Count == 0)
            return baseFace;

        return Created(baseFace.Clone([.. coordinates])) ?? baseFace;
    }

    private static SKFontStyleWeight ToSkWeight(int weight) =>
        (SKFontStyleWeight)Math.Clamp((int)Math.Round(weight / 100.0) * 100, 100, 1000);
}
