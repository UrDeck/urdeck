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
    /// wide. The caller owns the result and must dispose it; it owns the typefaces. With <paramref name="compact"/> (the dock)
    /// the card radius and border width are the theme's <c>card.compact</c> ones; the rest is the page's.
    /// </summary>
    public static Theme Resolve(LoadedTheme loaded, float cellPx, bool compact = false)
    {
        var d = loaded.Definition;
        var c = d.Colors!;
        var card = d.Card!;
        var type = d.Typography!;
        var shape = CardShapeValues(card, cellPx, compact);
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
            Accent2 = Color(c.Accent2 ?? c.Accent),
            Ink = c.Ink != null ? Color(c.Ink) : ColorContrast.MoreContrasting(Color(c.Accent), Color(c.Text), Color(c.Background)),
            Ramp = c.Ramp is { Count: >= 2 } ramp ? ramp.Select(Color).ToList() : [Color(c.Good), Color(c.Warning), Color(c.Critical)],
            CardRadius = shape.Radius,
            CardBorderWidth = shape.BorderWidth,
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
            Gauge = Gauge(d.Gauge, cellPx),
            Chip = Chip(d.Chip),
            Divider = Divider(d.Divider, Color(c.TextMuted).WithAlpha(0x55), Px(card.Gap, cellPx), cellPx),
            Well = Well(d.Well, cellPx),
            TextStyle = Style(type, cellPx),
            Typefaces = LoadTypefaces(loaded, cellPx),
        };
    }

    private static (float Radius, float BorderWidth) CardShapeValues(ThemeCardDefinition card, float cellPx, bool compact)
    {
        var over = compact ? card.Compact : null;
        float radius = over?.Shape == "circle" ? cellPx / 2 : Px(over?.Radius ?? card.Radius, cellPx);
        return (radius, Px(over?.BorderWidth ?? card.BorderWidth, cellPx));
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

    /// <summary>
    /// The gauge values of the theme in pixels. A value the theme leaves out gives null (nothing to draw), except in a group
    /// the theme did write, where a key it left out takes the default listed here.
    /// </summary>
    private static GaugeTheme Gauge(ThemeGaugeDefinition? g, float cellPx)
    {
        if (g == null)
            return new GaugeTheme();

        return new GaugeTheme
        {
            Layout = g.Layout == "stacked" ? GaugeLayout.Stacked : GaugeLayout.Auto,
            LabelPosition = g.LabelPosition switch
            {
                "inside" => GaugeLabelPosition.Inside,
                "below" => GaugeLabelPosition.Below,
                "above" => GaugeLabelPosition.Above,
                _ => GaugeLabelPosition.Auto,
            },
            Sweep = g.Sweep is { } sweep && (sweep.Start != null || sweep.Span != null)
                ? new GaugeSweep((float)(sweep.Start ?? 135), (float)(sweep.Span ?? 270))
                : null,
            Segments = g.Segments is { Length: { } length } segments
                ? new GaugeSegments(Px(length, cellPx), Px(segments.Gap ?? 0, cellPx))
                : null,
            Track = g.Track is { } track && (track.Color != null || track.WidthRatio != null)
                ? new GaugeTrack(OptionalColor(track.Color), (float)(track.WidthRatio ?? 1))
                : null,
            Outline = g.Outline is { Width: { } width } outline
                ? new GaugeOutline(Px(width, cellPx), OptionalColor(outline.Color))
                : null,
            Ticks = g.Ticks is { } ticks
                ? new GaugeTicks(
                    ticks.Count ?? 11,
                    ticks.MajorEvery ?? 5,
                    Px(ticks.Length ?? 0.04, cellPx),
                    Px(ticks.Thickness ?? 0.006, cellPx),
                    OptionalColor(ticks.Color),
                    ticks.Labels ?? false)
                : null,
            Scale = g.Scale is { Labels: { Count: >= 2 } labels } scale
                ? new GaugeScaleLabels(labels, Px(scale.Size ?? 0, cellPx))
                : null,
            Redline = g.Redline is { } redline
                ? new GaugeRedline((float)(redline.From ?? 0.85), OptionalColor(redline.Color), Glow(redline.Glow, cellPx), redline.LightsAbove ?? false)
                : null,
            Marker = g.Marker is { } marker
                ? new GaugeMarker(
                    string.Equals(marker.Shape, "needle", StringComparison.OrdinalIgnoreCase) ? GaugeMarkerShape.Needle : GaugeMarkerShape.Diamond,
                    Px(marker.Size ?? 0.05, cellPx),
                    OptionalColor(marker.Color),
                    marker.Halo ?? false)
                : null,
            Crosshair = g.Crosshair == true,
            Gradient = g.Gradient switch
            {
                { Ramp: true } => new GaugeGradient([], 0f, true),
                { Stops: { Colors: { Count: >= 2 } colors } stops } => new GaugeGradient(colors.Select(Color).ToList(), (float)(stops.Angle ?? 0), false),
                _ => null,
            },
            Glow = Glow(g.Glow, cellPx),
            ColorByRamp = g.ColorBy == "ramp",
            ValueTint = g.ValueTint == true,
            Sparkline = g.Sparkline is { Enabled: true } sparkline
                ? new GaugeSparkline(
                    Px(sparkline.Height ?? 0.3, cellPx),
                    Px(sparkline.StrokeWidth ?? 0.012, cellPx),
                    (int)(sparkline.HistorySeconds ?? 60),
                    SparklineMinRectCells * cellPx)
                : null,
            Status = g.Status is { Enabled: true } status
                ? new GaugeStatusStyle(
                    status.Labels is { Count: 3 } statusLabels ? statusLabels : ["OK", "WARM", "HOT"],
                    (float)(status.Thresholds is { Count: 2 } t ? t[0] : 0.7),
                    (float)(status.Thresholds is { Count: 2 } u ? u[1] : 0.9))
                : null,
        };
    }

    /// <summary>A gauge rectangle must be this many grid cells tall to show a sparkline.</summary>
    private const float SparklineMinRectCells = 0.9f;

    private static ChipTheme Chip(ThemeChipDefinition? chip) => chip == null
        ? new ChipTheme()
        : new ChipTheme(
            chip.Shape == "pill" ? ChipShape.Pill : ChipShape.None,
            OptionalColor(chip.Fill),
            OptionalColor(chip.Border),
            OptionalColor(chip.TextColor),
            chip.ColorBy != "none");

    /// <summary>A theme that sets no divider gets the rule the Weather card has always drawn (the muted text colour at 0x55, a pixel or 0.15 of the gap).</summary>
    private static DividerTheme Divider(ThemeDividerDefinition? divider, SKColor defaultColor, float gapPx, float cellPx)
    {
        if (divider == null)
            return new DividerTheme(DividerStyle.Line, defaultColor, Math.Max(1f, gapPx * 0.15f));
        var style = divider.Style switch
        {
            "none" => DividerStyle.None,
            "gradient" => DividerStyle.Gradient,
            "dimension" => DividerStyle.Dimension,
            _ => DividerStyle.Line,
        };
        return new DividerTheme(
            style,
            OptionalColor(divider.Color) ?? defaultColor,
            Math.Max(1f, Px(divider.Thickness ?? 0.01, cellPx)),
            OptionalColor(divider.GradientTo));
    }

    private static WellTheme? Well(ThemeWellDefinition? well, float cellPx) =>
        well == null ? null : new WellTheme(OptionalColor(well.Fill), Px(well.Radius ?? 0.09, cellPx), OptionalColor(well.Border));

    private static GaugeGlow? Glow(ThemeGlowDefinition? glow, float cellPx) =>
        glow is { Blur: > 0 } ? new GaugeGlow(Px(glow.Blur, cellPx), OptionalColor(glow.Color), (float)(glow.Alpha ?? 0.6)) : null;

    private static SKColor? OptionalColor(string? hex) => hex == null ? null : Color(hex);

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
