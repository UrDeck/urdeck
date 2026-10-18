// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// The one paint path for a widget's surface, shared by the live window and snapshots: the host draws the card's back,
/// clips to its shape, lets the widget draw, then ends the card (<see cref="CardChrome"/>). A widget that throws is drawn
/// as a themed error card.
/// </summary>
public static class WidgetPainter
{
    /// <summary>As <see cref="Paint(IWidget, SKCanvas, System.Drawing.Size, Theme, CardLook?, DateTime, CancellationToken)"/> with the plain card.</summary>
    public static void Paint(
        IWidget widget,
        SKCanvas canvas,
        System.Drawing.Size size,
        Theme theme,
        DateTime time,
        CancellationToken cancellationToken) =>
        Paint(widget, canvas, size, theme, null, time, cancellationToken);

    /// <summary>
    /// Paints <paramref name="widget"/> on <paramref name="canvas"/>, whose origin is the top-left corner of the
    /// widget's card and whose size is <paramref name="size"/> (the card's size), in the card's <paramref name="look"/>
    /// (null, or a plain style, for the card drawn from <paramref name="theme"/> alone). The parts of the surface outside
    /// the card's shape are left untouched.
    /// </summary>
    public static void Paint(
        IWidget widget,
        SKCanvas canvas,
        System.Drawing.Size size,
        Theme theme,
        CardLook? look,
        DateTime time,
        CancellationToken cancellationToken)
    {
        var chrome = look is { Style: { IsPlain: false } style } ? style.For(look.Value.Index, size) : null;
        int save = CardChrome.DrawBack(canvas, size, theme, chrome);
        try
        {
            var context = new WidgetRenderContext(
                canvas, time, size, theme, ContentRect(size, theme), widget.Config, cancellationToken);
            widget.Render(context);
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{widget.Name}' render failed", ex);
            canvas.RestoreToCount(save);

            // Wipe whatever the widget managed to draw (its content is clipped to the shape), then show the error on a
            // fresh card with all of its chrome.
            int wipe = canvas.Save();
            CardChrome.ClipToShape(canvas, size, theme, chrome);
            canvas.DrawColor(SKColors.Transparent, SKBlendMode.Src);
            canvas.RestoreToCount(wipe);
            save = CardChrome.DrawBack(canvas, size, theme, chrome);
            DrawError(canvas, size, theme, widget.Name, ex.Message);
        }
        finally
        {
            CardChrome.DrawFront(canvas, chrome, save);
        }
    }

    /// <summary>The card's area inset by the theme's padding, never smaller than nothing.</summary>
    internal static SKRect ContentRect(System.Drawing.Size size, Theme theme)
    {
        float pad = Math.Min(theme.Padding, Math.Min(size.Width, size.Height) / 2f);
        return new SKRect(pad, pad, size.Width - pad, size.Height - pad);
    }

    private static void DrawError(SKCanvas canvas, System.Drawing.Size size, Theme theme, string name, string message)
    {
        var content = ContentRect(size, theme);
        using var paint = new SKPaint { Color = theme.Critical, IsAntialias = true };

        float y = content.Top;
        y = DrawLine(canvas, paint, theme.GetTypeface(TextRole.Title), theme.TitleSize, content, y, name);
        DrawLine(canvas, paint, theme.GetTypeface(TextRole.Body), theme.BodySize, content, y + theme.BodySize * 0.5f, message);
    }

    /// <summary>Draws one left-aligned line, shrunk to the content width, and returns the y below it.</summary>
    private static float DrawLine(SKCanvas canvas, SKPaint paint, SKTypeface typeface, float size, SKRect content, float top, string text)
    {
        using var font = new SKFont(typeface, size) { Subpixel = true, Edging = SKFontEdging.Antialias };
        float width = font.MeasureText(text);
        if (width > content.Width && width > 0)
            font.Size = size * content.Width / width;

        float cap = font.Metrics.CapHeight > 0 ? font.Metrics.CapHeight : -font.Metrics.Ascent * 0.7f;
        canvas.DrawText(text, content.Left, top + cap, SKTextAlign.Left, font, paint);
        return top + cap;
    }
}
