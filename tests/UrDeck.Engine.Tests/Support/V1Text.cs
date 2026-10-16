// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A copy of <see cref="Readout"/> and <see cref="TextLine"/> drawing as they were before the theme's text style existed
/// (theme-typography). A theme that sets no style must draw the same bytes as this; do not change it to follow the components.
/// </summary>
internal static class V1Text
{
    private const float ReferenceSize = 100f;
    private const float UnitGapRatio = 0.06f;
    private const float LabelGapRatio = 1.2f;

    private static SKFont CreateFont(SKTypeface typeface, float size) =>
        new(typeface, size) { Subpixel = true, Edging = SKFontEdging.Antialias };

    private static float CapHeight(SKFont font)
    {
        float cap = font.Metrics.CapHeight;
        return cap > 0 ? cap : -font.Metrics.Ascent * 0.7f;
    }

    private static float DigitAdvance(SKFont font)
    {
        float widest = 0;
        for (char d = '0'; d <= '9'; d++)
            widest = Math.Max(widest, font.MeasureText(d.ToString()));
        return widest;
    }

    public static void Readout(SKCanvas canvas, Theme theme, SKRect rect, string value, ReadoutOptions options)
    {
        bool hasUnit = !string.IsNullOrEmpty(options.Unit);
        bool hasLabel = !string.IsNullOrEmpty(options.Label);
        float ratio = theme.UnitRatio;

        using var valueFont = CreateFont(theme.GetTypeface(TextRole.Value), ReferenceSize);
        using var unitFont = CreateFont(theme.GetTypeface(TextRole.Unit), ReferenceSize);
        using var labelFont = CreateFont(theme.GetTypeface(TextRole.Label), theme.LabelSize);

        float digit = DigitAdvance(valueFont);
        float unitWidth = hasUnit ? unitFont.MeasureText(options.Unit!) : 0f;
        float valueCap = CapHeight(valueFont);
        float labelBlock = hasLabel ? CapHeight(labelFont) + theme.LabelSize * LabelGapRatio : 0f;

        float sampleWidth = Advance(valueFont, options.WidestValue ?? value, digit);
        float widthPerSize = (sampleWidth + (hasUnit ? unitWidth * theme.UnitRatio + UnitGapRatio * ReferenceSize : 0f)) / ReferenceSize;
        float fit = Math.Min(rect.Width / widthPerSize, Math.Max(1f, rect.Height - labelBlock) / (valueCap / ReferenceSize));
        float size = Math.Max(1f, fit * Math.Clamp(options.Scale, 0.1f, 1f));
        float k = size / ReferenceSize;

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

        valueFont.Size = size;
        unitFont.Size = size * ratio;
        using var valuePaint = new SKPaint { Color = options.ValueColor ?? theme.Text, IsAntialias = true };
        float x = left;
        foreach (char c in value)
        {
            string glyph = c.ToString();
            if (char.IsAsciiDigit(c))
            {
                float own = valueFont.MeasureText(glyph);
                canvas.DrawText(glyph, x + (digit * k - own) / 2, baseline, SKTextAlign.Left, valueFont, valuePaint);
                x += digit * k;
            }
            else
            {
                canvas.DrawText(glyph, x, baseline, SKTextAlign.Left, valueFont, valuePaint);
                x += valueFont.MeasureText(glyph);
            }
        }

        if (hasUnit)
        {
            float unitX = left + actualValueWidth + unitGap;
            float unitBaseline = options.UnitPlacement == UnitPlacement.Raised ? top + CapHeight(unitFont) : baseline;
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
            using var labelPaint = new SKPaint { Color = options.LabelColor ?? theme.TextMuted, IsAntialias = true };
            canvas.DrawText(options.Label!, labelX, baseline + labelBlock, SKTextAlign.Left, labelFont, labelPaint);
        }
    }

    public static void TextLine(SKCanvas canvas, Theme theme, SKRect rect, string text, TextStep step, TextEmphasis emphasis,
        HorizontalAlign horizontal, VerticalAlign vertical)
    {
        var role = step switch { TextStep.Label => TextRole.Label, TextStep.Body => TextRole.Body, _ => TextRole.Title };
        float size = theme.GetTextSize(step);

        using var font = CreateFont(theme.GetTypeface(role), size);
        float width = font.MeasureText(text);
        if (width > rect.Width)
        {
            font.Size = size * rect.Width / width;
            width = font.MeasureText(text);
        }

        float cap = CapHeight(font);
        float left = horizontal switch
        {
            HorizontalAlign.Left => rect.Left,
            HorizontalAlign.Right => rect.Right - width,
            _ => rect.MidX - width / 2,
        };
        float top = vertical switch
        {
            VerticalAlign.Top => rect.Top,
            VerticalAlign.Bottom => rect.Bottom - cap,
            _ => rect.MidY - cap / 2,
        };

        using var paint = new SKPaint { Color = emphasis == TextEmphasis.Muted ? theme.TextMuted : theme.Text, IsAntialias = true };
        canvas.DrawText(text, left, top + cap, SKTextAlign.Left, font, paint);
    }

    private static float Align(HorizontalAlign align, float left, float right, float width) => align switch
    {
        HorizontalAlign.Left => left,
        HorizontalAlign.Right => right - width,
        _ => (left + right) / 2 - width / 2,
    };

    private static float Advance(SKFont font, string text, float digitAdvance)
    {
        float width = 0;
        foreach (char c in text)
            width += char.IsAsciiDigit(c) ? digitAdvance : font.MeasureText(c.ToString());
        return width;
    }
}
