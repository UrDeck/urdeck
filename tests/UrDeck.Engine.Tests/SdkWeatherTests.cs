// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The SDK pieces of the weather change: the time value and its formatting, attributions and the Lottie component.</summary>
public sealed class SdkWeatherTests : IDisposable
{
    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

    public void Dispose() => _theme.Dispose();

    private sealed class Source(bool use24Hour) : IReadingSource
    {
        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;

        public bool RegionUses24HourClock => use24Hour;
    }

    private static readonly ReadingDescriptor Sunrise = new("today/sunrise", ReadingKind.Time, "Sunrise", "Sunrise");

    private static ReadingText Format(Reading reading, bool use24Hour, ReadingDescriptor? descriptor = null, ReadingFormatOptions? options = null) =>
        ReadingFormatter.Format(reading, descriptor ?? Sunrise, options, new Source(use24Hour));

    private static DateTimeOffset At(int hour, int minute, int offsetHours) =>
        new(2026, 10, 8, hour, minute, 0, TimeSpan.FromHours(offsetHours));

    // The time value

    [Fact]
    public void ATime_KeepsItsInstantAndItsOffset()
    {
        ReadingValue value = At(6, 42, 9);

        Assert.Equal(ReadingValueType.Time, value.Type);
        Assert.Equal(At(6, 42, 9), value.Time);
        Assert.Equal(TimeSpan.FromHours(9), value.Time.Offset);
    }

    [Fact]
    public void ATimeWithAnOffsetInWholeMinutes_KeepsIt()
    {
        var kathmandu = new DateTimeOffset(2026, 10, 8, 5, 45, 0, new TimeSpan(5, 45, 0));

        Assert.Equal(new TimeSpan(5, 45, 0), ReadingValue.FromTime(kathmandu).Time.Offset);
    }

    [Fact]
    public void TheSameInstantAtAnotherOffset_IsNotEqual()
    {
        ReadingValue tokyo = At(6, 42, 9);
        ReadingValue zurich = tokyo.Time.ToOffset(TimeSpan.FromHours(2));

        Assert.Equal(tokyo.Time.UtcDateTime, zurich.Time.UtcDateTime);
        Assert.NotEqual(tokyo, zurich);
        Assert.True(tokyo != zurich);
        Assert.Equal(tokyo, (ReadingValue)At(6, 42, 9));
        Assert.Equal(tokyo.GetHashCode(), ((ReadingValue)At(6, 42, 9)).GetHashCode());
    }

    [Fact]
    public void ANumberHasNoTime_AndATimeHasNoNumberOrText()
    {
        Assert.Equal(DateTimeOffset.UnixEpoch, ReadingValue.FromNumber(5).Time);
        Assert.Equal("", ((ReadingValue)At(6, 42, 9)).Text);
        Assert.Equal("2026-10-08 06:42:00+09:00", ((ReadingValue)At(6, 42, 9)).ToString());
    }

    // The formatter

    [Fact]
    public void ATimeOnA24HourClock_IsHoursAndMinutesWithNoUnit()
    {
        var text = Format(Reading.Ok(At(6, 42, 9)), use24Hour: true);

        Assert.Equal("06:42", text.Value);
        Assert.Null(text.Unit);
        Assert.Equal("00:00", text.WidestValue);
        Assert.True(text.IsCurrent);
    }

    [Fact]
    public void ATimeOnA12HourClock_HasAnAmOrPmUnitOnTheBaseline()
    {
        var evening = Format(Reading.Ok(At(19, 3, -7)), use24Hour: false);
        var midnight = Format(Reading.Ok(At(0, 5, 0)), use24Hour: false);
        var noon = Format(Reading.Ok(At(12, 30, 0)), use24Hour: false);

        Assert.Equal("7:03", evening.Value);
        Assert.Equal("PM", evening.Unit);
        Assert.Equal(UnitPlacement.Baseline, evening.UnitPlacement);
        Assert.Equal("12:05", midnight.Value);
        Assert.Equal("AM", midnight.Unit);
        Assert.Equal("12:30", noon.Value);
        Assert.Equal("PM", noon.Unit);
        Assert.Equal("12:00", evening.WidestValue);
    }

    [Fact]
    public void ATime_IsShownInTheValuesOwnOffset_NotThePcs()
    {
        // 06:42 in Tokyo is the evening before in a zone at -07:00; the text must be Tokyo's.
        Assert.Equal("06:42", Format(Reading.Ok(At(6, 42, 9)), use24Hour: true).Value);
    }

