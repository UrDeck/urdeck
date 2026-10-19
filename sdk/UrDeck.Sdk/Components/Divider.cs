// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The rule between two sections of a widget, drawn along the vertical middle of a rectangle as <see cref="Theme.Divider"/>
/// says: a line, a line fading to a second colour, or a dimension rule with a tick at each end and room for a centred label.
/// A theme that sets no divider gives the rule the Weather card always drew.
/// </summary>
public static class Divider
{
    /// <summary>Draws the rule across <paramref name="rect"/>; a <paramref name="label"/> is centred in a dimension rule.</summary>
    public static void Draw(SKCanvas canvas, Theme theme, SKRect rect, string? label = null)
    {
        var divider = theme.Divider;
        if (divider.Style == DividerStyle.None || rect.Width <= 0)
            return;

        float y = rect.MidY;
        using var paint = new SKPaint { Color = divider.Color, IsAntialias = true, StrokeWidth = divider.Thickness };

        switch (divider.Style)
        {
            case DividerStyle.Gradient:
                paint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(rect.Left, y), new SKPoint(rect.Right, y),
                    [divider.Color, divider.GradientTo ?? divider.Color.WithAlpha(0)], null, SKShaderTileMode.Clamp);
                canvas.DrawLine(rect.Left, y, rect.Right, y, paint);
                break;
            case DividerStyle.Dimension:
                DrawDimension(canvas, theme, rect, y, label, paint, divider.Thickness);
                break;
            default:
                canvas.DrawLine(rect.Left, y, rect.Right, y, paint);
                break;
        }
    }

    private static void DrawDimension(SKCanvas canvas, Theme theme, SKRect rect, float y, string? label, SKPaint paint, float thickness)
    {
        float tick = Math.Max(thickness * 6, 4f);
        canvas.DrawLine(rect.Left, y - tick / 2, rect.Left, y + tick / 2, paint);
        canvas.DrawLine(rect.Right, y - tick / 2, rect.Right, y + tick / 2, paint);

        if (string.IsNullOrEmpty(label))
        {
            canvas.DrawLine(rect.Left, y, rect.Right, y, paint);
            return;
        }

        // The rule breaks round the label, with half a label height of space either side.
        var slot = SKRect.Create(rect.Left + tick, y - tick, Math.Max(0, rect.Width - 2 * tick), 2 * tick);
        var drawn = TextLine.Draw(canvas, theme, slot, label, TextStep.Label, TextEmphasis.Muted);
        float room = theme.GetTextSize(TextStep.Label) * 0.5f;
        if (drawn.Left - room > rect.Left)
            canvas.DrawLine(rect.Left, y, drawn.Left - room, y, paint);
        if (drawn.Right + room < rect.Right)
            canvas.DrawLine(drawn.Right + room, y, rect.Right, y, paint);
    }
}
