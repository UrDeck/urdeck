// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The gauge styles and the theme's gauge values (theme-gauges), drawn on a 300 by 300 box.</summary>
public sealed class GaugeStylesTests : IDisposable
{
    private sealed class NoSource : IReadingSource
    {
        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;
    }

    private static readonly SKRect Box = new(0, 0, 300, 300);
    private static readonly ReadingDescriptor Percent = new("p", ReadingKind.Percent, "P", "P");
    private static readonly SKColor Magenta = SKColor.Parse("#ff00ff");
    private static readonly SKColor Red = SKColor.Parse("#ff0000");

    private readonly Theme _base = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly List<GaugeTheme> _gauges = [];

    public void Dispose()
    {
        _base.Dispose();
        foreach (var gauge in _gauges)
            gauge.Dispose();
    }

    /// <summary>The default theme's colours and typefaces with the given gauge values; its typefaces stay the default theme's to dispose.</summary>
    private Theme Themed(GaugeTheme gauge, GaugeStyle style = GaugeStyle.Ring, IReadOnlyList<SKColor>? ramp = null)
    {
        _gauges.Add(gauge);
        return new Theme
        {
            Background = _base.Background,
            CardFill = _base.CardFill,
            Text = _base.Text,
            TextMuted = _base.TextMuted,
            Accent = _base.Accent,
            AccentDim = _base.AccentDim,
            Good = _base.Good,
            Warning = _base.Warning,
            Critical = _base.Critical,
            LabelSize = _base.LabelSize,
            UnitRatio = _base.UnitRatio,
            StrokeRatio = _base.StrokeRatio,
            StrokeCap = _base.StrokeCap,
            GaugeStyle = style,
            Typefaces = _base.Typefaces,
            Gauge = gauge,
            Ramp = ramp ?? [_base.Good, _base.Warning, _base.Critical],
        };
    }

    private static ReadingText Text(Reading reading) => ReadingFormatter.Format(reading, Percent, null, new NoSource());

    private static ReadingText Ok => Text(Reading.Ok(37));

    private static SKBitmap Draw(Theme theme, GaugeStyle style, ReadingText text, GaugeOptions? options = null, SKRect? rect = null)
    {
        var bitmap = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bitmap);
        Gauge.Draw(canvas, theme, rect ?? Box, style, text, options);
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

    private static bool Drawn(SKBitmap bitmap, int left, int top, int right, int bottom)
    {
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (bitmap.GetPixel(x, y).Alpha > 40)
                    return true;
        return false;
    }

    /// <summary>The runs of <paramref name="color"/> along row <paramref name="y"/>, as (start, length).</summary>
    private static List<(int Start, int Length)> Runs(SKBitmap bitmap, int y, SKColor color)
    {
        var runs = new List<(int, int)>();
        int start = -1;
        for (int x = 0; x <= bitmap.Width; x++)
        {
            bool on = x < bitmap.Width && bitmap.GetPixel(x, y) == color;
            if (on && start < 0)
                start = x;
            else if (!on && start >= 0)
            {
                runs.Add((start, x - start));
                start = -1;
            }
        }

        return runs;
    }

    /// <summary>How many separate stretches of <paramref name="color"/> a circle of this radius crosses over the angles given.</summary>
    private static int CirclePasses(SKBitmap bitmap, float radius, float fromDegrees, float toDegrees, SKColor color)
    {
        int passes = 0;
        bool inside = false;
        for (float angle = fromDegrees; angle <= toDegrees; angle += 0.25f)
        {
            double rad = angle * Math.PI / 180;
            int x = (int)Math.Round(150 + radius * Math.Cos(rad));
            int y = (int)Math.Round(150 + radius * Math.Sin(rad));
            bool on = x >= 0 && y >= 0 && x < bitmap.Width && y < bitmap.Height && bitmap.GetPixel(x, y) == color;
            if (on && !inside)
                passes++;
            inside = on;
        }

        return passes;
    }

    /// <summary>How many separate stretches of drawn pixels a circle of this radius crosses over the angles given.</summary>
    private static int AlphaPasses(SKBitmap bitmap, float radius, float fromDegrees, float toDegrees)
    {
        int passes = 0;
        bool inside = false;
        for (float angle = fromDegrees; angle <= toDegrees; angle += 0.25f)
        {
            double rad = angle * Math.PI / 180;
            bool on = bitmap.GetPixel((int)Math.Round(150 + radius * Math.Cos(rad)), (int)Math.Round(150 + radius * Math.Sin(rad))).Alpha > 40;
            if (on && !inside)
                passes++;
            inside = on;
        }

        return passes;
    }