    [Fact]
    public void TheWidgetsClockChoice_BeatsTheRegions()
    {
        var reading = Reading.Ok(At(19, 3, 0));

        Assert.Equal("19:03", Format(reading, use24Hour: false, options: new ReadingFormatOptions { Use24HourClock = true }).Value);
        Assert.Equal("7:03", Format(reading, use24Hour: true, options: new ReadingFormatOptions { Use24HourClock = false }).Value);
    }

    [Fact]
    public void AnUnavailableTime_IsADashThatIsNotCurrent_WithAStableWidth()
    {
        var text = Format(Reading.Unavailable("no sunrise"), use24Hour: true);

        Assert.Equal(ReadingFormatter.Dash, text.Value);
        Assert.False(text.IsCurrent);
        Assert.Equal("00:00", text.WidestValue);
    }

    [Fact]
    public void AStaleTime_KeepsItsValueAndIsNotCurrent()
    {
        var text = Format(Reading.Stale(At(6, 42, 9)), use24Hour: true);

        Assert.Equal("06:42", text.Value);
        Assert.False(text.IsCurrent);
    }

    [Fact]
    public void WithoutADescriptor_ATimeValueStillFormatsAsATime()
    {
        var text = ReadingFormatter.Format(Reading.Ok(At(6, 42, 9)), null, null, new Source(true));

        Assert.Equal("06:42", text.Value);
    }

    // The attribution component

    private static readonly ReadingAttribution OpenMeteo = new("Weather data by Open-Meteo.com", "Open-Meteo.com", "https://open-meteo.com/");

