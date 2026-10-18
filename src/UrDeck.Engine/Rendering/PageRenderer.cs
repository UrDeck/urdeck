// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
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
    /// no dock, and nothing is reserved for it. A <paramref name="background"/> is painted over the theme's background colour,
    /// at the snapshot time (see <see cref="BackgroundClock.SnapshotTime"/>), before anything else.
    /// </summary>
    public static void Render(
        SKCanvas canvas,
        int widthPx,
        int heightPx,
        IReadOnlyList<(WidgetConfig Config, IWidget Widget)> widgets,
        LoadedTheme loaded,
        DateTime time,
        Chrome? chrome = null,
        IReadOnlyList<(WidgetConfig Config, IWidget? Widget)>? dock = null,
        DeckBackground? background = null)
    {
        // Pixels are physical here, so the theme resolves against this canvas's own cell size.
        using var theme = ThemeResolver.Resolve(loaded, widthPx / 4f);
        using var cardStyle = ResolvedCardStyle.Resolve(loaded, widthPx / 4f);
        canvas.Clear(theme.Background);
        if (background != null)
            PaintBackground(canvas, widthPx, heightPx, background);

        var screen = new System.Drawing.Size(widthPx, heightPx);
        var indicator = loaded.Definition.Indicator!;
        bool hasDock = dock is { Count: > 0 };
        double gap = loaded.Definition.Card!.Gap!.Value;
        var frame = ChromeLayout.Compute(screen, chrome?.Mode ?? IndicatorMode.Off, chrome?.PageCount ?? 1, indicator.BandHeight!.Value,
            hasDock ? loaded.Definition.Dock!.Height!.Value : 0);
        var layout = new GridLayoutManager(widthPx, heightPx, gap, frame.Grid.Height)
            .RenderWidgetLayout(widgets.Select(w => w.Config).ToList(), new System.Drawing.Size(widthPx, heightPx));
        int[] order = CardOrder.Assign(layout);
        var shown = Enumerable.Range(0, widgets.Count).Where(i => widgets[i].Config.IsVisible && layout[i].Placed).ToList();
        PaintUnderlay(canvas, shown.Select(i => (layout[i], order[i])), cardStyle, null);

        for (int i = 0; i < widgets.Count; i++)
        {
            var (config, widget) = widgets[i];
            if (!config.IsVisible)
                continue;

            var item = layout[i];
            if (!item.Placed)
                continue;
            PaintAt(canvas, widget, item, theme, new CardLook(cardStyle, order[i]), time);
        }

        int side = hasDock ? DockLayout.SlotSide(frame.Dock) : 0;
        if (side > 0)
        {
            // A slot is a small grid cell: the theme is resolved again for its size, so the card is a miniature.
            using var slotTheme = ThemeResolver.Resolve(loaded, side);
            using var slotStyle = ResolvedCardStyle.Resolve(loaded, side);
            var slots = DockLayout.Compute(frame.Dock, dock!.Count, gap);
            int[] slotOrder = CardOrder.Dock(slots.Count);
            var filled = Enumerable.Range(0, slots.Count).Where(i => dock[i].Widget != null && dock[i].Config.IsVisible).ToList();
            PaintUnderlay(canvas, filled.Select(i => (slots[i], slotOrder[i])), slotStyle, frame.Dock);
            for (int i = 0; i < slots.Count; i++)
            {
                var (config, widget) = dock[i];
                if (widget != null && config.IsVisible)
                    PaintAt(canvas, widget, slots[i], slotTheme, new CardLook(slotStyle, slotOrder[i]), time);
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
        IReadOnlyList<(WidgetConfig Config, IWidget? Widget)>? dock = null,
        DeckBackground? background = null)
    {
        var bitmap = new SKBitmap(widthPx, heightPx);
        using var canvas = new SKCanvas(bitmap);
        Render(canvas, widthPx, heightPx, widgets, loaded, time, chrome, dock, background);
        return bitmap;
    }

    /// <summary>
    /// A shader renders into a surface of its scale times the page's size and is stretched over the page, so a snapshot
    /// has the softness of the live layer; a picture is painted at full size. A failure is logged and the page is
    /// drawn without a background.
    /// </summary>
    private static void PaintBackground(SKCanvas canvas, int widthPx, int heightPx, DeckBackground background)
    {
        try
        {
            int save = canvas.Save();
            if (background.Kind == BackgroundKind.Shader && background.Scale < 1)
            {
                int width = Math.Max(1, (int)Math.Round(widthPx * background.Scale));
                int height = Math.Max(1, (int)Math.Round(heightPx * background.Scale));
                using var surface = SKSurface.Create(new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
                surface.Canvas.Clear(SKColors.Transparent);
                background.PaintAtTime(surface.Canvas, width, height, BackgroundClock.SnapshotTime);
                using var image = surface.Snapshot();
                canvas.DrawImage(image, SKRect.Create(widthPx, heightPx), new SKSamplingOptions(SKFilterMode.Linear));
            }
            else
            {
                background.PaintAtTime(canvas, widthPx, heightPx, BackgroundClock.SnapshotTime);
            }
            canvas.RestoreToCount(save);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Background: painting failed, the page is drawn without it: {ex.Message}");
        }
    }

    /// <summary>
    /// What the theme draws outside the cards, under all of them, with the routine the host's pictures use; the dock's is
    /// kept inside its band (<paramref name="band"/>), as the host clips it.
    /// </summary>
    private static void PaintUnderlay(SKCanvas canvas, IEnumerable<(WidgetLayoutItem Item, int Index)> cards, ResolvedCardStyle style, System.Drawing.Rectangle? band)
    {
        if (!style.HasOutside)
            return;
        int save = canvas.Save();
        if (band is { } b)
            canvas.ClipRect(SKRect.Create(b.X, b.Y, b.Width, b.Height));
        foreach (var (item, index) in cards)
        {
            int card = canvas.Save();
            canvas.Translate(item.Position.X, item.Position.Y);
            CardUnderlay.Draw(canvas, style, style.For(index, item.Size).Shape);
            canvas.RestoreToCount(card);
        }
        canvas.RestoreToCount(save);
    }

    /// <summary>Paints one card clipped to its rectangle, as its own surface clips it in the window.</summary>
    private static void PaintAt(SKCanvas canvas, IWidget widget, WidgetLayoutItem item, Theme theme, CardLook look, DateTime time)
    {
        int save = canvas.Save();
        canvas.Translate(item.Position.X, item.Position.Y);
        canvas.ClipRect(SKRect.Create(item.Size.Width, item.Size.Height));
        WidgetPainter.Paint(widget, canvas, item.Size, theme, look, time, CancellationToken.None);
        canvas.RestoreToCount(save);
    }
}
