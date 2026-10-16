// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Data;

namespace UrDeck.Sdk.Components;

/// <summary>Optional parts and placement of a <see cref="Readout"/>.</summary>
public sealed class ReadoutOptions
{
    public string? Unit { get; init; }
    public UnitPlacement UnitPlacement { get; init; } = UnitPlacement.Raised;
    public string? Label { get; init; }

    /// <summary>
    /// The widest value the widget expects (for example <c>100</c> or <c>88:88</c>). The readout is sized so that this
    /// fits, so its size stays the same while the value changes. Defaults to the value itself.
    /// </summary>
    public string? WidestValue { get; init; }

    public HorizontalAlign Horizontal { get; init; } = HorizontalAlign.Center;
    public VerticalAlign Vertical { get; init; } = VerticalAlign.Middle;

    /// <summary>Overrides the theme's text colour for the value only.</summary>
    public SKColor? ValueColor { get; init; }

    /// <summary>Overrides the theme's muted text colour for the label.</summary>
    public SKColor? LabelColor { get; init; }

    /// <summary>Fraction of the fitted size to draw at, from 0.1 to 1.0.</summary>
    public float Scale { get; init; } = 1f;
}

/// <summary>
/// A large value with an optional unit and label. The value is as large as fits the rectangle and does not change size
/// as the number changes; digits have equal widths so a changing number does not move the characters around it. The theme's
/// text style (<see cref="Theme.TextStyle"/>) adds letter case and spacing to the label, and spacing, slant, size, a gradient
/// fill and a glow to the value.
/// </summary>
public static class Readout
{
    private const float UnitGapRatio = 0.06f;
    private const float LabelGapRatio = 1.2f;

    /// <summary>Draws the readout and returns the rectangle it occupies.</summary>
    public static SKRect Draw(SKCanvas canvas, Theme theme, SKRect rect, string value, ReadoutOptions? options = null) =>
        Run(canvas, theme, rect, value, options);

    /// <summary>The rectangle <see cref="Draw(SKCanvas, Theme, SKRect, string, ReadoutOptions?)"/> would occupy, without drawing anything.</summary>
    public static SKRect Measure(Theme theme, SKRect rect, string value, ReadoutOptions? options = null) =>
        Run(null, theme, rect, value, options);

    /// <summary>
    /// Draws a formatted reading. The value, unit, unit placement and widest value come from <paramref name="reading"/>;
    /// <paramref name="options"/> only supplies the label, alignment and scale. A value that is not current is drawn in
    /// the theme's muted text colour. The text size is the same for a value, a stale value and a dash.
    /// </summary>
    public static SKRect Draw(SKCanvas canvas, Theme theme, SKRect rect, ReadingText reading, ReadoutOptions? options = null) =>
        Run(canvas, theme, rect, reading.Value, ForReading(theme, reading, options));

    /// <summary>The rectangle <see cref="Draw(SKCanvas, Theme, SKRect, ReadingText, ReadoutOptions?)"/> would occupy.</summary>
    public static SKRect Measure(Theme theme, SKRect rect, ReadingText reading, ReadoutOptions? options = null) =>
        Run(null, theme, rect, reading.Value, ForReading(theme, reading, options));

    private static ReadoutOptions ForReading(Theme theme, ReadingText reading, ReadoutOptions? options) => new()
    {
        Unit = reading.Unit,
        UnitPlacement = reading.UnitPlacement,
        WidestValue = reading.WidestValue,
        Label = options?.Label,
        LabelColor = options?.LabelColor,
        Horizontal = options?.Horizontal ?? HorizontalAlign.Center,
        Vertical = options?.Vertical ?? VerticalAlign.Middle,
        ValueColor = reading.IsCurrent ? options?.ValueColor : theme.TextMuted,
        Scale = options?.Scale ?? 1f,
    };