    private static int CountNot(SKBitmap bitmap, SKColor background)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y) != background)
                    count++;
        return count;
    }

    private SKBitmap DrawAttribution(float width, params ReadingAttribution[] attributions)
    {
        var bitmap = new SKBitmap(400, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        Attribution.Draw(canvas, _theme, new SKRect(0, 0, width, 100), attributions);
        return bitmap;
    }

    [Fact]
    public void NoAttribution_NeedsNoHeight_AndDrawsNothing()
    {
        Assert.Equal(0, Attribution.Measure(_theme, [], 300));
        using var bitmap = DrawAttribution(300);
        Assert.Equal(0, CountNot(bitmap, SKColors.Black));
    }

    [Fact]
    public void TheSameTextTwice_IsOneLine()
    {
        float one = Attribution.Measure(_theme, [OpenMeteo], 300);

        Assert.True(one > 0);
        Assert.Equal(one, Attribution.Measure(_theme, [OpenMeteo, OpenMeteo with { Url = null }], 300));
    }

    [Fact]
    public void TwoDifferentTexts_AreTwoLines()
    {
        float one = Attribution.Measure(_theme, [OpenMeteo], 300);
        float two = Attribution.Measure(_theme, [OpenMeteo, new ReadingAttribution("Data by Somebody")], 300);

        Assert.Equal(one * 2, two, 3);
    }

    [Fact]
    public void TheCredit_IsDrawn()
    {
        using var bitmap = DrawAttribution(380, OpenMeteo);

        Assert.True(CountNot(bitmap, SKColors.Black) > 0);
    }

    [Fact]
    public void WhenTheFullTextDoesNotFit_TheShortTextIsDrawn()
    {
        using var wide = DrawAttribution(380, OpenMeteo);
        using var narrow = DrawAttribution(150, OpenMeteo);
        using var noShort = DrawAttribution(150, OpenMeteo with { ShortText = null });

        // Narrow: the short text is drawn at the label size, so it differs from the full text shrunk to fit.
        Assert.True(CountNot(narrow, SKColors.Black) > 0);
        Assert.NotEqual(CountNot(narrow, SKColors.Black), CountNot(noShort, SKColors.Black));
        Assert.True(CountNot(wide, SKColors.Black) > CountNot(narrow, SKColors.Black));
    }

    [Fact]
    public void TheCredit_NeverLeavesItsRectangle()
    {
        using var bitmap = DrawAttribution(60, OpenMeteo);

        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 60; x < bitmap.Width; x++)
                Assert.Equal(SKColors.Black, bitmap.GetPixel(x, y));
    }

    [Fact]
    public void TheCreditFollowsTheThemesLabelSize()
    {
        var definition = new ThemeStore("").Load(ThemeStore.DefaultName);
        using Theme small = ThemeResolver.Resolve(definition, 275);
        using Theme large = ThemeResolver.Resolve(definition, 550);

        float smallHeight = Attribution.Measure(small, [OpenMeteo], 400);
        float largeHeight = Attribution.Measure(large, [OpenMeteo], 800);

        Assert.True(smallHeight > 0);
        Assert.Equal(smallHeight * 2, largeHeight, smallHeight * 0.05f);
    }

    // The Lottie component: a red 40 x 40 square that moves from the left to the right in two seconds, on a 100 x 100 canvas.

    private const string Square = """
        {"v":"5.7.0","fr":30,"ip":0,"op":60,"w":100,"h":100,"nm":"t","ddd":0,"assets":[],
         "layers":[{"ddd":0,"ind":1,"ty":4,"nm":"s","sr":1,
           "ks":{"o":{"a":0,"k":100},"r":{"a":0,"k":0},
                 "p":{"a":1,"k":[{"t":0,"s":[20,50,0],"i":{"x":0.5,"y":1},"o":{"x":0.5,"y":0}},{"t":60,"s":[80,50,0]}]},
                 "a":{"a":0,"k":[0,0,0]},"s":{"a":0,"k":[100,100,100]}},
           "ao":0,
           "shapes":[{"ty":"gr","it":[
             {"ty":"rc","d":1,"s":{"a":0,"k":[40,40]},"p":{"a":0,"k":[0,0]},"r":{"a":0,"k":0}},
             {"ty":"fl","c":{"a":0,"k":[1,0,0,1]},"o":{"a":0,"k":100},"r":1},
             {"ty":"tr","p":{"a":0,"k":[0,0]},"a":{"a":0,"k":[0,0]},"s":{"a":0,"k":[100,100]},"r":{"a":0,"k":0},"o":{"a":0,"k":100}}]}],
           "ip":0,"op":60,"st":0,"bm":0}]}
        """;

    private static LottieAnimation Create()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Square));
        Assert.True(LottieAnimation.TryCreate(stream, out var animation));
        return animation!;
    }

    private static SKBitmap Frame(LottieAnimation animation, double seconds, int width = 100, int height = 100)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        animation.Draw(canvas, new SKRect(0, 0, width, height), seconds);
        return bitmap;
    }

    /// <summary>The middle of the red pixels' horizontal extent, or -1 when there are none.</summary>
    private static int RedColumn(SKBitmap bitmap)
    {
        int min = int.MaxValue, max = -1;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Red > 200 && bitmap.GetPixel(x, y).Green < 60)
                {
                    min = Math.Min(min, x);
                    max = Math.Max(max, x);
                }

        return max < 0 ? -1 : (min + max) / 2;
    }

    [Fact]
    public void TextThatIsNotAnAnimation_FailsWithoutThrowing()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is not lottie"));

        Assert.False(LottieAnimation.TryCreate(stream, out var animation));
        Assert.Null(animation);
    }

    [Fact]
    public void AnEmptyStream_FailsWithoutThrowing()
    {
        using var stream = new MemoryStream();

        Assert.False(LottieAnimation.TryCreate(stream, out _));
    }

    [Fact]
    public void TheDuration_IsTheAnimationsOwn()
    {
        using var animation = Create();

        Assert.Equal(2, animation.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void DrawingAtDifferentTimes_DrawsDifferentMoments()
    {
        using var animation = Create();
        using var start = Frame(animation, 0);
        using var end = Frame(animation, 2);

        Assert.InRange(RedColumn(start), 15, 25);
        Assert.InRange(RedColumn(end), 75, 85);
    }

    [Fact]
    public void ATimePastTheEnd_DrawsTheLastFrame_AndANegativeTimeTheFirst()
    {
        using var animation = Create();
        using var last = Frame(animation, 2);
        using var past = Frame(animation, 50);
        using var first = Frame(animation, 0);
        using var before = Frame(animation, -3);

        Assert.Equal(last.Bytes, past.Bytes);
        Assert.Equal(first.Bytes, before.Bytes);
    }

    [Fact]
    public void AnyOrder_DrawsTheSameFrame()
    {
        using var played = Create();
        using var fresh = Create();
        using var later = Frame(played, 2.0);
        using var second = Frame(played, 0.5);
        using var direct = Frame(fresh, 0.5);

        Assert.Equal(direct.Bytes, second.Bytes);
    }

    [Fact]
    public void ASquareAnimationInAWideRectangle_IsCentredAndNeverLeavesTheFittedSquare()
    {
        using var animation = Create();
        using var wide = Frame(animation, 1, 400, 200);

        // The scale is 2, so the animation covers x 100..300. Nothing is drawn outside it.
        for (int y = 0; y < wide.Height; y++)
            for (int x = 0; x < wide.Width; x++)
                if (x < 100 || x >= 300)
                    Assert.Equal(SKColors.Black, wide.GetPixel(x, y));
        Assert.True(RedColumn(wide) is > 100 and < 300);
    }

    [Fact]
    public void DisposingTwice_IsAllowed_AndDrawingADisposedAnimationIsReported()
    {
        var animation = Create();
        animation.Dispose();
        animation.Dispose();

        using var bitmap = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<ObjectDisposedException>(() => animation.Draw(canvas, new SKRect(0, 0, 10, 10), 0));
    }
}
