// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The date, speed and depth kinds and the compass point (weather-4x4-forecast).</summary>
public class ReadingFormatterKindsTests
{
    private sealed class Source(bool fahrenheit = false, CultureInfo? culture = null) : IReadingSource
    {
        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => fahrenheit;

        public CultureInfo RegionCulture => culture ?? CultureInfo.GetCultureInfo("en-US");
    }

    private static readonly ReadingDescriptor Day = new("daily/0/date", ReadingKind.Date, "Day", "Day");
    private static readonly ReadingDescriptor Wind = new("current/wind-speed", ReadingKind.Speed, "Wind", "Wind") { Min = 0, Max = 150 };
    private static readonly ReadingDescriptor Snow = new("daily/0/snow", ReadingKind.Depth, "Snow", "Snow") { DepthUnit = DepthUnit.Centimetre };
    private static readonly ReadingDescriptor Rain = new("daily/0/rain", ReadingKind.Depth, "Rain", "Rain");
    private static readonly ReadingDescriptor Temperature = new("current/temperature", ReadingKind.Temperature, "Now", "Temperature");
    private static readonly ReadingFormatOptions Imperial = new() { DisplayUnit = Sdk.Data.DisplayUnit.Fahrenheit };
    private static readonly ReadingFormatOptions Metric = new() { DisplayUnit = Sdk.Data.DisplayUnit.Celsius };

    private static readonly DateTimeOffset Tokyo14 = new(2026, 10, 14, 0, 0, 0, TimeSpan.FromHours(9));

    private static ReadingText Format(Reading reading, ReadingDescriptor? descriptor, ReadingFormatOptions? options = null, bool fahrenheit = false, CultureInfo? culture = null) =>
        ReadingFormatter.Format(reading, descriptor, options, new Source(fahrenheit, culture));

    // Date

    [Fact]
    public void Date_DefaultsToTheWeekday_WithNoUnit()
    {
        var text = Format(Reading.Ok(Tokyo14), Day);

        Assert.Equal("Wed", text.Value);
        Assert.Null(text.Unit);
        Assert.True(text.IsCurrent);
    }

    [Fact]
    public void Date_IsReadInTheValuesOwnOffset_NotThePcs()
    {
        // 2026-10-14 00:00 at +09:00 is 2026-10-13 08:00 at -07:00; the weekday is still Wednesday.
        var sameInstant = Tokyo14.ToOffset(TimeSpan.FromHours(-7));
        Assert.Equal(DayOfWeek.Tuesday, sameInstant.DayOfWeek);

        Assert.Equal("Wed", Format(Reading.Ok(Tokyo14), Day).Value);
    }

    [Fact]
    public void Date_MonthDay_FollowsTheRegion()
    {
        var options = new ReadingFormatOptions { DateStyle = DateStyle.MonthDay };

        Assert.Equal("Oct 14", Format(Reading.Ok(Tokyo14), Day, options).Value);
        Assert.Equal("14 Oct", Format(Reading.Ok(Tokyo14), Day, options, culture: CultureInfo.GetCultureInfo("en-GB")).Value);
    }

    [Fact]
    public void Date_Weekday_UsesTheRegionsLanguage() =>
        Assert.Equal("mer.", Format(Reading.Ok(Tokyo14), Day, culture: CultureInfo.GetCultureInfo("fr-FR")).Value);

    [Fact]
    public void Date_WidestValue_IsStable()
    {
        string widest = Format(Reading.Ok(Tokyo14), Day).WidestValue;

        Assert.Equal(3, widest.Length);
        Assert.Equal(widest, Format(Reading.Unavailable("x"), Day).WidestValue);
        Assert.True(Format(Reading.Ok(Tokyo14), Day, new ReadingFormatOptions { DateStyle = DateStyle.MonthDay }).WidestValue.Length >= "Oct 14".Length);
    }

    [Fact]
    public void Date_UnavailableOrPending_IsADash()
    {
        var unavailable = Format(Reading.Unavailable("x"), Day);

        Assert.Equal(ReadingFormatter.Dash, unavailable.Value);
        Assert.False(unavailable.IsCurrent);
        Assert.Equal(ReadingFormatter.Dash, Format(Reading.Pending(), Day).Value);
    }

    [Fact]
    public void Date_Stale_KeepsItsValue_MarkedNotCurrent()
    {
        var text = Format(Reading.Stale(Tokyo14), Day);

        Assert.Equal("Wed", text.Value);
        Assert.False(text.IsCurrent);
    }

    [Fact]
    public void ATimeValueWithoutADescriptor_StillFormatsAsATime() =>
        Assert.Equal("06:42", Format(Reading.Ok(new DateTimeOffset(2026, 10, 14, 6, 42, 0, TimeSpan.Zero)), null, new ReadingFormatOptions { Use24HourClock = true }).Value);

    // Speed

    [Fact]
    public void Speed_ImperialAndMetric()
    {
        var mph = Format(Reading.Ok(16.1), Wind, Imperial);
        var kmh = Format(Reading.Ok(16.1), Wind, Metric);

        Assert.Equal("10", mph.Value);
        Assert.Equal("mph", mph.Unit);
        Assert.Equal(UnitPlacement.Baseline, mph.UnitPlacement);
        Assert.Equal("16", kmh.Value);
        Assert.Equal("km/h", kmh.Unit);
    }

