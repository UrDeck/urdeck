// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The text-layout capability: overflow, marquee, block and cache. One collection: the cache is process-wide.</summary>
[Collection("TextLayoutCache")]
public sealed class TextLayoutTests : IDisposable
{
    private const string Long = "The Ballad of a Very Long Track Title That Goes On";

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

    public void Dispose() => _theme.Dispose();

    private static readonly SKRect Slot = new(0, 0, 300, 60);

    private static SKBitmap Render(Action<SKCanvas> draw)
    {
        var bitmap = new SKBitmap(Slot.RightAsInt() + 20, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        draw(canvas);
        return bitmap;
    }

    private static bool Same(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    private SKFont BodyFont() => TextMetrics.CreateFont(_theme.GetTypeface(TextRole.Body), _theme.BodySize);

    // ---- Ellipsis and clip

    [Fact]
    public void Ellipsis_cuts_the_text_and_fits_the_rectangle()
    {
        using var font = BodyFont();
        string cut = TextMetrics.Ellipsize(font, Long, 0, 200);
        Assert.EndsWith("…", cut);
        Assert.True(cut.Length < Long.Length);
        Assert.True(font.MeasureText(cut) <= 200);

        var drawn = TextLine.Measure(_theme, new SKRect(0, 0, 200, 40), Long, TextStep.Body, overflow: TextOverflow.Ellipsis);
        Assert.True(drawn.Width <= 200 + 0.01f);
    }

    [Fact]
    public void Ellipsis_keeps_the_theme_size()
    {
        // The shrink answer would make the line much narrower per character; the ellipsis answer fills the rectangle.
        var ellipsis = TextLine.Measure(_theme, new SKRect(0, 0, 200, 40), Long, TextStep.Body, overflow: TextOverflow.Ellipsis);
        var shrink = TextLine.Measure(_theme, new SKRect(0, 0, 200, 40), Long, TextStep.Body);
        Assert.True(ellipsis.Height >= shrink.Height);
        Assert.True(ellipsis.Width > 150);
    }

    [Fact]
    public void Text_that_fits_is_drawn_whole()
    {
        using var font = BodyFont();
        Assert.Equal("CPU", TextMetrics.Ellipsize(font, "CPU", 0, 200));
        using var plain = Render(c => TextLine.Draw(c, _theme, Slot, "CPU"));
        using var ellipsis = Render(c => TextLine.Draw(c, _theme, Slot, "CPU", overflow: TextOverflow.Ellipsis));
        Assert.True(Same(plain, ellipsis));
    }

    [Fact]
    public void Nothing_is_drawn_when_not_even_the_ellipsis_fits()
    {
        using var font = BodyFont();
        Assert.Equal("", TextMetrics.Ellipsize(font, Long, 0, 1));
        var drawn = TextLine.Measure(_theme, new SKRect(0, 0, 1, 40), Long, TextStep.Body, overflow: TextOverflow.Ellipsis);
        Assert.Equal(0, drawn.Width);
    }

    [Theory]
    [InlineData("xxxxxxxxxxxxxxxxxxxx\U0001F468‍\U0001F469‍\U0001F467yyyy")]
    [InlineData("xxxxxxxxxxxxxxxxxxxx\U0001F1E9\U0001F1EAyyyyyy")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxéééyyyy")]
    public void The_cut_never_falls_inside_a_character(string text)
    {
        using var font = BodyFont();
        int[] boundaries = [.. StringInfo.ParseCombiningCharacters(text), text.Length];
        for (float width = 20; width < font.MeasureText(text); width += 3)
        {
            string cut = TextMetrics.Ellipsize(font, text, 0, width);
            if (cut.Length == 0)
                continue;
            string kept = cut[..^1];
            Assert.StartsWith(kept, text);
            Assert.Contains(kept.Length, boundaries);
        }
    }

    [Fact]
    public void Trailing_spaces_before_the_ellipsis_are_dropped()
    {
        using var font = BodyFont();
        string cut = TextMetrics.Ellipsize(font, "aaaa bbbb cccc dddd eeee ffff", 0, font.MeasureText("aaaa bbbb ") + font.MeasureText("…") + 0.5f);
        Assert.DoesNotContain(" …", cut);
    }

    [Fact]
    public void Clip_draws_at_the_theme_size_and_nothing_outside()
    {
        var narrow = new SKRect(0, 20, 120, 60);
        using var bitmap = Render(c => TextLine.Draw(c, _theme, narrow, Long, horizontal: HorizontalAlign.Left, overflow: TextOverflow.Clip));
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 120; x < bitmap.Width; x++)
                Assert.Equal(SKColors.Black, bitmap.GetPixel(x, y));
        bool inside = false;
        for (int y = 0; y < bitmap.Height && !inside; y++)
            for (int x = 0; x < 120; x++)
                inside |= bitmap.GetPixel(x, y) != SKColors.Black;
        Assert.True(inside);
    }

    // ---- Unchanged default

    /// <summary>The line as it was drawn before the overflow choice existed (shrink only, left or centred).</summary>
    private SKRect Reference(SKCanvas canvas, SKRect rect, string text)
    {
        float size = _theme.GetTextSize(TextStep.Body);
        text = TextStyle.Apply(TextStyle.CaseFor(_theme.TextStyle, TextStep.Body), text);
        float tracking = TextStyle.TrackingFor(_theme.TextStyle, TextStep.Body);
        using var font = TextMetrics.CreateFont(_theme.GetTypeface(TextRole.Body), size);
        float width = TextMetrics.Measure(font, text, tracking);
        if (width > rect.Width)
        {
            font.Size = size * rect.Width / width;
            width = TextMetrics.Measure(font, text, tracking);
        }

        float cap = TextMetrics.CapHeight(font);
        float left = rect.MidX - width / 2;
        float top = rect.MidY - cap / 2;
        using var paint = new SKPaint { Color = _theme.Text, IsAntialias = true };
        if (tracking == 0)
            canvas.DrawText(text, left, top + cap, SKTextAlign.Left, font, paint);
        else
            TextMetrics.DrawTracked(canvas, text, left, top + cap, font, paint, tracking);
        return SKRect.Create(left, top, width, cap);
    }

    [Theory]
    [InlineData("CPU")]
    [InlineData(Long)]
    public void A_call_without_overflow_is_the_untouched_path_byte_for_byte(string text)
    {
        SKRect expectedRect = default;
        SKRect actualRect = default;
        using var expected = Render(c => expectedRect = Reference(c, Slot, text));
        using var actual = Render(c => actualRect = TextLine.Draw(c, _theme, Slot, text));
        Assert.True(Same(expected, actual));
        Assert.Equal(expectedRect, actualRect);
    }

    // ---- Marquee

    [Fact]
    public void Marquee_rests_scrolls_rests_and_returns()
    {
        const float overflow = 100, size = 20;
        double scroll = overflow / (size * Marquee.SizesPerSecond);
        double cycle = Marquee.PauseSeconds + scroll + Marquee.PauseSeconds + Marquee.ReturnSeconds;

        Assert.Equal(0, Marquee.Offset(0, overflow, size));
        Assert.Equal(0, Marquee.Offset(Marquee.PauseSeconds - 0.01, overflow, size));
        float mid = Marquee.Offset(Marquee.PauseSeconds + scroll / 2, overflow, size);
        Assert.InRange(mid, 45, 55);
        Assert.Equal(overflow, Marquee.Offset(Marquee.PauseSeconds + scroll + 0.5, overflow, size), 0.01);
        Assert.InRange(Marquee.Offset(cycle - 0.25, overflow, size), 1, 99);
        Assert.Equal(Marquee.Offset(0.7, overflow, size), Marquee.Offset(0.7 + 3 * cycle, overflow, size), 0.01);
        Assert.Equal(0, Marquee.Offset(5, 0, size));
    }

    [Fact]
    public void Marquee_position_depends_only_on_time()
    {
        var at = new DateTime(2026, 11, 3, 12, 0, 2, DateTimeKind.Local);
        using var a = Render(c => TextLine.Draw(c, _theme, Slot, Long, overflow: TextOverflow.Marquee, time: at));
        using var b = Render(c => TextLine.Draw(c, _theme, Slot, Long, overflow: TextOverflow.Marquee, time: at));
        Assert.True(Same(a, b));
    }

    [Fact]
    public void Marquee_moves_between_the_rests()
    {
        var start = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Local);
        using var rest = Render(c => TextLine.Draw(c, _theme, Slot, Long, overflow: TextOverflow.Marquee, time: start));
        using var later = Render(c => TextLine.Draw(c, _theme, Slot, Long, overflow: TextOverflow.Marquee, time: start.AddSeconds(Marquee.PauseSeconds + 1)));
        Assert.False(Same(rest, later));
    }

    [Fact]
    public void Marquee_at_rest_shows_the_start_of_the_line_clipped()
    {
        var at = StartOfCycle(Long);
        using var marquee = Render(c => TextLine.Draw(c, _theme, Slot, Long, horizontal: HorizontalAlign.Left, overflow: TextOverflow.Marquee, time: at));
        using var clip = Render(c => TextLine.Draw(c, _theme, Slot, Long, horizontal: HorizontalAlign.Left, overflow: TextOverflow.Clip));
        Assert.True(Same(marquee, clip));
    }

    /// <summary>A time 0.1 s into a marquee cycle for <paramref name="text"/> in the test slot, so the line is at rest at its start.</summary>
    private DateTime StartOfCycle(string text)
    {
        float overflow = TextLayoutCache.Width(_theme, TextStep.Body, text) - Slot.Width;
        double scroll = overflow / (_theme.BodySize * Marquee.SizesPerSecond);
        double cycle = Marquee.PauseSeconds + scroll + Marquee.PauseSeconds + Marquee.ReturnSeconds;
        var anyTime = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Local);
        double seconds = anyTime.Ticks / (double)TimeSpan.TicksPerSecond;
        return anyTime.AddSeconds(0.1 - seconds % cycle);
    }