    private static SKRect Run(SKCanvas? canvas, Theme theme, SKRect rect, string value, ReadoutOptions? options)
    {
        options ??= new ReadoutOptions();
        if (string.IsNullOrEmpty(value) || rect.Width <= 0 || rect.Height <= 0)
            return SKRect.Create(rect.Left, rect.Top, 0, 0);

        var style = theme.TextStyle;
        string? label = options.Label is { } given ? TextStyle.Apply(style.LabelCase, given) : null;
        bool hasUnit = !string.IsNullOrEmpty(options.Unit);
        bool hasLabel = !string.IsNullOrEmpty(label);
        float ratio = theme.UnitRatio;
        float tracking = style.ValueTracking * TextMetrics.ReferenceSize;

        using var valueFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Value), TextMetrics.ReferenceSize);
        using var unitFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Unit), TextMetrics.ReferenceSize);
        using var labelFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), theme.LabelSize);

        float digit = TextMetrics.DigitAdvance(valueFont);
        float unitWidth = hasUnit ? unitFont.MeasureText(options.Unit!) : 0f;
        float valueCap = TextMetrics.CapHeight(valueFont);
        float labelBlock = hasLabel ? TextMetrics.CapHeight(labelFont) + theme.LabelSize * LabelGapRatio : 0f;

        float size = Math.Max(1f, Fit(theme, rect, value, options, valueFont, unitFont, labelFont) * Scale(options, style));
        float k = size / TextMetrics.ReferenceSize;

        float actualValueWidth = Advance(valueFont, value, digit, tracking) * k;
        float unitDrawWidth = hasUnit ? unitWidth * ratio * k : 0f;
        float unitGap = hasUnit ? UnitGapRatio * size : 0f;
        float blockWidth = actualValueWidth + unitGap + unitDrawWidth;
        float capHeight = valueCap * k;
        float blockHeight = capHeight + labelBlock;

        float left = Align(options.Horizontal, rect.Left, rect.Right, blockWidth);
        float top = options.Vertical switch
        {
            VerticalAlign.Top => rect.Top,
            VerticalAlign.Bottom => rect.Bottom - blockHeight,
            _ => rect.MidY - blockHeight / 2,
        };
        float baseline = top + capHeight;
        var block = SKRect.Create(left, top, blockWidth, blockHeight);
        if (canvas == null)
            return block;

        valueFont.Size = size;
        unitFont.Size = size * ratio;
        using var valuePaint = new SKPaint { Color = options.ValueColor ?? theme.Text, IsAntialias = true };
        var drawn = new DrawnValue(value, left, baseline, digit * k, tracking * k, actualValueWidth, capHeight);
        if (HasValueEffects(style))
            DrawStyledValue(canvas, style, valueFont, valuePaint, drawn, filled: options.ValueColor == null && style.ValueFill != null);
        else
            DrawValue(canvas, valueFont, valuePaint, value, left, baseline, digit * k, 0f);

        if (hasUnit)
        {
            float unitX = left + actualValueWidth + unitGap;
            float unitBaseline = options.UnitPlacement == UnitPlacement.Raised
                ? top + TextMetrics.CapHeight(unitFont)
                : baseline;
            canvas.DrawText(options.Unit!, unitX, unitBaseline, SKTextAlign.Left, unitFont, valuePaint);
        }

        if (hasLabel)
        {
            float labelWidth = TextMetrics.Measure(labelFont, label!, style.LabelTracking);
            if (labelWidth > rect.Width)
            {
                labelFont.Size = theme.LabelSize * rect.Width / labelWidth;
                labelWidth = rect.Width;
            }
            float labelX = Align(options.Horizontal, rect.Left, rect.Right, labelWidth);
            using var labelPaint = new SKPaint { Color = options.LabelColor ?? theme.TextMuted, IsAntialias = true };
            if (style.LabelTracking == 0)
                canvas.DrawText(label!, labelX, baseline + labelBlock, SKTextAlign.Left, labelFont, labelPaint);
            else
                TextMetrics.DrawTracked(canvas, label!, labelX, baseline + labelBlock, labelFont, labelPaint, style.LabelTracking);
        }

        return block;
    }

    /// <summary>The fraction of the fitted size to draw at: the widget's, times the theme's value scale, never above the fit.</summary>
    private static float Scale(ReadoutOptions options, TextStyle style)
    {
        float scale = Math.Clamp(options.Scale, 0.1f, 1f);
        return style.ValueScale == 1f ? scale : Math.Min(1f, scale * style.ValueScale);
    }

    /// <summary>The value size in pixels that fits <paramref name="rect"/>, before <see cref="ReadoutOptions.Scale"/>.</summary>
    internal static float FitSize(Theme theme, SKRect rect, string value, ReadoutOptions? options = null)
    {
        options ??= new ReadoutOptions();
        if (string.IsNullOrEmpty(value) || rect.Width <= 0 || rect.Height <= 0)
            return 0f;
        using var valueFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Value), TextMetrics.ReferenceSize);
        using var unitFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Unit), TextMetrics.ReferenceSize);
        using var labelFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), theme.LabelSize);
        return Math.Max(1f, Fit(theme, rect, value, options, valueFont, unitFont, labelFont));
    }

    private static float Fit(Theme theme, SKRect rect, string value, ReadoutOptions options, SKFont valueFont, SKFont unitFont, SKFont labelFont)
    {
        bool hasUnit = !string.IsNullOrEmpty(options.Unit);
        bool hasLabel = !string.IsNullOrEmpty(options.Label);
        float digit = TextMetrics.DigitAdvance(valueFont);
        float tracking = theme.TextStyle.ValueTracking * TextMetrics.ReferenceSize;
        float sampleWidth = Advance(valueFont, options.WidestValue ?? value, digit, tracking);
        float unitWidth = hasUnit ? unitFont.MeasureText(options.Unit!) : 0f;

        // Everything is measured at the reference size and scales linearly with the value's size.
        float widthPerSize = (sampleWidth + (hasUnit ? unitWidth * theme.UnitRatio + UnitGapRatio * TextMetrics.ReferenceSize : 0f))
                             / TextMetrics.ReferenceSize;
        float labelBlock = hasLabel ? TextMetrics.CapHeight(labelFont) + theme.LabelSize * LabelGapRatio : 0f;
        float capPerSize = TextMetrics.CapHeight(valueFont) / TextMetrics.ReferenceSize;
        return Math.Min(rect.Width / widthPerSize, Math.Max(1f, rect.Height - labelBlock) / capPerSize);
    }

    private static float Align(HorizontalAlign align, float left, float right, float width) => align switch
    {
        HorizontalAlign.Left => left,
        HorizontalAlign.Right => right - width,
        _ => (left + right) / 2 - width / 2,
    };

    /// <summary>
    /// Width of <paramref name="text"/> with every digit given the same advance, and <paramref name="tracking"/> pixels after
    /// each character but the last.
    /// </summary>
    private static float Advance(SKFont font, string text, float digitAdvance, float tracking)
    {
        float width = 0;
        foreach (char c in text)
            width += char.IsAsciiDigit(c) ? digitAdvance : font.MeasureText(c.ToString());
        return tracking == 0 ? width : width + tracking * (text.Length - 1);
    }

    private static void DrawValue(SKCanvas canvas, SKFont font, SKPaint paint, string text, float x, float baseline, float digitAdvance, float tracking)
    {
        foreach (char c in text)
        {
            string glyph = c.ToString();
            if (char.IsAsciiDigit(c))
            {
                float own = font.MeasureText(glyph);
                canvas.DrawText(glyph, x + (digitAdvance - own) / 2, baseline, SKTextAlign.Left, font, paint);
                x += digitAdvance + tracking;
            }
            else
            {
                canvas.DrawText(glyph, x, baseline, SKTextAlign.Left, font, paint);
                x += font.MeasureText(glyph) + tracking;
            }
        }
    }

    // ---------------------------------------------------------------- the theme's value effects

    /// <summary>The value as laid out: where it starts, its baseline, the digit advance and spacing in pixels, its box.</summary>
    private readonly record struct DrawnValue(string Text, float Left, float Baseline, float DigitAdvance, float Tracking, float Width, float CapHeight);

    private static bool HasValueEffects(TextStyle style) =>
        style.ValueTracking != 0 || style.ValueSlantDegrees != 0 || style.ValueFill != null || style.ValueGlow is { BlurPx: > 0 };

    /// <summary>
    /// The value with the theme's spacing, slant, fill and glow. It is drawn about its own origin (left end of the baseline),
    /// so the slant leans about the baseline and a blurred copy can be reused wherever the same value is drawn again.
    /// </summary>
    private static void DrawStyledValue(SKCanvas canvas, TextStyle style, SKFont font, SKPaint paint, DrawnValue value, bool filled)
    {
        canvas.Save();
        canvas.Translate(value.Left, value.Baseline);
        float skew = -MathF.Tan(style.ValueSlantDegrees * MathF.PI / 180f);

        using var shader = filled ? Gradient(style.ValueFill!, value.Width, value.CapHeight) : null;
        if (style.ValueGlow is { BlurPx: > 0 } glow)
            DrawGlow(canvas, style, glow, font, paint.Color, shader, value, skew);

        if (skew != 0)
            canvas.Skew(skew, 0);
        using var fillPaint = shader == null ? null : new SKPaint { Color = SKColors.White, Shader = shader, IsAntialias = true };
        DrawValue(canvas, font, fillPaint ?? paint, value.Text, 0, 0, value.DigitAdvance, value.Tracking);
        canvas.Restore();
    }

    /// <summary>The fill across the value's box (its width by its cap height above the baseline), at the theme's angle.</summary>
    private static SKShader Gradient(TextFill fill, float width, float capHeight)
    {
        float angle = fill.AngleDegrees * MathF.PI / 180f;
        float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        var centre = new SKPoint(width / 2, -capHeight / 2);
        float half = width / 2 * MathF.Abs(cos) + capHeight / 2 * MathF.Abs(sin);
        var start = new SKPoint(centre.X - cos * half, centre.Y - sin * half);
        var end = new SKPoint(centre.X + cos * half, centre.Y + sin * half);
        return SKShader.CreateLinearGradient(start, end, [.. fill.Colors], null, SKShaderTileMode.Clamp);
    }

    /// <summary>
    /// Draws the blurred copy, slant included, with nothing but a translation: a copy drawn through the skew, or scaled, costs
    /// several times as much on the CPU raster.
    /// </summary>
    private static void DrawGlow(
        SKCanvas canvas, TextStyle style, TextGlow glow, SKFont font, SKColor valueColor, SKShader? shader, DrawnValue value, float skew)
    {
        var colour = glow.Color ?? valueColor;
        bool useShader = glow.Color == null && shader != null;
        float margin = MathF.Ceiling(glow.BlurPx * 1.5f) + 1;
        var metrics = font.Metrics;
        // The skew moves the top of the text right by |skew| times the ascent and its foot left by |skew| times the descent.
        float leanLeft = MathF.Abs(skew) * metrics.Descent, leanRight = MathF.Abs(skew) * -metrics.Ascent;
        var key = new GlowKey(value.Text, font.Size, (uint)colour, useShader);
        var image = style.Glows.Get(key, () =>
        {
            var info = new SKImageInfo(
                (int)MathF.Ceiling(value.Width + leanLeft + leanRight + 2 * margin), (int)MathF.Ceiling(metrics.Descent - metrics.Ascent + 2 * margin),
                SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            surface.Canvas.Translate(margin + leanLeft, margin - metrics.Ascent);
            if (skew != 0)
                surface.Canvas.Skew(skew, 0);
            using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, glow.BlurPx / 2);
            using var blurPaint = new SKPaint { Color = useShader ? SKColors.White : colour, Shader = useShader ? shader : null, MaskFilter = blur, IsAntialias = true };
            DrawValue(surface.Canvas, font, blurPaint, value.Text, 0, 0, value.DigitAdvance, value.Tracking);
            return surface.Snapshot();
        });
        using var glowPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(glow.Alpha, 0f, 1f) * 255)) };
        canvas.DrawImage(image, -margin - leanLeft, metrics.Ascent - margin, new SKSamplingOptions(SKFilterMode.Nearest), glowPaint);
    }
}
