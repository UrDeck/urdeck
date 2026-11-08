// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Deck;

/// <summary>
/// Paints everything a deck session shows into one canvas, in the physical pixels of the session's surface: the theme's
/// colour, the background (with its scrim), the theme's pattern, each live page's underlay and cards at their offsets, the
/// moving borders, the dock and the page indicator. The window draws the same things in layers of its own; this is for a
/// presenter with one surface (the editor's preview), which scales the canvas to its size.
/// <see cref="PaintCached"/> keeps what does not change from frame to frame (the colour, the background, the pattern and the
/// underlays) as a picture at the output's size, so a frame paints only the cards and the indicator: on a software canvas the
/// background's shader and the shadows' blur would otherwise cost the whole frame. Deck thread only.
/// </summary>
public sealed class DeckFramePainter : IDisposable
{
    private PatternTile? _pattern;
    private (LoadedTheme Theme, System.Drawing.Size Size)? _patternFor;
    private SKImage? _back;
    private object? _backKey;
    private SKImage? _base;
    private object? _baseKey;

    /// <summary>How often the cached layers were made again, for tests and the cost log.</summary>
    public int LayerBuilds { get; private set; }

    /// <summary>
    /// Paints the session at <paramref name="elapsed"/> (the scheduler's clock): pages at their offsets, moving borders and a
    /// moving <paramref name="background"/> at that time when <paramref name="animate"/>, else still. Without a page only the
    /// theme's colour and the background are painted.
    /// </summary>
    public void Paint(SKCanvas canvas, DeckSession session, DeckBackground? background, TimeSpan elapsed, bool animate)
    {
        canvas.Clear(SKColor.Parse(session.Theme.Definition.Colors!.Background));
        if (session.Pager.Setup is not { } setup)
            return;
        var px = Pixels(setup);
        if (background != null)
            PaintBackground(canvas, background, px, animate ? elapsed : null);
        PaintPattern(canvas, session.Theme, px, (float)(px.Width / (double)GridLayoutManager.Columns));
        var motion = animate ? new FrameTime(elapsed.TotalSeconds, MotionLevel.Full) : (FrameTime?)null;
        PaintPages(canvas, session, elapsed, motion, underlays: true);
    }