    private static bool Same(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    // The v1 pixels

    public static TheoryData<string, float, string, bool> V1Cases()
    {
        var data = new TheoryData<string, float, string, bool>();
        foreach (string theme in new[] { "default-dark", "default-light", "glass" })
            foreach (float cell in new[] { 275f, 180f })
                foreach (string style in new[] { "Ring", "Bar", "VerticalBar" })
                    foreach (bool fraction in new[] { false, true })
                        data.Add(theme, cell, style, fraction);
        return data;
    }

    [Theory]
    [MemberData(nameof(V1Cases))]
    public void ATheme_WithNoGaugeValues_DrawsTheV1Pixels(string themeName, float cell, string styleName, bool withFraction)
    {
        using var theme = ThemeResolver.Resolve(new ThemeStore("").Load(themeName), cell);
        var style = Enum.Parse<GaugeStyle>(styleName);
        var options = new GaugeOptions { Label = "CPU", Fraction = withFraction ? 0.4f : null, Level = GaugeLevel.Warning };
        var rect = new SKRect(10, 20, 290, 220);
        var text = Ok;

        using var now = Draw(theme, style, text, options, rect);
        using var before = new SKBitmap(300, 300);
        using (var canvas = new SKCanvas(before))
            V1Gauge.Draw(canvas, theme, rect, style, text, options);

        Assert.True(theme.Gauge.IsEmpty);
        Assert.True(Same(now, before));
        Assert.Equal(V1Gauge.Measure(theme, rect, style, text, options), Gauge.Measure(theme, rect, style, text, options));
    }

    [Fact]
    public void ABar_AndAVerticalBar_IgnoreTheValuesThatDoNotApplyToThem()
    {
        using var gauge = new GaugeTheme { Ticks = new GaugeTicks(11, 5, 12, 2, Red, false), Marker = new GaugeMarker(GaugeMarkerShape.Needle, 20, Magenta, false) };
        var theme = Themed(gauge);
        var options = new GaugeOptions { Fraction = 0.5f, Label = "CPU" };

        foreach (var style in new[] { GaugeStyle.Bar, GaugeStyle.VerticalBar })
        {
            using var now = Draw(theme, style, Ok, options);
            using var before = new SKBitmap(300, 300);
            using (var canvas = new SKCanvas(before))
                V1Gauge.Draw(canvas, theme, Box, style, Ok, options);

            Assert.True(Same(now, before), style.ToString());
        }
    }

    // Ring, sweep, track

    [Fact]
    public void ARing_WithAFullSweepFromTheTop_FillsFromTwelveToThree_AndTheTrackHasNoGap()
    {
        using var gauge = new GaugeTheme { Sweep = new GaugeSweep(-90, 360) };
        var theme = Themed(gauge);
        using var bitmap = Draw(theme, GaugeStyle.Ring, Ok, new GaugeOptions { Fraction = 0.25f });

        // The fill covers the upper right quarter only.
        for (int y = 0; y < 300; y++)
        {
            for (int x = 0; x < 300; x++)
            {
                if (bitmap.GetPixel(x, y) == theme.Accent)
                {
                    // The round caps reach 12 px past each end of the quarter.
                    Assert.True(x >= 136, $"fill at {x},{y}");
                    Assert.True(y <= 164, $"fill at {x},{y}");
                }
            }
        }

        Assert.True(Count(bitmap, theme.Accent) > 0);
        // Where the v1 ring is open, the full circle has track: straight down from the centre.
        Assert.True(bitmap.GetPixel(150, 288).Alpha > 0);
    }

    [Fact]
    public void TheTrackColour_ReplacesTheDimmedFill_AndAStaleReadingKeepsTheMutedOne()
    {
        using var gauge = new GaugeTheme { Track = new GaugeTrack(Magenta, 1f) };
        var theme = Themed(gauge);

        using var current = Draw(theme, GaugeStyle.Ring, Ok, new GaugeOptions { Fraction = 0.1f });
        using var stale = Draw(theme, GaugeStyle.Ring, Text(Reading.Stale(37)), new GaugeOptions { Fraction = 0.1f });

        Assert.True(Count(current, Magenta) > 0);
        Assert.Equal(0, Count(stale, Magenta));
    }

    // Segments

    [Fact]
    public void ABarWithSegments_FillsWholeBlocksAndTheLastOneToHalf()
    {
        using var gauge = new GaugeTheme { Segments = new GaugeSegments(30, 10) };
        var theme = Themed(gauge);
        // 300 px of track, blocks every 40 px: the fourth starts at 120, so 0.45 ends in the middle of it.
        using var bitmap = Draw(theme, GaugeStyle.Bar, Ok, new GaugeOptions { Fraction = 0.45f });

        var runs = Runs(bitmap, 12, theme.Accent);
        Assert.Equal(4, runs.Count);
        Assert.All(runs.Take(3), run => Assert.InRange(run.Length, 29, 31));
        Assert.InRange(runs[3].Length, 14, 16);
        Assert.Equal(120, runs[3].Start);
    }

    [Fact]
    public void ARingWithSegments_IsCutIntoBlocks()
    {
        using var gauge = new GaugeTheme { Segments = new GaugeSegments(30, 20) };
        var theme = Themed(gauge);
        using var cut = Draw(theme, GaugeStyle.Ring, Ok, new GaugeOptions { Fraction = 1f });
        using var whole = Draw(Themed(new GaugeTheme()), GaugeStyle.Ring, Ok, new GaugeOptions { Fraction = 1f });

        // The ring's radius is 138 px, so its 270 degrees are 650 px: blocks of 30 px with gaps of 20 px make 13.
        int passes = CirclePasses(cut, 138, 135, 405, theme.Accent);
        Assert.InRange(passes, 12, 14);
        Assert.Equal(1, CirclePasses(whole, 138, 136, 404, theme.Accent));
    }

    // Colours

    [Fact]
    public void ColorByRamp_ColoursTheFillAlongTheRamp_WhateverTheLevel()
    {
        var cyan = SKColor.Parse("#00ffff");
        var violet = SKColor.Parse("#8000ff");
        using var gauge = new GaugeTheme { ColorByRamp = true };
        var theme = Themed(gauge, ramp: [cyan, violet, Magenta]);
        using var bitmap = Draw(theme, GaugeStyle.Ring, Ok, new GaugeOptions { Fraction = 0.5f, Level = GaugeLevel.Critical });

        Assert.True(Count(bitmap, violet) > 0);
        Assert.Equal(0, Count(bitmap, theme.Critical));
    }

    [Fact]
    public void AGradient_IsLaidOverTheWholeTrack_SoTheFillEndsHalfwayThroughIt()
    {
        var blue = SKColor.Parse("#0000ff");
        using var gauge = new GaugeTheme { Gradient = new GaugeGradient([Red, blue], 0f, false) };
        var theme = Themed(gauge);
        using var bitmap = Draw(theme, GaugeStyle.Bar, Ok, new GaugeOptions { Fraction = 0.5f });

        var near = bitmap.GetPixel(140, 12);
        Assert.InRange(near.Red, 100, 160);
        Assert.InRange(near.Blue, 100, 160);
        var start = bitmap.GetPixel(20, 12);
        Assert.True(start.Red > 230 && start.Blue < 40);
    }

    [Fact]
    public void AStaleReading_IgnoresTheGradientAndTheRamp()
    {
        using var gauge = new GaugeTheme { ColorByRamp = true, Gradient = new GaugeGradient([], 0f, true) };
        var theme = Themed(gauge, ramp: [SKColor.Parse("#00ffff"), Magenta]);
        using var bitmap = Draw(theme, GaugeStyle.Ring, Text(Reading.Stale(37)), new GaugeOptions { Fraction = 0.5f });

        Assert.Equal(0, Count(bitmap, Magenta));
        Assert.True(Count(bitmap, theme.TextMuted) > 0);
    }

    [Fact]
    public void ValueTint_DrawsTheValueInTheFillColour()
    {
        using var tinted = new GaugeTheme { ValueTint = true };
        var theme = Themed(tinted);
        var options = new GaugeOptions { Level = GaugeLevel.Warning };
        using var withTint = Draw(theme, GaugeStyle.Ring, Ok, options);
        using var without = Draw(Themed(new GaugeTheme { Track = new GaugeTrack(null, 1f) }), GaugeStyle.Ring, Ok, options);

        Assert.True(Count(withTint, theme.Warning) > 0);
        Assert.Equal(0, Count(withTint, theme.Text));
        Assert.True(Count(without, theme.Text) > 0);
    }

    [Fact]
    public void AGlow_SpillsPastTheFill()
    {
        using var gauge = new GaugeTheme { Glow = new GaugeGlow(20, null, 0.8f) };
        var theme = Themed(gauge);
        var rect = new SKRect(40, 40, 260, 260);
        var options = new GaugeOptions { Fraction = 0.5f };
        using var lit = Draw(theme, GaugeStyle.Ring, Ok, options, rect);
        using var plain = Draw(Themed(new GaugeTheme { Track = new GaugeTrack(null, 1f) }), GaugeStyle.Ring, Ok, options, rect);

        // The fill reaches the top of the ring at its half; just above the ring only the glow can colour the pixel.
        Assert.True(lit.GetPixel(150, 32).Alpha > 0);
        Assert.Equal(0, plain.GetPixel(150, 32).Alpha);
    }

    // Ticks, scale, redline, marker

    [Fact]
    public void Ticks_AreCountedOverTheSweep_AndEveryMajorEveryOneIsLonger()
    {
        using var gauge = new GaugeTheme { Sweep = new GaugeSweep(180, 180), Ticks = new GaugeTicks(19, 3, 20, 3, Red, false) };
        var theme = Themed(gauge, GaugeStyle.Marker);
        using var bitmap = Draw(theme, GaugeStyle.Marker, Ok, new GaugeOptions());

        // Ticks start 12 px outside a track of radius 96: minor ones run to 140, major ones (1.5 times as long) to 150.
        Assert.Equal(19, CirclePasses(bitmap, 125, 175, 365, Red));
        Assert.Equal(7, CirclePasses(bitmap, 146, 175, 365, Red));
    }

    [Fact]
    public void Ticks_MakeRoomForThemselves_SoTheReadoutShrinks()
    {
        using var ticked = new GaugeTheme { Ticks = new GaugeTicks(28, 4, 30, 3, Red, true) };
        var plain = Themed(new GaugeTheme());
        var theme = Themed(ticked, GaugeStyle.Tach);

        Assert.True(Gauge.Measure(theme, Box, GaugeStyle.Tach, Ok) < Gauge.Measure(plain, Box, GaugeStyle.Ring, Ok));
    }

    [Fact]
    public void ATach_WithNoTicksOfTheThemes_StillHasARingOfTicksAndARedlineZone()
    {
        var plain = Themed(new GaugeTheme(), GaugeStyle.Tach);
        using var tach = Draw(plain, GaugeStyle.Tach, Ok, new GaugeOptions { Fraction = 0.3f });
        using var ring = Draw(plain, GaugeStyle.Ring, Ok, new GaugeOptions { Fraction = 0.3f });

        Assert.False(Same(tach, ring));
        // The ticks lie outside the track (its outer edge is 115.5 px from the centre): 28 marks cross a circle of 128 px; the ring's track is one stretch there.
        Assert.InRange(AlphaPasses(tach, 128, 130, 410), 24, 30);
        Assert.InRange(AlphaPasses(ring, 128, 136, 404), 1, 2);
    }

    [Fact]
    public void ATach_IsNotReplacedByARing_OnlyTheTachDrawsTheNeedle()
    {
        using var gauge = new GaugeTheme { Marker = new GaugeMarker(GaugeMarkerShape.Needle, 20, Magenta, false) };
        var theme = Themed(gauge, GaugeStyle.Tach);
        var options = new GaugeOptions { Fraction = 0.5f };
        using var tach = Draw(theme, GaugeStyle.Tach, Ok, options);
        using var ring = Draw(theme, GaugeStyle.Ring, Ok, options);

        // Half way round is straight up: the needle runs from the centre to the top of the track.
        Assert.Equal(Magenta, tach.GetPixel(150, 60));
        Assert.Equal(0, Count(ring, Magenta));
    }

    [Fact]
    public void ARedline_LightsOnlyAtOrAboveItsStart_AndItsGlowWithIt()
    {
        using var gauge = new GaugeTheme { Redline = new GaugeRedline(0.8f, Red, new GaugeGlow(20, null, 0.9f), true) };
        var theme = Themed(gauge, GaugeStyle.Tach);
        using var below = Draw(theme, GaugeStyle.Tach, Ok, new GaugeOptions { Fraction = 0.6f });
        using var above = Draw(theme, GaugeStyle.Tach, Ok, new GaugeOptions { Fraction = 0.9f });

        // A tach keeps room for its ticks, so its track has a radius of 103.5 px. A point of the zone 18 degrees round, and one just outside it.
        var zone = (X: 248, Y: 182);
        var beside = (X: 266, Y: 187);
        var dim = below.GetPixel(zone.X, zone.Y);
        var lit = above.GetPixel(zone.X, zone.Y);
        // The unlit zone is a third of the colour's strength, over the dimmed track.
        Assert.InRange(dim.Alpha, 120, 200);
        Assert.Equal(255, lit.Alpha);
        Assert.Equal(0, below.GetPixel(beside.X, beside.Y).Alpha);
        Assert.True(above.GetPixel(beside.X, beside.Y).Alpha > 0);
    }

    [Fact]
    public void AMarkerGauge_PutsTheMarkerOnTheArcAtTheValue_AndDoesNotFillIt()
    {
        using var gauge = new GaugeTheme { Marker = new GaugeMarker(GaugeMarkerShape.Diamond, 20, Magenta, false) };
        var theme = Themed(gauge, GaugeStyle.Marker);
        using var bitmap = Draw(theme, GaugeStyle.Marker, Ok, new GaugeOptions { Fraction = 0.5f });

        // 135 degrees along a sweep of 270 from 135 is straight up, at the track's radius of 138.
        Assert.Equal(Magenta, bitmap.GetPixel(150, 12));
        Assert.Equal(0, Count(bitmap, theme.Accent));
    }

    [Fact]
    public void WithoutAFraction_TheMarkerTheNeedleAndTheRedlineLightAreNotDrawn_AndTheTrackIs()
    {
        var redline = new GaugeRedline(0.5f, Red, new GaugeGlow(10, null, 0.9f), true);
        using var gauge = new GaugeTheme { Marker = new GaugeMarker(GaugeMarkerShape.Needle, 20, Magenta, true), Redline = redline };
        var theme = Themed(gauge);

        foreach (var style in new[] { GaugeStyle.Marker, GaugeStyle.Tach })
        {
            using var bitmap = Draw(theme, style, Text(Reading.Unavailable("x")), new GaugeOptions());

            Assert.Equal(0, Count(bitmap, Magenta));
            Assert.Equal(0, Count(bitmap, Red));
            Assert.Equal(0, Count(bitmap, theme.Accent));
            Assert.True(Drawn(bitmap, 0, 0, 300, 300));
        }
    }

    // Layout: stacked bar, meter, thick bar

    [Fact]
    public void AStackedBar_PutsTheReadoutAboveAndTheTrackAtTheBottom()
    {
        using var gauge = new GaugeTheme { Layout = GaugeLayout.Stacked };
        var theme = Themed(gauge, GaugeStyle.Bar);
        using var bitmap = Draw(theme, GaugeStyle.Bar, Ok, new GaugeOptions { Fraction = 1f });

        Assert.NotEmpty(Runs(bitmap, 288, theme.Accent));
        Assert.Empty(Runs(bitmap, 12, theme.Accent));
        // The value is left-aligned above the track.
        int leftmost = 300;
        for (int y = 0; y < 240; y++)
            for (int x = 0; x < 300; x++)
                if (bitmap.GetPixel(x, y) == theme.Text)
                    leftmost = Math.Min(leftmost, x);
        Assert.InRange(leftmost, 0, 30);
    }

    [Fact]
    public void AMeter_PrintsItsScaleUnderTheTrack_AtBothEndsAndTheMiddle()
    {
        using var gauge = new GaugeTheme { Scale = new GaugeScaleLabels(["0", "50", "100"], 0f) };
        var theme = Themed(gauge, GaugeStyle.Meter);
        using var bitmap = Draw(theme, GaugeStyle.Meter, Ok, new GaugeOptions { Fraction = 0.5f });

        // The labels are the bottom rows, the track just above them, the readout above that.
        Assert.True(Drawn(bitmap, 0, 270, 40, 300));
        Assert.True(Drawn(bitmap, 130, 270, 170, 300));
        Assert.True(Drawn(bitmap, 260, 270, 300, 300));
        Assert.False(Drawn(bitmap, 60, 270, 120, 300));
        Assert.True(Count(bitmap, theme.Accent) > 0);
        Assert.True(Count(bitmap, theme.Text) > 0);
    }

    [Fact]
    public void AThickBar_IsFilledToTheFractionAndOutlined()
    {
        using var gauge = new GaugeTheme { Outline = new GaugeOutline(8, SKColor.Parse("#111111")) };
        var theme = Themed(gauge, GaugeStyle.ThickBar);
        using var bitmap = Draw(theme, GaugeStyle.ThickBar, Ok, new GaugeOptions { Fraction = 0.5f });

        // The track is 72 px high and ends 8 px above the bottom; its middle is at y = 256.
        Assert.Equal(theme.Accent, bitmap.GetPixel(80, 256));
        Assert.NotEqual(theme.Accent, bitmap.GetPixel(240, 256));
        Assert.Equal(SKColor.Parse("#111111"), bitmap.GetPixel(4, 256));
        Assert.Equal(SKColor.Parse("#111111"), bitmap.GetPixel(296, 256));
    }

    // Label position

    [Fact]
    public void ALabelBelow_LeavesOnlyTheValueInsideTheRing()
    {
        using var below = new GaugeTheme { LabelPosition = GaugeLabelPosition.Below };
        var options = new GaugeOptions { Label = "CPU", Fraction = 0.5f };
        using var outside = Draw(Themed(below), GaugeStyle.Ring, Ok, options);
        using var inside = Draw(Themed(new GaugeTheme { LabelPosition = GaugeLabelPosition.Inside }), GaugeStyle.Ring, Ok, options);

        Assert.True(Drawn(outside, 100, 282, 200, 300));
        Assert.False(Drawn(inside, 100, 282, 200, 300));
    }

    // The readout's size

    [Theory]
    [InlineData(GaugeStyle.Meter)]
    [InlineData(GaugeStyle.Tach)]
    [InlineData(GaugeStyle.Marker)]
    [InlineData(GaugeStyle.ThickBar)]
    public void TheReadoutSize_OfEachNewStyle_DoesNotChangeWithTheValue(GaugeStyle style)
    {
        var theme = Themed(new GaugeTheme { Scale = new GaugeScaleLabels(["0", "100"], 0f), Ticks = new GaugeTicks(11, 5, 12, 2, null, false) }, style);
        var ranged = Percent with { Min = 0, Max = 100 };
        float small = Gauge.Measure(theme, Box, style, ReadingFormatter.Format(Reading.Ok(8), ranged, null, new NoSource()));
        float full = Gauge.Measure(theme, Box, style, ReadingFormatter.Format(Reading.Ok(100), ranged, null, new NoSource()));

        Assert.Equal(small, full);
        Assert.True(small > 0);
    }

    [Fact]
    public void TheReadoutSize_ThatMeasureGives_IsTheOneDrawUses()
    {
        using var gauge = new GaugeTheme { Layout = GaugeLayout.Stacked };
        var theme = Themed(gauge, GaugeStyle.Bar);
        var text = Ok;
        float measured = Gauge.Measure(theme, Box, GaugeStyle.Bar, text);
        float half = Gauge.Measure(theme, Box, GaugeStyle.Bar, text, new GaugeOptions { ReadoutScale = 0.5f });

        Assert.True(measured > 0);
        Assert.Equal(measured, half);
    }

    // Cost

    [Fact]
    public void TheShaders_AreBuiltOncePerSize_AndDisposedWithTheTheme()
    {
        var gauge = new GaugeTheme { Gradient = new GaugeGradient([Red, Magenta], 0f, false), Glow = new GaugeGlow(10, null, 0.5f) };
        var theme = Themed(gauge);

        for (int i = 0; i < 5; i++)
        {
            using var ring = Draw(theme, GaugeStyle.Ring, Ok, new GaugeOptions { Fraction = 0.1f * (i + 1) });
            using var bar = Draw(theme, GaugeStyle.Bar, Ok, new GaugeOptions { Fraction = 0.1f * (i + 1) });
        }

        // One sweep shader, one linear shader and one blur filter, however often they are drawn.
        Assert.Equal(3, gauge.Cache.Builds);
        gauge.Dispose();
        using var again = Draw(theme, GaugeStyle.Ring, Ok, new GaugeOptions { Fraction = 0.5f });
        Assert.Equal(5, gauge.Cache.Builds);
    }
}
