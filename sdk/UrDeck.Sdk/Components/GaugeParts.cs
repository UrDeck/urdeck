// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The status row above a gauge and the sparkline under it. They are drawn round the gauge, which gets the rectangle that is
/// left (and no label when the row carries it), so every style takes them the same way.
/// </summary>
internal readonly struct GaugeParts
{
    public SKRect Inner { get; init; }
    public SKRect? StatusRow { get; init; }
    public SKRect? SparklineRect { get; init; }
    public GaugeOptions Options { get; init; }

    public bool Any => StatusRow != null || SparklineRect != null;

    public static GaugeParts Plan(Theme theme, SKRect rect, GaugeOptions options)
    {
        SKRect? row = null;
        SKRect? spark = null;
        var inner = rect;
        float gap = theme.Gap / 2;

        if (theme.Gauge.Status != null && options.Status != null)
        {
            float height = Math.Min(theme.LabelSize * 1.5f, rect.Height * 0.25f);
            row = SKRect.Create(rect.Left, rect.Top, rect.Width, height);
            inner = new SKRect(inner.Left, inner.Top + height + gap, inner.Right, inner.Bottom);
        }

        if (theme.Gauge.Sparkline is { } sparkline && options.History is { Count: >= 2 } && rect.Height >= sparkline.MinRectHeight)
        {
            float height = Math.Min(sparkline.Height, rect.Height * 0.4f);
            spark = SKRect.Create(rect.Left, rect.Bottom - height, rect.Width, height);
            inner = new SKRect(inner.Left, inner.Top, inner.Right, inner.Bottom - height - gap);
        }

        return new GaugeParts { Inner = inner, StatusRow = row, SparklineRect = spark, Options = options.ForShape(row != null) };
    }

    /// <summary>Draws the label and the status chip on the row, and the sparkline in the colour of the fill.</summary>
    public void Draw(SKCanvas canvas, Theme theme, Data.ReadingText reading, GaugeOptions options)
    {
        if (StatusRow is { } row && options.Status is { } status)
        {
            var chip = Chip.Measure(theme, row, status.Text, TextStep.Label, HorizontalAlign.Right);
            float chipWidth = Math.Min(chip.Width, row.Width * 0.5f);
            float gap = theme.Gap / 2;
            if (!string.IsNullOrEmpty(options.Label))
            {
                var labelRect = new SKRect(row.Left, row.Top, Math.Max(row.Left, row.Right - chipWidth - gap), row.Bottom);
                TextLine.Draw(canvas, theme, labelRect, options.Label, TextStep.Label, TextEmphasis.Muted, HorizontalAlign.Left);
            }

            Chip.Draw(canvas, theme, new SKRect(row.Right - chipWidth, row.Top, row.Right, row.Bottom), status.Text, status.Level,
                TextStep.Label, HorizontalAlign.Right);
        }

        if (SparklineRect is { } spark && options.History is { } history)
        {
            float? fraction = options.Fraction is { } f ? Math.Clamp(f, 0f, 1f) : null;
            Sparkline.Draw(canvas, theme, spark, history, GaugeContext.FillColor(theme, reading, options, fraction));
        }
    }
}
