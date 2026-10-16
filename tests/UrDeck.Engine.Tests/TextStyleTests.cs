// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The theme's text style and how <see cref="Readout"/> and <see cref="TextLine"/> draw it (theme-typography).</summary>
public sealed class TextStyleTests : IDisposable
{
    private static readonly SKRect Slot = new(0, 0, 400, 200);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-textstyle-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly List<Theme> _themes = [];

    public TextStyleTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        foreach (var theme in _themes)
            theme.Dispose();
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    /// <summary>default-dark with <paramref name="typography"/> (the inside of a <c>typography</c> object) on top.</summary>
    private Theme ThemeWith(string typography, float cellPx = 275)
    {
        string name = "t" + _themes.Count;
        string dir = Path.Combine(_root, "themes", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), $$"""{ "typography": { {{typography}} } }""");
        var theme = ThemeResolver.Resolve(new ThemeStore(Path.Combine(_root, "themes")).Load(name), cellPx);
        _themes.Add(theme);
        return theme;
    }

    private Theme Plain(float cellPx = 275) => ThemeWith("", cellPx);

    private static SKBitmap Render(Action<SKCanvas> draw, int width = 400, int height = 200)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        draw(canvas);
        return bitmap;
    }

    private static void AssertSamePixels(SKBitmap expected, SKBitmap actual) =>
        Assert.True(expected.Bytes.AsSpan().SequenceEqual(actual.Bytes), "the pictures differ");

    private static bool SamePixels(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    /// <summary>The bounding box of the pixels that are not the black background, within <paramref name="area"/>.</summary>
    private static SKRectI Ink(SKBitmap bitmap, SKRectI area)
    {
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (bitmap.GetPixel(x, y) != SKColors.Black)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x + 1);
                    bottom = Math.Max(bottom, y + 1);
                }
        return new SKRectI(left, top, right, bottom);
    }

    // ---------------------------------------------------------------- the style on the Theme

    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    public void AThemeWithNoTextStyle_GivesTheNothingSetStyle(string name)
    {
        using var theme = ThemeResolver.Resolve(ThemeStore.LoadBuiltIn(name)!, 275);
        var style = theme.TextStyle;

        Assert.Equal(TextCase.None, style.LabelCase);
        Assert.Equal(TextCase.None, style.TitleCase);
        Assert.Equal(0f, style.ValueTracking);
        Assert.Equal(0f, style.LabelTracking);
        Assert.Equal(0f, style.TitleTracking);
        Assert.Equal(0f, style.ValueSlantDegrees);
        Assert.Equal(1f, style.ValueScale);
        Assert.Null(style.ValueFill);
        Assert.Null(style.ValueGlow);
    }

    [Fact]
    public void TheStyle_IsResolvedInPixels_AndIsTheSameForThePageAndTheDockExceptSizes()
    {
        const string typography = """
            "case": { "label": "upper", "title": "lower" }, "tracking": { "value": 0.05, "label": 0.2, "title": 0.1 },
            "slant": 8, "valueScale": 1.2, "valueFill": { "colors": ["#ff00ff", "#00ffff"], "angle": 90 },
            "valueGlow": { "blur": 0.1, "color": "#ff7a1a" }
            """;
        var page = ThemeWith(typography, 275).TextStyle;
        var dock = ThemeWith(typography, 120).TextStyle;

        Assert.Equal(27.5f, page.ValueGlow!.BlurPx, 3);
        Assert.Equal(12f, dock.ValueGlow!.BlurPx, 3);
        Assert.Equal(0.6f, page.ValueGlow.Alpha); // the schema's default strength
        Assert.Equal(SKColor.Parse("#ff7a1a"), page.ValueGlow.Color);
        foreach (var style in new[] { page, dock })
        {
            Assert.Equal(TextCase.Upper, style.LabelCase);
            Assert.Equal(TextCase.Lower, style.TitleCase);
            Assert.Equal(0.05f, style.ValueTracking, 4);
            Assert.Equal(0.2f, style.LabelTracking, 4);
            Assert.Equal(0.1f, style.TitleTracking, 4);
            Assert.Equal(8f, style.ValueSlantDegrees);
            Assert.Equal(1.2f, style.ValueScale, 4);
            Assert.Equal([SKColor.Parse("#ff00ff"), SKColor.Parse("#00ffff")], style.ValueFill!.Colors);
            Assert.Equal(90f, style.ValueFill.AngleDegrees);
        }
    }

    // ---------------------------------------------------------------- no style: the v1 bytes

    public static TheoryData<string> V1Themes() => ["default-dark", "default-light", "glass"];

    [Theory]
    [MemberData(nameof(V1Themes))]
    public void WithNoStyle_TheComponentsDrawTheBytesTheyDrewBefore(string name)
    {
        foreach (float cell in new[] { 275f, 120f })
        {
            using var theme = ThemeResolver.Resolve(ThemeStore.LoadBuiltIn(name)!, cell);
            var readouts = new (string Value, ReadoutOptions Options)[]
            {
                ("41", new ReadoutOptions { Unit = "%", Label = "Cpu load" }),
                ("12:34", new ReadoutOptions { WidestValue = "88:88", Scale = 0.6f, Label = "Time", Vertical = VerticalAlign.Top }),
                ("-3.5", new ReadoutOptions { Unit = "°C", UnitPlacement = UnitPlacement.Baseline, ValueColor = SKColors.Orange, Horizontal = HorizontalAlign.Left }),
                ("7", new ReadoutOptions { Label = "A very long label that has to shrink to fit", LabelColor = SKColors.Red, Horizontal = HorizontalAlign.Right }),
            };
            foreach (var (value, options) in readouts)
            {
                using var expected = Render(c => V1Text.Readout(c, theme, Slot, value, options));
                using var actual = Render(c => Readout.Draw(c, theme, Slot, value, options));
                AssertSamePixels(expected, actual);
            }

            var lines = new (string Text, TextStep Step, TextEmphasis Emphasis, HorizontalAlign H, VerticalAlign V)[]
            {
                ("Label line Cpu", TextStep.Label, TextEmphasis.Primary, HorizontalAlign.Center, VerticalAlign.Middle),
                ("Body line Sunny", TextStep.Body, TextEmphasis.Muted, HorizontalAlign.Left, VerticalAlign.Top),
                ("Title line Weather", TextStep.Title, TextEmphasis.Primary, HorizontalAlign.Right, VerticalAlign.Bottom),
                ("A title far too wide for the rectangle it is given", TextStep.Title, TextEmphasis.Primary, HorizontalAlign.Center, VerticalAlign.Middle),
            };
            foreach (var (text, step, emphasis, h, v) in lines)
            {
                using var expected = Render(c => V1Text.TextLine(c, theme, Slot, text, step, emphasis, h, v));
                using var actual = Render(c => TextLine.Draw(c, theme, Slot, text, step, emphasis, h, v));
                AssertSamePixels(expected, actual);
            }
        }
    }

    // ---------------------------------------------------------------- case

    [Fact]
    public void UpperCaseLabels_AreDrawnAsUpperCase()
    {
        var upper = ThemeWith(""" "case": { "label": "upper" } """);
        var plain = Plain();

        using var line = Render(c => TextLine.Draw(c, upper, Slot, "Cpu", TextStep.Label));
        using var lineExpected = Render(c => TextLine.Draw(c, plain, Slot, "CPU", TextStep.Label));
        using var readout = Render(c => Readout.Draw(c, upper, Slot, "41", new ReadoutOptions { Label = "Cpu" }));
        using var readoutExpected = Render(c => Readout.Draw(c, plain, Slot, "41", new ReadoutOptions { Label = "CPU" }));

        AssertSamePixels(lineExpected, line);
        AssertSamePixels(readoutExpected, readout);
    }

    [Fact]
    public void LowerCaseTitles_AreDrawnAsLowerCase_AndTheBodyIsNeverRecased()
    {
        var themed = ThemeWith(""" "case": { "label": "upper", "title": "lower" } """);
        var plain = Plain();

        using var title = Render(c => TextLine.Draw(c, themed, Slot, "Weather", TextStep.Title));
        using var titleExpected = Render(c => TextLine.Draw(c, plain, Slot, "weather", TextStep.Title));
        using var body = Render(c => TextLine.Draw(c, themed, Slot, "Sunny", TextStep.Body));
        using var bodyExpected = Render(c => TextLine.Draw(c, plain, Slot, "Sunny", TextStep.Body));

        AssertSamePixels(titleExpected, title);
        AssertSamePixels(bodyExpected, body);
    }

    [Fact]
    public void Case_IsInvariant_WhateverTheMachinesLanguage()
    {
        var upper = ThemeWith(""" "case": { "label": "upper" } """);
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR"); // Turkish upper-cases i as İ
            var turkish = TextLine.Measure(upper, Slot, "disk", TextStep.Label);
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var invariant = TextLine.Measure(upper, Slot, "disk", TextStep.Label);
            Assert.Equal(invariant, turkish);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }

    [Fact]
    public void ARecasedTitle_StillShrinksToFit()
    {
        var upper = ThemeWith(""" "case": { "title": "upper" } """);
        var plain = Plain();
        var narrow = new SKRect(0, 0, 120, 100);

        var recased = TextLine.Measure(upper, narrow, "Network traffic", TextStep.Title);
        var expected = TextLine.Measure(plain, narrow, "NETWORK TRAFFIC", TextStep.Title);
        var unshrunk = TextLine.Measure(upper, new SKRect(0, 0, 4000, 100), "Network traffic", TextStep.Title);

        // The shrink scales the size linearly; FreeType hints advances, so on Linux the result lands a few pixels short of
        // the width rather than on it. Fitting is the requirement, not filling.
        Assert.True(recased.Width <= 120.01f, $"{recased.Width} should fit 120");
        Assert.True(recased.Height < unshrunk.Height, "the title was shrunk");
        Assert.Equal(expected, recased);
    }

    // ---------------------------------------------------------------- letter spacing

    [Fact]
    public void LabelSpacing_AddsTheSpacingBetweenEachPairOfLetters()
    {
        // A label size of 0.2 cell at a 100 px cell is 20 px, so 0.2 em is 4 px per gap and 8 px over "CPU".
        var spaced = ThemeWith(""" "labelSize": 0.2, "tracking": { "label": 0.2 } """, 100);
        var plain = ThemeWith(""" "labelSize": 0.2 """, 100);
        var wide = new SKRect(0, 0, 400, 100);

        float without = TextLine.Measure(plain, wide, "CPU", TextStep.Label).Width;
        float with = TextLine.Measure(spaced, wide, "CPU", TextStep.Label).Width;

        Assert.Equal(without + 8f, with, 2);
    }

    [Fact]
    public void LabelSpacing_DoesNotTouchTheBody()
    {
        var spaced = ThemeWith(""" "tracking": { "label": 0.3, "title": 0.3 } """);
        var plain = Plain();

        using var body = Render(c => TextLine.Draw(c, spaced, Slot, "Sunny", TextStep.Body));
        using var expected = Render(c => TextLine.Draw(c, plain, Slot, "Sunny", TextStep.Body));

        AssertSamePixels(expected, body);
    }

    [Fact]
    public void ValueSpacing_KeepsTheSizeAndTheCharactersInPlace_AsTheValueChanges()
    {
        var spaced = ThemeWith(""" "tracking": { "value": 0.1 } """);
        var options = new ReadoutOptions { WidestValue = "88:88", Horizontal = HorizontalAlign.Left };

        var a = Readout.Measure(spaced, Slot, "11:11", options);
        var b = Readout.Measure(spaced, Slot, "10:00", options);

        Assert.Equal(a, b);

        // Digits of equal width with the same spacing after each put the colon in the middle, in the same columns in both.
        using var first = Render(c => Readout.Draw(c, spaced, Slot, "11:11", options));
        using var second = Render(c => Readout.Draw(c, spaced, Slot, "10:00", options));
        int middle = (int)a.MidX;
        var colonArea = new SKRectI(middle - 4, 0, middle + 4, 200);
        Assert.Equal(Ink(first, colonArea), Ink(second, colonArea));
        Assert.True(Ink(first, colonArea).Width > 0);
    }

    [Fact]
    public void ValueSpacing_IsPartOfTheFit()
    {
        var spaced = ThemeWith(""" "tracking": { "value": 0.3 } """);
        var plain = Plain();
        var wideAndLow = new SKRect(0, 0, 300, 400); // the width decides the size

        var with = Readout.Measure(spaced, wideAndLow, "1234");
        var without = Readout.Measure(plain, wideAndLow, "1234");

        Assert.True(with.Height < without.Height, $"{with.Height} should be below {without.Height}");
        Assert.True(with.Width <= 300.01f, $"{with.Width} should fit 300");
        Assert.Equal(300f, with.Width, 1);
    }

    [Fact]
    public void LabelSpacing_InAReadout_WidensTheLabel()
    {
        var spaced = ThemeWith(""" "tracking": { "label": 0.4 } """);
        var plain = Plain();
        var options = new ReadoutOptions { Label = "Cpu" };

        using var with = Render(c => Readout.Draw(c, spaced, Slot, "41", options));
        using var without = Render(c => Readout.Draw(c, plain, Slot, "41", options));
        var labelArea = new SKRectI(0, 160, 400, 200);

        Assert.True(Ink(with, labelArea).Width > Ink(without, labelArea).Width);
    }

    // ---------------------------------------------------------------- slant

    [Fact]
    public void ASlantedValue_LeansRight_KeepsItsRectangle_AndLeavesTheLabelUpright()
    {
        var slanted = ThemeWith(""" "slant": 12 """);
        var plain = Plain();
        var options = new ReadoutOptions { Label = "Cpu" };

        Assert.Equal(Readout.Measure(plain, Slot, "41", options), Readout.Measure(slanted, Slot, "41", options));

        using var with = Render(c => Readout.Draw(c, slanted, Slot, "41", options));
        using var without = Render(c => Readout.Draw(c, plain, Slot, "41", options));
        var block = Readout.Measure(plain, Slot, "41", options);
        var valueTop = new SKRectI(0, (int)block.Top, 400, (int)block.Top + 12);
        var labelArea = new SKRectI(0, 165, 400, 200);

        Assert.True(Ink(with, valueTop).Left > Ink(without, valueTop).Left, "the top of the digits moves right");
        Assert.Equal(Ink(without, labelArea), Ink(with, labelArea));
    }

    // ---------------------------------------------------------------- value scale

    [Theory]
    [InlineData(0.8, 1.0, 0.8)]
    [InlineData(1.25, 0.6, 0.75)]
    [InlineData(1.35, 1.0, 1.0)]
    public void ValueScale_MultipliesTheWidgetsSize_ButNeverExceedsTheFit(double valueScale, float widgetScale, float expected)
    {
        var scaled = ThemeWith($""" "valueScale": {valueScale.ToString(System.Globalization.CultureInfo.InvariantCulture)} """);
        var plain = Plain();

        float fitted = Readout.Measure(plain, Slot, "41").Height;
        float drawn = Readout.Measure(scaled, Slot, "41", new ReadoutOptions { Scale = widgetScale }).Height;

        Assert.Equal(fitted * expected, drawn, 2);
    }

    // ---------------------------------------------------------------- fill

    private static readonly SKColor Magenta = new(0xff, 0x00, 0xff);
    private static readonly SKColor Cyan = new(0x00, 0xff, 0xff);

    private static int Count(SKBitmap bitmap, Func<SKColor, bool> test)
    {
        int n = 0;
        foreach (var pixel in bitmap.Pixels)
            if (test(pixel))
                n++;
        return n;
    }

    private static bool Magentaish(SKColor c) => c.Red > 200 && c.Blue > 200 && c.Green < 60;

    private static bool Cyanish(SKColor c) => c.Green > 200 && c.Blue > 200 && c.Red < 60;

    // The muted colour has alpha, so over black it is a grey of its own; any bright grey is the plain text or the muted text.
    private static bool Grey(SKColor c) => c.Red > 80 && Math.Abs(c.Red - c.Green) < 8 && Math.Abs(c.Green - c.Blue) < 8;

    [Fact]
    public void AValueFill_RunsAcrossTheDigits_AndLeavesTheUnitAndLabelAlone()
    {
        var filled = ThemeWith(""" "valueFill": { "colors": ["#ff00ff", "#00ffff"], "angle": 0 } """);
        var options = new ReadoutOptions { Unit = "%", Label = "Cpu" };
        var block = Readout.Measure(filled, Slot, "41", options);

        using var bitmap = Render(c => Readout.Draw(c, filled, Slot, "41", options));
        using var left = new SKBitmap();
        using var right = new SKBitmap();
        int third = (int)(block.Width / 3);
        bitmap.ExtractSubset(left, new SKRectI((int)block.Left, (int)block.Top, (int)block.Left + third, (int)block.Bottom - 40));
        bitmap.ExtractSubset(right, new SKRectI((int)block.Right - 2 * third, (int)block.Top, (int)block.Right - third, (int)block.Bottom - 40));

        Assert.True(Count(left, Magentaish) > 0);
        Assert.Equal(0, Count(left, Cyanish));
        Assert.True(Count(right, Cyanish) > 0);
        using var unit = new SKBitmap();
        using var label = new SKBitmap();
        bitmap.ExtractSubset(unit, new SKRectI((int)block.Right - third / 2, (int)block.Top, (int)block.Right, (int)block.Bottom - 40));
        bitmap.ExtractSubset(label, new SKRectI(0, (int)block.Bottom - 30, 400, 200));
        Assert.True(Count(unit, c => c == filled.Text) > 0, "the unit keeps the text colour");
        Assert.Equal(0, Count(unit, c => Magentaish(c) || Cyanish(c)));
        Assert.True(Count(label, Grey) > 0, "the label keeps the muted colour");
        Assert.Equal(0, Count(label, c => Magentaish(c) || Cyanish(c)));
    }

    [Fact]
    public void AStaleReading_AndAWidgetColour_SkipTheFill()
    {
        var filled = ThemeWith(""" "valueFill": { "colors": ["#ff00ff", "#00ffff"] } """);
        var percent = new ReadingDescriptor("p", ReadingKind.Percent, "P", "P") { Min = 0, Max = 100 };
        var stale = ReadingFormatter.Format(Reading.Stale(37), percent, null, new NoSource());

        using var staleBitmap = Render(c => Readout.Draw(c, filled, Slot, stale));
        using var coloured = Render(c => Readout.Draw(c, filled, Slot, "41", new ReadoutOptions { ValueColor = SKColors.Orange }));

        Assert.Equal(0, Count(staleBitmap, c => Magentaish(c) || Cyanish(c)));
        Assert.True(Count(staleBitmap, Grey) > 0);
        Assert.Equal(0, Count(coloured, c => Magentaish(c) || Cyanish(c)));
        Assert.True(Count(coloured, c => c == SKColors.Orange) > 0);
    }

    private sealed class NoSource : IReadingSource
    {
        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;
    }

    // ---------------------------------------------------------------- glow

    [Fact]
    public void AGlow_IsDrawnAroundTheDigits_WithoutMovingTheLayout()
    {
        var glowing = ThemeWith(""" "valueGlow": { "blur": 0.1, "alpha": 0.4 } """);
        var plain = Plain();

        Assert.Equal(Readout.Measure(plain, Slot, "41"), Readout.Measure(glowing, Slot, "41"));

        using var with = Render(c => Readout.Draw(c, glowing, Slot, "41"));
        using var without = Render(c => Readout.Draw(c, plain, Slot, "41"));
        var all = new SKRectI(0, 0, 400, 200);
        Assert.True(Ink(with, all).Width > Ink(without, all).Width, "the halo reaches beyond the digits");
        Assert.True(Count(with, c => c == plain.Text) > 0, "the digits are drawn sharp over it");
    }

    [Fact]
    public void TheSameValue_IsBlurredOnce()
    {
        var glowing = ThemeWith(""" "valueGlow": { "blur": 0.1 } """);
        var glows = glowing.TextStyle.Glows;

        using (Render(c => Readout.Draw(c, glowing, Slot, "41")))
        using (Render(c => Readout.Draw(c, glowing, new SKRect(20, 10, 420, 210), "41")))
        using (Render(c => Readout.Draw(c, glowing, Slot, "41")))
        {
        }
        Assert.Equal(1, glows.Renders);

        using (Render(c => Readout.Draw(c, glowing, Slot, "42")))
        {
        }
        Assert.Equal(2, glows.Renders);
    }

    [Fact]
    public void TheGlowCache_KeepsABoundedNumberOfCopies()
    {
        var glowing = ThemeWith(""" "valueGlow": { "blur": 0.05 } """);

        for (int i = 0; i < 40; i++)
            using (Render(c => Readout.Draw(c, glowing, Slot, i.ToString(System.Globalization.CultureInfo.InvariantCulture))))
            {
            }
        using (Render(c => Readout.Draw(c, glowing, Slot, "39")))
        {
        }

        Assert.Equal(40, glowing.TextStyle.Glows.Renders); // the newest is still there
        using (Render(c => Readout.Draw(c, glowing, Slot, "0")))
        {
        }
        Assert.Equal(41, glowing.TextStyle.Glows.Renders); // the oldest is not
    }
}