    /// <summary>
    /// Paints the session into a canvas of <paramref name="output"/> pixels, the surface scaled to fit its width, with the
    /// background and the borders still. The background, pattern and underlays are painted once into a picture and reused until
    /// the theme, the background, the page or the output's size change.
    /// </summary>
    public void PaintCached(SKCanvas canvas, SKSizeI output, DeckSession session, DeckBackground? background)
    {
        var theme = session.Theme;
        if (session.Pager.Setup is not { } setup)
        {
            canvas.Clear(SKColor.Parse(theme.Definition.Colors!.Background));
            return;
        }

        var px = Pixels(setup);
        float scale = output.Width / (float)px.Width;

        var backKey = (theme, background, output, px);
        if (_back == null || !Equals(_backKey, backKey))
        {
            _back?.Dispose();
            _back = Layer(output, scale, c =>
            {
                c.Clear(SKColor.Parse(theme.Definition.Colors!.Background));
                if (background != null)
                    PaintBackground(c, background, px, null);
                PaintPattern(c, theme, px, (float)(px.Width / (double)GridLayoutManager.Columns));
            }, null);
            _backKey = backKey;
            _base?.Dispose();
            _base = null;
        }

        var pager = session.Pager;
        var baseKey = (_back, setup, pager.Current, pager.Dock);
        if (_base == null || !Equals(_baseKey, baseKey))
        {
            _base?.Dispose();
            _base = Layer(output, scale, c => PaintUnderlays(c, session), _back);
            _baseKey = baseKey;
        }

        canvas.DrawImage(_base, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        int save = canvas.Save();
        canvas.Scale(scale, scale);
        PaintPages(canvas, session, TimeSpan.Zero, null, underlays: false);
        canvas.RestoreToCount(save);
    }

    /// <summary>Forgets the cached layers (the next frame makes them again).</summary>
    public void Invalidate()
    {
        _base?.Dispose();
        _base = null;
        _baseKey = null;
    }

    private SKImage Layer(SKSizeI output, float scale, Action<SKCanvas> paint, SKImage? under)
    {
        LayerBuilds++;
        using var surface = SKSurface.Create(new SKImageInfo(Math.Max(1, output.Width), Math.Max(1, output.Height), SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        var c = surface.Canvas;
        c.Clear(SKColors.Transparent);
        if (under != null)
            c.DrawImage(under, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        c.Scale(scale, scale);
        paint(c);
        return surface.Snapshot();
    }

    private static System.Drawing.Size Pixels(PagerSetup setup) =>
        new(Math.Max(1, (int)Math.Round(setup.Screen.Width * setup.Scale)), Math.Max(1, (int)Math.Round(setup.Screen.Height * setup.Scale)));

    private static void PaintPages(SKCanvas canvas, DeckSession session, TimeSpan elapsed, FrameTime? motion, bool underlays)
    {
        var pager = session.Pager;
        var setup = pager.Setup!;
        double scale = setup.Scale;
        foreach (var page in new[] { pager.Current, pager.Neighbor })
        {
            if (page != null)
                PaintPage(canvas, page, pager.OffsetAt(page, elapsed), scale, motion, null, underlays);
        }

        if (pager.Dock is { } dock)
            PaintPage(canvas, dock, 0, scale, motion, dock.UnderlayClip, underlays);

        var chrome = setup.Chrome;
        if (chrome.Mode != IndicatorMode.Off && session.Navigator.Count > 0)
        {
            var area = new System.Drawing.RectangleF((float)(chrome.Indicator.X * scale), (float)(chrome.Indicator.Y * scale),
                (float)(chrome.Indicator.Width * scale), (float)(chrome.Indicator.Height * scale));
            PageIndicator.Paint(canvas, area, session.Navigator.Count, session.Navigator.Index, 1f, chrome.Mode == IndicatorMode.Fade, setup.Style);
        }
    }

    private static void PaintUnderlays(SKCanvas canvas, DeckSession session)
    {
        var pager = session.Pager;
        double scale = pager.Setup!.Scale;
        if (pager.Current is { } current)
            PaintUnderlay(canvas, current, pager.OffsetAt(current, TimeSpan.Zero), scale, null);
        if (pager.Dock is { } dock)
            PaintUnderlay(canvas, dock, 0, scale, dock.UnderlayClip);
    }

    private static void PaintBackground(SKCanvas canvas, DeckBackground background, System.Drawing.Size px, TimeSpan? elapsed)
    {
        float time = elapsed is { } e && background.IsMoving
            ? BackgroundClock.SnapshotTime + BackgroundClock.Wrap(e.TotalSeconds, background.Speed)
            : BackgroundClock.SnapshotTime;
        try
        {
            int save = canvas.Save();
            background.PaintAtTime(canvas, px.Width, px.Height, time);
            canvas.RestoreToCount(save);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Background: painting failed, the preview is drawn without it: {ex.Message}");
        }
    }

    private void PaintPattern(SKCanvas canvas, LoadedTheme theme, System.Drawing.Size px, float cellPx)
    {
        if (_patternFor is not { } key || !ReferenceEquals(key.Theme, theme) || key.Size != px)
        {
            _pattern?.Dispose();
            _pattern = PatternTile.Create(theme, cellPx, px);
            _patternFor = (theme, px);
        }

        if (_pattern == null)
            return;
        using var shader = _pattern.Image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, new SKSamplingOptions(SKFilterMode.Nearest));
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, 0, px.Width, px.Height, paint);
    }

    // What the theme draws outside the cards, under all of them.
    private static void PaintUnderlay(SKCanvas canvas, PageSlots page, double offset, double scale, System.Drawing.Rectangle? clip)
    {
        int save = canvas.Save();
        if (clip is { } band)
            canvas.ClipRect(SKRect.Create((float)(band.X * scale), (float)(band.Y * scale), (float)(band.Width * scale), (float)(band.Height * scale)));
        foreach (var card in page.Cards)
        {
            if (!card.Style.HasOutside)
                continue;
            var size = SizeOf(card, scale);
            int c = canvas.Save();
            canvas.Translate((float)((card.Item.Position.X + offset) * scale), (float)(card.Item.Position.Y * scale));
            CardUnderlay.Draw(canvas, card.Style, card.Style.For(card.Order, size).Shape);
            canvas.RestoreToCount(c);
        }

        canvas.RestoreToCount(save);
    }

    private static void PaintPage(SKCanvas canvas, PageSlots page, double offset, double scale, FrameTime? motion, System.Drawing.Rectangle? clip, bool underlays)
    {
        if (underlays)
            PaintUnderlay(canvas, page, offset, scale, clip);

        foreach (var card in page.Cards)
        {
            var size = SizeOf(card, scale);
            int c = canvas.Save();
            canvas.Translate((float)((card.Item.Position.X + offset) * scale), (float)(card.Item.Position.Y * scale));
            canvas.ClipRect(new SKRect(0, 0, size.Width, size.Height));
            card.Slot.Paint(canvas, size);
            // A moving border is a sprite over the card in the window; here it is drawn over the card, at the frame's step or still.
            if (card.AnimatedBorder && BorderFrame.StepAt(card.Style, motion ?? new FrameTime(0, MotionLevel.Full)) is { } step)
                BorderFrame.Draw(canvas, card.Style, size, step);
            canvas.RestoreToCount(c);
        }
    }

    private static System.Drawing.Size SizeOf(DeckCard card, double scale) =>
        new(Math.Max(1, (int)Math.Round(card.Item.Size.Width * scale)), Math.Max(1, (int)Math.Round(card.Item.Size.Height * scale)));

    public void Dispose()
    {
        _pattern?.Dispose();
        _pattern = null;
        _patternFor = null;
        _back?.Dispose();
        _back = null;
        _backKey = null;
        Invalidate();
    }
}