    [Theory]
    [InlineData(MotionLevel.Reduced)]
    [InlineData(MotionLevel.Off)]
    public void Marquee_below_full_motion_is_an_ellipsis_that_never_moves(MotionLevel motion)
    {
        var t1 = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Local);
        using var a = Render(c => TextLine.Draw(c, _theme, Slot, Long, overflow: TextOverflow.Marquee, time: t1, motion: motion));
        using var b = Render(c => TextLine.Draw(c, _theme, Slot, Long, overflow: TextOverflow.Marquee, time: t1.AddSeconds(2), motion: motion));
        using var ellipsis = Render(c => TextLine.Draw(c, _theme, Slot, Long, overflow: TextOverflow.Ellipsis));
        Assert.True(Same(a, b));
        Assert.True(Same(a, ellipsis));
        Assert.False(TextLine.IsMoving(_theme, Slot, Long, overflow: TextOverflow.Marquee, motion: motion));
    }

    [Fact]
    public void A_marquee_line_that_fits_is_still_and_asks_for_no_frames()
    {
        Assert.False(TextLine.IsMoving(_theme, Slot, "CPU", overflow: TextOverflow.Marquee));
        Assert.True(TextLine.IsMoving(_theme, Slot, Long, overflow: TextOverflow.Marquee));
        Assert.False(TextLine.IsMoving(_theme, Slot, Long, overflow: TextOverflow.Ellipsis));
        Assert.False(TextLine.IsMoving(_theme, Slot, "", overflow: TextOverflow.Marquee));
    }

    // ---- Cache

    [Fact]
    public void A_repeated_layout_measures_nothing()
    {
        TextLayoutCache.Clear();
        string text = "repeated layout " + Guid.NewGuid();
        TextLayoutCache.Lines(_theme, TextStep.Body, text, 180);
        int before = TextLayoutCache.Layouts;
        for (int i = 0; i < 20; i++)
            TextLayoutCache.Lines(_theme, TextStep.Body, text, 180);
        Assert.Equal(before, TextLayoutCache.Layouts);
        TextLayoutCache.Lines(_theme, TextStep.Body, text, 181);
        Assert.Equal(before + 1, TextLayoutCache.Layouts);
    }

    [Fact]
    public void A_theme_change_lays_out_again()
    {
        TextLayoutCache.Clear();
        using var other = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 400);
        TextLayoutCache.Lines(_theme, TextStep.Body, "theme change", 180);
        int before = TextLayoutCache.Layouts;
        TextLayoutCache.Lines(other, TextStep.Body, "theme change", 180);
        Assert.Equal(before + 1, TextLayoutCache.Layouts);
        TextLayoutCache.Lines(_theme, TextStep.Body, "theme change", 180);
        Assert.Equal(before + 1, TextLayoutCache.Layouts);
    }

    [Fact]
    public void The_cache_is_bounded_and_drops_the_least_recently_used()
    {
        TextLayoutCache.Clear();
        TextLayoutCache.Lines(_theme, TextStep.Body, "keep me", 180);
        for (int i = 0; i < 700; i++)
        {
            TextLayoutCache.Lines(_theme, TextStep.Body, "filler " + i, 180);
            if (i % 50 == 0)
                TextLayoutCache.Lines(_theme, TextStep.Body, "keep me", 180);
        }

        Assert.True(TextLayoutCache.Count <= 512);
        int before = TextLayoutCache.Layouts;
        TextLayoutCache.Lines(_theme, TextStep.Body, "keep me", 180);
        Assert.Equal(before, TextLayoutCache.Layouts);
        TextLayoutCache.Lines(_theme, TextStep.Body, "filler 0", 180);
        Assert.Equal(before + 1, TextLayoutCache.Layouts);
    }

    // ---- Block

    private const string Sentence = "the quick brown fox jumps over the lazy dog and keeps running far away";

    private float LineHeight => _theme.BodySize * TextBlock.LineSpacing;

    private SKRect Tall(float width, int lines) => new(0, 0, width, LineHeight * lines + 1);

    [Fact]
    public void A_block_wraps_at_spaces_without_changing_the_size()
    {
        using var font = BodyFont();
        float width = font.MeasureText("the quick brown fox") + 2;
        var result = TextBlock.Measure(_theme, Tall(width, 10), Sentence);
        Assert.True(result.Lines >= 3);
        Assert.False(result.Truncated);
        Assert.True(result.Bounds.Width <= width);
        Assert.Equal(result.Lines * LineHeight, result.Bounds.Height, 0.01);
        foreach (string line in TextLayoutCache.Lines(_theme, TextStep.Body, Sentence, width))
            Assert.True(font.MeasureText(line) <= width);
    }

    [Fact]
    public void A_line_limit_ends_the_last_line_in_an_ellipsis()
    {
        using var font = BodyFont();
        float width = font.MeasureText("the quick brown fox") + 2;
        var open = TextBlock.Measure(_theme, Tall(width, 10), Sentence);
        var limited = TextBlock.Measure(_theme, Tall(width, 10), Sentence, maxLines: 2);
        Assert.Equal(2, limited.Lines);
        Assert.True(limited.Truncated);
        Assert.True(open.Lines > 2);

        using var plain = Render(c => TextBlock.Draw(c, _theme, Tall(width, 10), Sentence, maxLines: open.Lines));
        using var cut = Render(c => TextBlock.Draw(c, _theme, Tall(width, 10), Sentence, maxLines: 2));
        Assert.False(Same(plain, cut));
    }

    [Fact]
    public void A_block_with_room_for_every_line_has_no_ellipsis()
    {
        using var font = BodyFont();
        float width = font.MeasureText("the quick brown fox") + 2;
        int needed = TextLayoutCache.Lines(_theme, TextStep.Body, Sentence, width).Count;
        var result = TextBlock.Measure(_theme, Tall(width, needed), Sentence, maxLines: needed);
        Assert.False(result.Truncated);
        Assert.Equal(needed, result.Lines);
    }

    [Fact]
    public void A_word_wider_than_the_line_breaks_between_characters()
    {
        using var font = BodyFont();
        string url = "https://example.com/a/very/long/path/that/does/not/break";
        float width = font.MeasureText(url) / 3;
        var lines = TextLayoutCache.Lines(_theme, TextStep.Body, url, width);
        Assert.True(lines.Count >= 3);
        Assert.All(lines, l => Assert.True(font.MeasureText(l) <= width));
        Assert.Equal(url, string.Concat(lines));
    }

    [Fact]
    public void A_short_rectangle_shows_fewer_lines_and_ends_in_an_ellipsis()
    {
        using var font = BodyFont();
        float width = font.MeasureText("the quick brown fox") + 2;
        var result = TextBlock.Measure(_theme, Tall(width, 2), Sentence);
        Assert.Equal(2, result.Lines);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void A_line_feed_starts_a_new_line()
    {
        var result = TextBlock.Measure(_theme, Tall(400, 10), "one\ntwo\n\nfour");
        Assert.Equal(4, result.Lines);
    }

    [Fact]
    public void A_rectangle_too_short_for_one_line_draws_nothing()
    {
        var result = TextBlock.Measure(_theme, new SKRect(0, 0, 200, 1), Sentence);
        Assert.Equal(0, result.Lines);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void Block_alignment_places_the_block_in_the_rectangle()
    {
        var rect = new SKRect(0, 0, 300, 200);
        var topLeft = TextBlock.Measure(_theme, rect, "hi", horizontal: HorizontalAlign.Left, vertical: VerticalAlign.Top);
        var bottomRight = TextBlock.Measure(_theme, rect, "hi", horizontal: HorizontalAlign.Right, vertical: VerticalAlign.Bottom);
        var middle = TextBlock.Measure(_theme, rect, "hi", horizontal: HorizontalAlign.Center, vertical: VerticalAlign.Middle);
        Assert.Equal(0, topLeft.Bounds.Left, 0.01);
        Assert.Equal(0, topLeft.Bounds.Top, 0.01);
        Assert.Equal(300, bottomRight.Bounds.Right, 0.01);
        Assert.Equal(200, bottomRight.Bounds.Bottom, 0.01);
        Assert.Equal(150, middle.Bounds.MidX, 0.5);
        Assert.Equal(100, middle.Bounds.MidY, 0.5);
    }

    [Fact]
    public void Measuring_a_block_draws_nothing_and_a_repaint_measures_nothing()
    {
        TextLayoutCache.Clear();
        string text = "block repaint " + Guid.NewGuid();
        TextBlock.Measure(_theme, Tall(200, 5), text);
        int before = TextLayoutCache.Layouts;
        using var one = Render(c => TextBlock.Draw(c, _theme, Tall(200, 5), text));
        using var two = Render(c => TextBlock.Draw(c, _theme, Tall(200, 5), text));
        Assert.Equal(before, TextLayoutCache.Layouts);
        Assert.True(Same(one, two));
    }
}

internal static class RectExtensions
{
    public static int RightAsInt(this SKRect rect) => (int)MathF.Ceiling(rect.Right);
}
