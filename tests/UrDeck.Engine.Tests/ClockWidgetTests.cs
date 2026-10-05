// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Widgets.Clock;
using UrDeck.Widgets.Clock.Config;
using Xunit;

namespace UrDeck.Engine.Tests;

public class ClockWidgetTests
{
    private static readonly DateTime NoonOnTheMinute = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Local);

    private static readonly LoadedTheme Loaded = new ThemeStore("").Load(ThemeStore.DefaultName);

    private static SKBitmap Paint(ClockWidget clock, DateTime time, int width = 400, int height = 200)
    {
        var size = new System.Drawing.Size(width, height);
        var bitmap = new SKBitmap(size.Width, size.Height);
        using var canvas = new SKCanvas(bitmap);
        using var theme = ThemeResolver.Resolve(Loaded, width / 4f);
        float pad = theme.Padding;
        clock.Render(new WidgetRenderContext(canvas, time, size, theme, new SKRect(pad, pad, width - pad, height - pad), clock.Config, CancellationToken.None));
        return bitmap;
    }

    private static bool HasPixel(SKBitmap bitmap, Func<SKColor, bool> match)
    {
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (match(bitmap.GetPixel(x, y)))
                    return true;
        return false;
    }

    private static int InkRows(SKBitmap bitmap)
    {
        int rows = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    rows++;
                    break;
                }
        return rows;
    }

    [Fact]
    public void NeedsRender_IsTrueBeforeTheFirstPaint()
    {
        Assert.True(new ClockWidget().NeedsRender(NoonOnTheMinute));
    }

    [Fact]
    public void NeedsRender_IsFalseWithinTheSameMinute()
    {
        var clock = new ClockWidget();
        Paint(clock, NoonOnTheMinute.AddSeconds(5));

        Assert.False(clock.NeedsRender(NoonOnTheMinute.AddSeconds(6)));
        Assert.False(clock.NeedsRender(NoonOnTheMinute.AddSeconds(59)));
    }

    [Fact]
    public void NeedsRender_IsTrueWhenTheMinuteChanges()
    {
        var clock = new ClockWidget();
        Paint(clock, NoonOnTheMinute.AddSeconds(59));

        Assert.True(clock.NeedsRender(NoonOnTheMinute.AddSeconds(60)));
    }

    [Fact]
    public void NeedsRender_IsTrueAfterTheConfigChanges()
    {
        var clock = new ClockWidget();
        Paint(clock, NoonOnTheMinute);

        clock.Configure(new ClockConfig { Format = "12h" });

        Assert.True(clock.NeedsRender(NoonOnTheMinute.AddSeconds(1)));
    }

    [Fact]
    public void TextColorOverride_ColoursTheTime()
    {
        var clock = new ClockWidget();
        clock.Configure(new ClockConfig { TextColor = "#ff0000", ShowDate = false });
        using var bitmap = Paint(clock, NoonOnTheMinute);

        Assert.True(HasPixel(bitmap, c => c.Red > 200 && c.Green < 40 && c.Blue < 40 && c.Alpha > 200));
        Assert.False(HasPixel(bitmap, c => c.Alpha > 200 && c.Red > 200 && c.Green > 200 && c.Blue > 200));
    }

    [Fact]
    public void HiddenDate_DrawsLessThanWithDate()
    {
        var withDate = new ClockWidget();
        var noDate = new ClockWidget();
        noDate.Configure(new ClockConfig { ShowDate = false });
        using var a = Paint(withDate, NoonOnTheMinute);
        using var b = Paint(noDate, NoonOnTheMinute);

        // The time-only clock has a single block of ink; with the date there is a second line below it.
        Assert.True(InkRows(a) > InkRows(b));
    }

    [Fact]
    public void FontSizeBelowOne_DrawsSmallerTime()
    {
        var full = new ClockWidget();
        var half = new ClockWidget();
        full.Configure(new ClockConfig { ShowDate = false });
        half.Configure(new ClockConfig { ShowDate = false, FontSize = 0.5 });
        using var a = Paint(full, NoonOnTheMinute);
        using var b = Paint(half, NoonOnTheMinute);

        Assert.InRange(InkRows(b) / (double)InkRows(a), 0.4, 0.6);
    }

    [Fact]
    public void TwelveHour_ShowsPmUnit_AndRendersWithoutError()
    {
        var clock = new ClockWidget();
        clock.Configure(new ClockConfig { Format = "12h", ShowDate = false });
        using var pm = Paint(clock, new DateTime(2026, 1, 15, 19, 30, 0, DateTimeKind.Local));
        var am = new ClockWidget();
        am.Configure(new ClockConfig { Format = "12h", ShowDate = false });
        using var amBitmap = Paint(am, new DateTime(2026, 1, 15, 7, 30, 0, DateTimeKind.Local));

        Assert.NotEqual(pm.Bytes, amBitmap.Bytes);
    }

    private static ClockWidget Flap(string format = "24h", bool showDate = true)
    {
        var clock = new ClockWidget();
        clock.Configure(new ClockConfig { Style = "flap", Format = format, ShowDate = showDate });
        return clock;
    }

    private static readonly DateTime T1041 = new(2026, 1, 15, 10, 41, 20, DateTimeKind.Local);
    private static readonly DateTime T1042 = new(2026, 1, 15, 10, 42, 0, DateTimeKind.Local);

    [Fact]
    public void Flap_FirstPaintIsAtRest()
    {
        var clock = Flap();
        using var bitmap = Paint(clock, T1041);

        Assert.False(clock.IsAnimating);
        Assert.True(HasPixel(bitmap, c => c.Alpha > 0));
    }

    [Fact]
    public void Flap_NextMinuteStartsAFlip_AndEndsAtRest()
    {
        var clock = Flap();
        using var rest = Paint(Flap(), T1042);
        using var before = Paint(clock, T1041);

        using var mid = Paint(clock, T1042.AddMilliseconds(150));
        Assert.True(clock.IsAnimating);
        Assert.NotEqual(rest.Bytes, mid.Bytes);

        using var after = Paint(clock, T1042.AddSeconds(1));
        Assert.False(clock.IsAnimating);
        Assert.Equal(rest.Bytes, after.Bytes);
    }

    [Fact]
    public void Flap_OnlyChangedTilesMove()
    {
        var clock = Flap(showDate: false);
        using var rest = Paint(Flap(showDate: false), T1042);
        using var before = Paint(clock, T1041);
        using var mid = Paint(clock, T1042.AddMilliseconds(150));

        int differing = 0;
        for (int y = 0; y < rest.Height; y++)
            for (int x = 0; x < rest.Width; x++)
                if (rest.GetPixel(x, y) != mid.GetPixel(x, y))
                {
                    differing++;
                    Assert.True(x > rest.Width / 2, $"pixel {x},{y} changed outside the last tile");
                }
        Assert.True(differing > 0);
    }

    [Fact]
    public void Flap_RenderTimeBeforeTheFlipStart_DrawsRest()
    {
        var clock = Flap();
        using var rest = Paint(Flap(), T1042);
        using var a = Paint(clock, T1041);
        using var b = Paint(clock, T1042);
        Assert.True(clock.IsAnimating);

        using var c = Paint(clock, T1042.AddSeconds(-30));

        Assert.False(clock.IsAnimating);
        using var restBefore = Paint(Flap(), T1042.AddSeconds(-30));
        Assert.Equal(restBefore.Bytes, c.Bytes);
    }

    [Fact]
    public void Flap_ConfigChangeDrawsRestAgain()
    {
        var clock = Flap();
        using var a = Paint(clock, T1041);
        using var b = Paint(clock, T1042);
        Assert.True(clock.IsAnimating);

        clock.Configure(new ClockConfig { Style = "flap" });
        using var c = Paint(clock, T1042.AddMilliseconds(100));

        Assert.False(clock.IsAnimating);
    }

    [Fact]
    public void Flap_TwelveHourWithSingleDigitHour_PaintsABlankFirstTile()
    {
        var seven = new DateTime(2026, 1, 15, 19, 30, 0, DateTimeKind.Local);
        var tenThirty = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Local);
        using var a = Paint(Flap("12h"), seven);
        using var b = Paint(Flap("12h"), tenThirty);

        Assert.NotEqual(a.Bytes, b.Bytes);
    }

    [Fact]
    public void Flap_AllHourTilesFlipAtTheTurnOfTheHour()
    {
        var clock = Flap(showDate: false);
        using var a = Paint(clock, new DateTime(2026, 1, 15, 9, 59, 30, DateTimeKind.Local));
        using var mid = Paint(clock, new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local).AddMilliseconds(150));
        using var rest = Paint(Flap(showDate: false), new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local));

        Assert.True(clock.IsAnimating);
        bool leftChanged = false;
        for (int y = 0; y < rest.Height && !leftChanged; y++)
            for (int x = 0; x < rest.Width / 2 && !leftChanged; x++)
                leftChanged = rest.GetPixel(x, y) != mid.GetPixel(x, y);
        Assert.True(leftChanged);
    }

    [Fact]
    public void UnknownStyle_FallsBackToSimple()
    {
        var neon = new ClockWidget();
        neon.Configure(new ClockConfig { Style = "neon" });
        var simple = new ClockWidget();
        using var a = Paint(neon, T1041);
        using var b = Paint(simple, T1041);
        using var c = Paint(neon, T1042);

        Assert.Equal(b.Bytes, a.Bytes);
        Assert.False(neon.IsAnimating);
    }

    [Fact]
    public void SimpleStyle_NeverAnimates()
    {
        var clock = new ClockWidget();
        using var a = Paint(clock, T1041);
        using var b = Paint(clock, T1042);

        Assert.False(clock.IsAnimating);
    }

    [Theory]
    [InlineData(400, 200, "24h")]
    [InlineData(400, 100, "24h")]
    [InlineData(200, 100, "12h")]
    [InlineData(100, 100, "12h")]
    public void Flap_DrawsInsideTheContentRectAtEachSize(int width, int height, string format)
    {
        var clock = Flap(format);
        using var a = Paint(clock, T1041, width, height);
        using var mid = Paint(clock, T1042.AddMilliseconds(150), width, height);

        using var theme = ThemeResolver.Resolve(Loaded, width / 4f);
        float pad = theme.Padding;
        foreach (var bitmap in new[] { a, mid })
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y).Alpha > 0)
                        Assert.True(x >= pad - 1 && x <= width - pad + 1 && y >= pad - 1 && y <= height - pad + 1,
                            $"ink at {x},{y} is outside the content rectangle");
    }
}
