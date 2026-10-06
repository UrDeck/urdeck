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

    /// <summary>Fraction of the fitted size to draw at, from 0.1 to 1.0.</summary>
    public float Scale { get; init; } = 1f;
}

/// <summary>
/// A large value with an optional unit and label. The value is as large as fits the rectangle and does not change size
/// as the number changes; digits have equal widths so a changing number does not move the characters around it.
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

        bool hasUnit = !string.IsNullOrEmpty(options.Unit);
        bool hasLabel = !string.IsNullOrEmpty(options.Label);
        float ratio = theme.UnitRatio;

        using var valueFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Value), TextMetrics.ReferenceSize);
        using var unitFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Unit), TextMetrics.ReferenceSize);
        using var labelFont = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), theme.LabelSize);

        float digit = TextMetrics.DigitAdvance(valueFont);
        float sampleWidth = Advance(valueFont, options.WidestValue ?? value, digit);
        float unitWidth = hasUnit ? unitFont.MeasureText(options.Unit!) : 0f;
        float valueCap = TextMetrics.CapHeight(valueFont);

        // Everything below is measured at the reference size and scales linearly with the value's size.
        float widthPerSize = (sampleWidth + (hasUnit ? unitWidth * ratio + UnitGapRatio * TextMetrics.ReferenceSize : 0f))
                             / TextMetrics.ReferenceSize;
        float labelCap = hasLabel ? TextMetrics.CapHeight(labelFont) : 0f;
        float labelBlock = hasLabel ? labelCap + theme.LabelSize * LabelGapRatio : 0f;
        float capPerSize = valueCap / TextMetrics.ReferenceSize;

        float size = Math.Min(rect.Width / widthPerSize, Math.Max(1f, rect.Height - labelBlock) / capPerSize);
        size = Math.Max(1f, size * Math.Clamp(options.Scale, 0.1f, 1f));
        float k = size / TextMetrics.ReferenceSize;

        float actualValueWidth = Advance(valueFont, value, digit) * k;
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
        DrawValue(canvas, valueFont, valuePaint, value, left, baseline, digit * k);

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
            float labelWidth = labelFont.MeasureText(options.Label!);
            if (labelWidth > rect.Width)
            {
                labelFont.Size = theme.LabelSize * rect.Width / labelWidth;
                labelWidth = rect.Width;
            }
            float labelX = Align(options.Horizontal, rect.Left, rect.Right, labelWidth);
            using var labelPaint = new SKPaint { Color = theme.TextMuted, IsAntialias = true };
            canvas.DrawText(options.Label!, labelX, baseline + labelBlock, SKTextAlign.Left, labelFont, labelPaint);
        }

        return block;
    }

    private static float Align(HorizontalAlign align, float left, float right, float width) => align switch
    {
        HorizontalAlign.Left => left,
        HorizontalAlign.Right => right - width,
        _ => (left + right) / 2 - width / 2,
    };

    /// <summary>Width of <paramref name="text"/> with every digit given the same advance.</summary>
    private static float Advance(SKFont font, string text, float digitAdvance)
    {
        float width = 0;
        foreach (char c in text)
            width += char.IsAsciiDigit(c) ? digitAdvance : font.MeasureText(c.ToString());
        return width;
    }

    private static void DrawValue(SKCanvas canvas, SKFont font, SKPaint paint, string text, float x, float baseline, float digitAdvance)
    {
        foreach (char c in text)
        {
            string glyph = c.ToString();
            if (char.IsAsciiDigit(c))
            {
                float own = font.MeasureText(glyph);
                canvas.DrawText(glyph, x + (digitAdvance - own) / 2, baseline, SKTextAlign.Left, font, paint);
                x += digitAdvance;
            }
            else
            {
                canvas.DrawText(glyph, x, baseline, SKTextAlign.Left, font, paint);
                x += font.MeasureText(glyph);
            }
        }
    }
}
