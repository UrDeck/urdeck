// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class GaugeTests : IDisposable
{
    private sealed class NoSource : IReadingSource
    {
        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;
    }

    private static readonly SKRect Box = new(0, 0, 300, 300);
    private static readonly ReadingDescriptor Percent = new("p", ReadingKind.Percent, "P", "P");
    private static readonly ReadingDescriptor Hot = new("t", ReadingKind.Temperature, "T", "T") { Min = 0, Max = 120, Warning = 80, Critical = 90 };

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

    public void Dispose() => _theme.Dispose();

    // Scale

    private static GaugeValue Scale(Reading reading, ReadingDescriptor? descriptor, GaugeScaleOptions? options = null) =>
        GaugeScale.Resolve(reading, descriptor, options);

    [Fact]
    public void APercentage_WithoutADeclaredRange_UsesZeroToOneHundred() =>
        Assert.Equal(0.37f, Scale(Reading.Ok(37), Percent).Fraction!.Value, 4);

    [Fact]
    public void TheRange_IsTheCallersFirst_ThenTheCatalogs()
    {
        var catalog = Percent with { Min = 0, Max = 50 };

        Assert.Equal(0.5f, Scale(Reading.Ok(25), catalog).Fraction!.Value, 4);
        Assert.Equal(0.25f, Scale(Reading.Ok(25), catalog, new GaugeScaleOptions { Max = 100 }).Fraction!.Value, 4);
        Assert.Equal(0.5f, Scale(Reading.Ok(60), Percent, new GaugeScaleOptions { Min = 20, Max = 100 }).Fraction!.Value, 4);
    }

    [Fact]
    public void ANumberWithoutARange_HasNoFraction()
    {
        var plain = new ReadingDescriptor("n", ReadingKind.Number, "N", "N") { Unit = "W" };

        Assert.Null(Scale(Reading.Ok(40), plain).Fraction);
        Assert.Null(Scale(Reading.Ok(40), null).Fraction);
        Assert.Null(Scale(Reading.Ok(40), plain, new GaugeScaleOptions { Min = 0 }).Fraction);
        Assert.Null(Scale(Reading.Ok(40), plain, new GaugeScaleOptions { Min = 10, Max = 10 }).Fraction);
        Assert.Equal(0.4f, Scale(Reading.Ok(40), plain, new GaugeScaleOptions { Min = 0, Max = 100 }).Fraction!.Value, 4);
    }

    [Fact]
    public void ATextOrOnOffValue_HasNoFraction()
    {
        Assert.Null(Scale(Reading.Ok("x"), Percent).Fraction);
        Assert.Null(Scale(Reading.Ok(true), Percent).Fraction);
    }

    [Fact]
    public void TheFraction_IsClamped()
    {
        Assert.Equal(1f, Scale(Reading.Ok(120), Percent).Fraction);
        Assert.Equal(0f, Scale(Reading.Ok(-5), Percent).Fraction);
    }

    [Fact]
    public void TheValue_IsRoundedToTheShownDecimalsBeforeScaling()
    {
        var a = Scale(Reading.Ok(37.2), Percent);
        var b = Scale(Reading.Ok(37.4), Percent);
        var tenths = Scale(Reading.Ok(37.46), Percent, new GaugeScaleOptions { Decimals = 1 });

        Assert.Equal(a, b);
        Assert.Equal(0.375f, tenths.Fraction!.Value, 4);
    }

    [Fact]
    public void RoundingComesBeforeTheLevel()
    {
        var catalog = Percent with { Warning = 80 };

        Assert.Equal(GaugeLevel.Warning, Scale(Reading.Ok(79.6), catalog).Level);
        Assert.Equal(GaugeLevel.Normal, Scale(Reading.Ok(79.6), catalog, new GaugeScaleOptions { Decimals = 1 }).Level);
    }

    [Fact]
    public void TheLevels_AreNormalWarningCritical()
    {
        Assert.Equal(GaugeLevel.Normal, Scale(Reading.Ok(79), Hot).Level);
        Assert.Equal(GaugeLevel.Warning, Scale(Reading.Ok(80), Hot).Level);
        Assert.Equal(GaugeLevel.Warning, Scale(Reading.Ok(89), Hot).Level);
        Assert.Equal(GaugeLevel.Critical, Scale(Reading.Ok(90), Hot).Level);
    }

    [Fact]
    public void AReadingWithoutLevels_IsAlwaysNormal() =>
        Assert.Equal(GaugeLevel.Normal, Scale(Reading.Ok(100), Percent).Level);

    [Fact]
    public void TheCallersLevels_ReplaceTheCatalogs()
    {
        Assert.Equal(GaugeLevel.Warning, Scale(Reading.Ok(75), Hot, new GaugeScaleOptions { Warning = 70 }).Level);
        Assert.Equal(GaugeLevel.Normal, Scale(Reading.Ok(85), Hot, new GaugeScaleOptions { Warning = 95, Critical = 99 }).Level);
    }

    [Fact]
    public void AStaleOrPendingReading_KeepsItsValuesResult()
    {
        var ok = Scale(Reading.Ok(85), Hot);

        Assert.Equal(ok, Scale(Reading.Stale(85), Hot));
        Assert.Equal(ok, Scale(Reading.Pending(85), Hot));
    }

    [Fact]
    public void AnUnavailableOrEmptyPendingReading_HasNoFraction()
    {
        Assert.Null(Scale(Reading.Unavailable("x"), Hot).Fraction);
        Assert.Null(Scale(Reading.Pending(), Hot).Fraction);
    }

    // Component

    private static ReadingText Text(Reading reading, ReadingDescriptor? descriptor = null) =>
        ReadingFormatter.Format(reading, descriptor ?? Percent, null, new NoSource());

    private SKBitmap DrawGauge(GaugeStyle style, ReadingText text, GaugeOptions? options = null, Theme? theme = null)
    {
        var bitmap = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bitmap);
        Gauge.Draw(canvas, theme ?? _theme, Box, style, text, options);
        return bitmap;
    }

    private static int Count(SKBitmap bitmap, SKColor color)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y) == color)
                    count++;
        return count;
    }

    private SKColor Track(SKColor fill) => fill.WithAlpha(_theme.AccentDim.Alpha);

    /// <summary>Pixels of about this colour: a translucent colour loses a little in the premultiplied bitmap.</summary>
    private static int CountNear(SKBitmap bitmap, SKColor color)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Alpha == color.Alpha && Math.Abs(p.Red - color.Red) < 6 && Math.Abs(p.Green - color.Green) < 6 && Math.Abs(p.Blue - color.Blue) < 6)
                    count++;
            }
        }

        return count;
    }

    [Fact]
    public void Plain_EqualsTheReadout()
    {
        var text = Text(Reading.Ok(37));
        using var gauge = DrawGauge(GaugeStyle.Plain, text, new GaugeOptions { Label = "CPU", Fraction = 0.9f });
        using var readout = new SKBitmap(300, 300);
        using (var canvas = new SKCanvas(readout))
            Readout.Draw(canvas, _theme, Box, text, new ReadoutOptions { Label = "CPU" });

        Assert.True(gauge.Bytes.AsSpan().SequenceEqual(readout.Bytes));
    }

    [Fact]
    public void TheRingsFill_GrowsWithTheFraction()
    {
        var text = Text(Reading.Ok(37));
        using var empty = DrawGauge(GaugeStyle.Ring, text, new GaugeOptions { Fraction = 0f });
        using var quarter = DrawGauge(GaugeStyle.Ring, text, new GaugeOptions { Fraction = 0.25f });
        using var full = DrawGauge(GaugeStyle.Ring, text, new GaugeOptions { Fraction = 1f });

        int none = Count(empty, _theme.Accent);
        int some = Count(quarter, _theme.Accent);
        int all = Count(full, _theme.Accent);
        Assert.Equal(0, none);
        Assert.True(some > 0);
        // The fill is a quarter of the arc, give or take the round cap at its end.
        Assert.InRange((double)some / all, 0.2, 0.32);
    }

    [Fact]
    public void TheRingsFill_StartsAtTheLowerLeft()
    {
        using var bitmap = DrawGauge(GaugeStyle.Ring, Text(Reading.Ok(37)), new GaugeOptions { Fraction = 0.1f });

        // 10% of 270 degrees from the lower left end: only pixels in the left half, below the middle.
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) == _theme.Accent)
                {
                    Assert.True(x < 150);
                    Assert.True(y > 150);
                }
            }
        }
    }

    [Fact]
    public void TheBar_FillsFromTheLeft_AndTheVerticalBarFromTheBottom()
    {
        var text = Text(Reading.Ok(37));
        using var bar = DrawGauge(GaugeStyle.Bar, text, new GaugeOptions { Fraction = 0.5f });
        using var vertical = DrawGauge(GaugeStyle.VerticalBar, text, new GaugeOptions { Fraction = 0.5f, TargetFraction = 0f });

        int rightmost = 0;
        for (int y = 0; y < bar.Height; y++)
            for (int x = 0; x < bar.Width; x++)
                if (bar.GetPixel(x, y) == _theme.Accent)
                    rightmost = Math.Max(rightmost, x);
        Assert.InRange(rightmost, 140, 170);

        int topmost = int.MaxValue;
        for (int y = 0; y < vertical.Height; y++)
            for (int x = 0; x < vertical.Width; x++)
                if (vertical.GetPixel(x, y) == _theme.Accent)
                    topmost = Math.Min(topmost, y);
        Assert.InRange(topmost, 140, 170);
    }

    [Fact]
    public void TheFillColour_FollowsTheLevel_AndTheTrackIsADimmedFill()
    {
        var text = Text(Reading.Ok(37));
        foreach (var (level, color) in new[] { (GaugeLevel.Normal, _theme.Accent), (GaugeLevel.Warning, _theme.Warning), (GaugeLevel.Critical, _theme.Critical) })
        {
            using var bitmap = DrawGauge(GaugeStyle.Ring, text, new GaugeOptions { Fraction = 0.5f, Level = level });

            Assert.True(Count(bitmap, color) > 0);
            Assert.True(CountNear(bitmap, Track(color)) > 0);
            Assert.True(Count(bitmap, _theme.Text) > 0);
        }
    }

    [Fact]
    public void ANotCurrentReading_IsMuted_FillAndTrack()
    {
        using var bitmap = DrawGauge(GaugeStyle.Ring, Text(Reading.Stale(37)), new GaugeOptions { Fraction = 0.5f });

        Assert.Equal(0, Count(bitmap, _theme.Accent));
        Assert.True(Count(bitmap, _theme.TextMuted) > 0);
        Assert.True(CountNear(bitmap, Track(_theme.TextMuted)) > 0);
    }

    [Fact]
    public void WithoutAFraction_OnlyTheTrackIsDrawn()
    {
        foreach (var style in new[] { GaugeStyle.Ring, GaugeStyle.Bar, GaugeStyle.VerticalBar })
        {
            using var bitmap = DrawGauge(style, Text(Reading.Unavailable("x")), new GaugeOptions());

            Assert.Equal(0, Count(bitmap, _theme.Accent));
            Assert.Equal(0, Count(bitmap, _theme.Text));
            Assert.True(CountNear(bitmap, Track(_theme.TextMuted)) > 0);
        }
    }

    [Fact]
    public void TheReadoutSize_DoesNotChangeWithTheValue()
    {
        float small = Gauge.Measure(_theme, Box, GaugeStyle.Ring, Text(Reading.Ok(8), Percent with { Min = 0, Max = 100 }));
        float full = Gauge.Measure(_theme, Box, GaugeStyle.Ring, Text(Reading.Ok(100), Percent with { Min = 0, Max = 100 }));

        Assert.Equal(small, full);
        Assert.True(small > 0);
    }

    [Fact]
    public void ReadoutScale_ShrinksTheText()
    {
        var text = Text(Reading.Ok(37));
        using var normal = DrawGauge(GaugeStyle.Ring, text, new GaugeOptions { Fraction = 0.5f });
        using var scaled = DrawGauge(GaugeStyle.Ring, text, new GaugeOptions { Fraction = 0.5f, ReadoutScale = 0.5f });

        Assert.True(Count(scaled, _theme.Text) < Count(normal, _theme.Text));
    }

    // Vertical bar contrast

    private Theme Tinted(SKColor warning, SKColor text, SKColor background) => new()
    {
        Warning = warning,
        Text = text,
        Background = background,
        TextMuted = text,
        StrokeRatio = _theme.StrokeRatio,
        Typefaces = new Dictionary<TextRole, SKTypeface>(),
    };

    [Fact]
    public void TextOverALightFill_UsesTheBackgroundColour()
    {
        var background = SKColor.Parse("#101020");
        var theme = Tinted(SKColor.Parse("#ffd000"), SKColors.White, background);
        var options = new GaugeOptions { Fraction = 1f, Level = GaugeLevel.Warning };
        using var bitmap = DrawGauge(GaugeStyle.VerticalBar, Text(Reading.Ok(37)), options, theme);

        Assert.True(Count(bitmap, background) > 0);
        Assert.Equal(0, Count(bitmap, SKColors.White));
    }

    [Fact]
    public void TextOverADarkFill_UsesTheTextColour()
    {
        var theme = Tinted(SKColor.Parse("#202080"), SKColors.White, SKColor.Parse("#101020"));
        var options = new GaugeOptions { Fraction = 1f, Level = GaugeLevel.Warning };
        using var bitmap = DrawGauge(GaugeStyle.VerticalBar, Text(Reading.Ok(37)), options, theme);

        Assert.True(Count(bitmap, SKColors.White) > 0);
    }

    [Fact]
    public void TheTextColour_IsDecidedByTheTargetFraction_NotTheEasedOne()
    {
        var background = SKColor.Parse("#101020");
        var theme = Tinted(SKColor.Parse("#ffd000"), SKColors.White, background);
        // Easing up from nothing toward a full bar: the text is already the colour it will have over the fill.
        var options = new GaugeOptions { Fraction = 0.05f, TargetFraction = 1f, Level = GaugeLevel.Warning };
        using var bitmap = DrawGauge(GaugeStyle.VerticalBar, Text(Reading.Ok(37)), options, theme);

        Assert.True(Count(bitmap, background) > 0);
    }
}
