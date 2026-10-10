// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// Renders a whole page of widgets, and the dock, onto a single canvas. The live host draws each widget into its own
/// element instead; this path is for snapshots and tests, and uses the same layout, card and widget code.
/// </summary>
public static class PageRenderer
{
    /// <summary>The page indicator to draw: the configured mode, which page is shown and how many there are.</summary>
    public sealed record Chrome(IndicatorMode Mode, int PageIndex, int PageCount);

    /// <summary>
    /// Draws the page and, below it, the dock. <paramref name="dock"/> holds the dock's slots in order (see
    /// <see cref="DockLayout.Select"/>), each with its widget or null for a slot that stays empty; null or empty means
    /// no dock, and nothing is reserved for it.
    /// </summary>
    public static void Render(
        SKCanvas canvas,
        int widthPx,
        int heightPx,
        IReadOnlyList<(WidgetConfig Config, IWidget Widget)> widgets,
        LoadedTheme loaded,
        DateTime time,
        Chrome? chrome = null,
        IReadOnlyList<(WidgetConfig Config, IWidget? Widget)>? dock = null)
    {
        // Pixels are physical here, so the theme resolves against this canvas's own cell size.
        using var theme = ThemeResolver.Resolve(loaded, widthPx / 4f);
        canvas.Clear(theme.Background);

        var screen = new System.Drawing.Size(widthPx, heightPx);
        var indicator = loaded.Definition.Indicator!;
        bool hasDock = dock is { Count: > 0 };
        double gap = loaded.Definition.Card!.Gap!.Value;
        var frame = ChromeLayout.Compute(screen, chrome?.Mode ?? IndicatorMode.Off, chrome?.PageCount ?? 1, indicator.BandHeight!.Value,
            hasDock ? loaded.Definition.Dock!.Height!.Value : 0);
        var layout = new GridLayoutManager(widthPx, heightPx, gap, frame.Grid.Height)
            .RenderWidgetLayout(widgets.Select(w => w.Config).ToList(), new System.Drawing.Size(widthPx, heightPx));

        for (int i = 0; i < widgets.Count; i++)
        {
            var (config, widget) = widgets[i];
            if (!config.IsVisible)
                continue;

            var item = layout[i];
            if (!item.Placed)
                continue;
            PaintAt(canvas, widget, item, theme, time);
        }

        int side = hasDock ? DockLayout.SlotSide(frame.Dock) : 0;
        if (side > 0)
        {
            // A slot is a small grid cell: the theme is resolved again for its size, so the card is a miniature.
            using var slotTheme = ThemeResolver.Resolve(loaded, side);
            var slots = DockLayout.Compute(frame.Dock, dock!.Count, gap);
            for (int i = 0; i < slots.Count; i++)
            {
                var (config, widget) = dock[i];
                if (widget != null && config.IsVisible)
                    PaintAt(canvas, widget, slots[i], slotTheme, time);
            }
        }

        if (chrome != null && frame.Mode != IndicatorMode.Off)
        {
            // A snapshot has no fade: the indicator is always at full opacity.
            PageIndicator.Paint(canvas, frame.Indicator, chrome.PageCount, chrome.PageIndex, 1f,
                frame.Mode == IndicatorMode.Fade, IndicatorStyle.Resolve(loaded, widthPx / 4f));
        }
    }

    public static SKBitmap RenderToBitmap(
        int widthPx,
        int heightPx,
        IReadOnlyList<(WidgetConfig Config, IWidget Widget)> widgets,
        LoadedTheme loaded,
        DateTime time,
        Chrome? chrome = null,
        IReadOnlyList<(WidgetConfig Config, IWidget? Widget)>? dock = null)
    {
        var bitmap = new SKBitmap(widthPx, heightPx);
        using var canvas = new SKCanvas(bitmap);
        Render(canvas, widthPx, heightPx, widgets, loaded, time, chrome, dock);
        return bitmap;
    }

    private static void PaintAt(SKCanvas canvas, IWidget widget, WidgetLayoutItem item, Theme theme, DateTime time)
    {
        int save = canvas.Save();
        canvas.Translate(item.Position.X, item.Position.Y);
        WidgetPainter.Paint(widget, canvas, item.Size, theme, time, CancellationToken.None);
        canvas.RestoreToCount(save);
    }
}