    [Fact]
    public void Speed_RegionDecidesWhenNothingIsChosen()
    {
        Assert.Equal("mph", Format(Reading.Ok(16.1), Wind, fahrenheit: true).Unit);
        Assert.Equal("km/h", Format(Reading.Ok(16.1), Wind, fahrenheit: false).Unit);
    }

    [Fact]
    public void Speed_UnavailableStalePending()
    {
        var unavailable = Format(Reading.Unavailable("x"), Wind, Imperial);

        Assert.Equal(ReadingFormatter.Dash, unavailable.Value);
        Assert.Equal("mph", unavailable.Unit);
        Assert.False(unavailable.IsCurrent);
        Assert.Equal("10", Format(Reading.Stale(16.1), Wind, Imperial).Value);
        Assert.False(Format(Reading.Stale(16.1), Wind, Imperial).IsCurrent);
        Assert.Equal(ReadingFormatter.Dash, Format(Reading.Pending(), Wind, Imperial).Value);
        Assert.Equal("93", unavailable.WidestValue);
    }

    // Depth

    [Fact]
    public void Depth_SnowInInches()
    {
        var text = Format(Reading.Ok(7.62), Snow, Imperial);

        Assert.Equal("3.0", text.Value);
        Assert.Equal("\"", text.Unit);
        Assert.Equal(UnitPlacement.Raised, text.UnitPlacement);
    }

    [Fact]
    public void Depth_SnowInCentimetres()
    {
        var text = Format(Reading.Ok(7.62), Snow, Metric);

        Assert.Equal("7.6", text.Value);
        Assert.Equal("cm", text.Unit);
        Assert.Equal(UnitPlacement.Baseline, text.UnitPlacement);
    }

    [Fact]
    public void Depth_RainInMillimetres_AndInches()
    {
        var mm = Format(Reading.Ok(7), Rain, Metric);

        Assert.Equal("7.0", mm.Value);
        Assert.Equal("mm", mm.Unit);
        Assert.Equal("0.3", Format(Reading.Ok(7), Rain, Imperial).Value);
    }

    [Fact]
    public void Depth_LargeAmount_HasNoDecimal()
    {
        Assert.Equal("11", Format(Reading.Ok(27), Snow, Imperial).Value);
        Assert.Equal("27", Format(Reading.Ok(27), Snow, Metric).Value);
        Assert.Equal("10", Format(Reading.Ok(25.3), Snow, Imperial).Value);
    }

    [Fact]
    public void Depth_Trace_IsLessThanOneTenth()
    {
        Assert.Equal("<0.1", Format(Reading.Ok(0.1), Snow, Imperial).Value);
        Assert.Equal("<0.1", Format(Reading.Ok(0.01), Snow, Metric).Value);
        Assert.Equal("0.1", Format(Reading.Ok(0.1), Snow, Metric).Value);
    }

    [Fact]
    public void Depth_Zero_IsZero()
    {
        Assert.Equal("0", Format(Reading.Ok(0), Snow, Imperial).Value);
        Assert.Equal("0", Format(Reading.Ok(0), Rain, Metric).Value);
    }

    [Fact]
    public void Depth_WithoutADeclaredUnit_IsMillimetres() =>
        Assert.Equal("1.0", Format(Reading.Ok(25.4), new ReadingDescriptor("r", ReadingKind.Depth, "R", "R"), Imperial).Value);

    [Fact]
    public void Depth_UnavailableStalePending()
    {
        var unavailable = Format(Reading.Unavailable("x"), Snow, Imperial);

        Assert.Equal(ReadingFormatter.Dash, unavailable.Value);
        Assert.Equal("\"", unavailable.Unit);
        Assert.False(unavailable.IsCurrent);
        Assert.Equal("3.0", Format(Reading.Stale(7.62), Snow, Imperial).Value);
        Assert.False(Format(Reading.Stale(7.62), Snow, Imperial).IsCurrent);
        Assert.Equal(ReadingFormatter.Dash, Format(Reading.Pending(), Snow, Metric).Value);
    }

    [Fact]
    public void OneSetting_ChoosesTheSystemOfTheWholeCard()
    {
        Assert.Equal("68", Format(Reading.Ok(20), Temperature, Imperial).Value);
        Assert.Equal("mph", Format(Reading.Ok(16), Wind, Imperial, fahrenheit: false).Unit);
        Assert.Equal("\"", Format(Reading.Ok(5), Snow, Imperial, fahrenheit: false).Unit);
    }

    // Compass

    [Theory]
    [InlineData(0, "N")]
    [InlineData(315, "NW")]
    [InlineData(100, "E")]
    [InlineData(359, "N")]
    [InlineData(22.4, "N")]
    [InlineData(22.5, "NE")]
    [InlineData(180, "S")]
    [InlineData(-45, "NW")]
    [InlineData(405, "NE")]
    [InlineData(360, "N")]
    public void CompassPoint_IsTheSectorThatHoldsTheBearing(double degrees, string expected) =>
        Assert.Equal(expected, ReadingFormatter.CompassPoint(degrees));
}
