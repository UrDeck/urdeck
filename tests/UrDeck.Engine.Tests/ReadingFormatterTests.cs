// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public class ReadingFormatterTests
{
    private sealed class Source(bool fahrenheit = false) : IReadingSource
    {
        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => fahrenheit;
    }

    private static readonly ReadingDescriptor Cpu = new("cpu/load", ReadingKind.Percent, "CPU", "CPU load") { Min = 0, Max = 100 };

    private static ReadingText Format(Reading reading, ReadingDescriptor? descriptor = null, ReadingFormatOptions? options = null, bool fahrenheit = false) =>
        ReadingFormatter.Format(reading, descriptor, options, new Source(fahrenheit));

    [Fact]
    public void Percent_RoundsToWholeNumbers_WithARaisedPercentSign()
    {
        var text = Format(Reading.Ok(36.6), Cpu);

        Assert.Equal("37", text.Value);
        Assert.Equal("%", text.Unit);
        Assert.Equal(UnitPlacement.Raised, text.UnitPlacement);
        Assert.True(text.IsCurrent);
    }

    [Theory]
    [InlineData(0.4, "0")]
    [InlineData(0.5, "1")]
    [InlineData(99.5, "100")]
    public void Percent_RoundsHalvesAwayFromZero(double value, string expected) =>
        Assert.Equal(expected, Format(Reading.Ok(value), Cpu).Value);

    [Fact]
    public void Decimals_WidgetOptionBeatsTheDescriptor_WhichBeatsZero()
    {
        var withDecimals = Cpu with { Decimals = 1 };

        Assert.Equal("36.6", Format(Reading.Ok(36.6), withDecimals).Value);
        Assert.Equal("36.62", Format(Reading.Ok(36.623), withDecimals, new ReadingFormatOptions { Decimals = 2 }).Value);
        Assert.Equal("37", Format(Reading.Ok(36.6), Cpu).Value);
    }

    [Fact]
    public void Percent_WidestValueComesFromTheRange_WhateverTheValue()
    {
        Assert.Equal("100", Format(Reading.Ok(8), Cpu).WidestValue);
        Assert.Equal("100", Format(Reading.Ok(100), Cpu).WidestValue);
        Assert.Equal("100", Format(Reading.Unavailable("x"), Cpu).WidestValue);
    }

    [Fact]
    public void Widest_WithDecimals_UsesThemForTheRange() =>
        Assert.Equal("100.0", Format(Reading.Ok(8), Cpu, new ReadingFormatOptions { Decimals = 1 }).WidestValue);

    [Fact]
    public void Widest_WithoutARange_IsTheValueItself()
    {
        var plain = new ReadingDescriptor("w", ReadingKind.Number, "Power", "Power") { Unit = "W" };

        Assert.Equal("1234", Format(Reading.Ok(1234), plain).WidestValue);
    }

    [Fact]
    public void Number_ShowsTheDescriptorUnitOnTheBaseline()
    {
        var plain = new ReadingDescriptor("w", ReadingKind.Number, "Power", "Power") { Unit = "W" };

        var text = Format(Reading.Ok(250), plain);

        Assert.Equal("250", text.Value);
        Assert.Equal("W", text.Unit);
        Assert.Equal(UnitPlacement.Baseline, text.UnitPlacement);
    }

    [Fact]
    public void Number_WithoutADescriptorUnit_HasNoUnit() =>
        Assert.Null(Format(Reading.Ok(3), new ReadingDescriptor("n", ReadingKind.Number, "N", "N")).Unit);

    [Fact]
    public void Text_IsShownAsItIs_AndOnOffAsOnOrOff()
    {
        var label = new ReadingDescriptor("t", ReadingKind.Text, "T", "T");
        var flag = new ReadingDescriptor("f", ReadingKind.OnOff, "F", "F");

        Assert.Equal("Playing", Format(Reading.Ok("Playing"), label).Value);
        Assert.Equal("On", Format(Reading.Ok(true), flag).Value);
        Assert.Equal("Off", Format(Reading.Ok(false), flag).Value);
        Assert.Null(Format(Reading.Ok("Playing"), label).Unit);
    }

    [Fact]
    public void WithoutADescriptor_TheKindFollowsTheValue()
    {
        Assert.Equal("On", Format(Reading.Ok(true)).Value);
        Assert.Equal("hi", Format(Reading.Ok("hi")).Value);
        Assert.Equal("5", Format(Reading.Ok(5)).Value);
    }

    [Fact]
    public void Unavailable_IsADash_WithItsUnitKept_AndNotCurrent()
    {
        var text = Format(Reading.Unavailable("gone"), Cpu);

        Assert.Equal(ReadingFormatter.Dash, text.Value);
        Assert.Equal("%", text.Unit);
        Assert.False(text.IsCurrent);
    }

    [Fact]
    public void PendingWithoutAValue_IsADash()
    {
        var text = Format(Reading.Pending(), Cpu);

        Assert.Equal(ReadingFormatter.Dash, text.Value);
        Assert.False(text.IsCurrent);
    }

    [Fact]
    public void Stale_KeepsItsLastValue_AndIsNotCurrent()
    {
        var text = Format(Reading.Stale(42), Cpu);

        Assert.Equal("42", text.Value);
        Assert.False(text.IsCurrent);
    }

    [Fact]
    public void PendingWithALastValue_ShowsItAsNotCurrent()
    {
        var text = Format(Reading.Pending(42), Cpu);

        Assert.Equal("42", text.Value);
        Assert.False(text.IsCurrent);
    }

    [Fact]
    public void Dash_WithoutARange_IsSizedLikeATwoDigitNumber() =>
        Assert.Equal("88", Format(Reading.Pending()).WidestValue);

    [Fact]
    public void Dash_AndValue_ShareTheWidestValue_SoTheSizeDoesNotJump() =>
        Assert.Equal(Format(Reading.Ok(37), Cpu).WidestValue, Format(Reading.Unavailable("x"), Cpu).WidestValue);

    // Display unit order: the widget's setting, then the descriptor's default, then the region.

    private static readonly ReadingDescriptor Temperature = new("t", ReadingKind.Temperature, "Temp", "Temperature");

    [Fact]
    public void Temperature_ShowsARaisedDegreeSign_InCelsiusByDefault()
    {
        var text = Format(Reading.Ok(41), Temperature);

        Assert.Equal("41", text.Value);
        Assert.Equal("°", text.Unit);
        Assert.Equal(UnitPlacement.Raised, text.UnitPlacement);
    }

    [Fact]
    public void DisplayUnit_ADescriptorThatDeclaresCelsius_BeatsAFahrenheitRegion() =>
        Assert.Equal("20", Format(Reading.Ok(20), Temperature with { DisplayUnit = Sdk.Data.DisplayUnit.Celsius }, fahrenheit: true).Value);

    [Fact]
    public void DisplayUnit_ADescriptorThatDeclaresNone_FollowsTheRegion()
    {
        Assert.Equal("68", Format(Reading.Ok(20), Temperature, fahrenheit: true).Value);
        Assert.Equal("20", Format(Reading.Ok(20), Temperature, fahrenheit: false).Value);
    }

    [Fact]
    public void DisplayUnit_TheWidgetSetting_BeatsTheDescriptor() =>
        Assert.Equal("68", Format(
            Reading.Ok(20),
            Temperature with { DisplayUnit = Sdk.Data.DisplayUnit.Celsius },
            new ReadingFormatOptions { DisplayUnit = Sdk.Data.DisplayUnit.Fahrenheit }).Value);

    [Fact]
    public void Fahrenheit_ConvertsTheRangeToo()
    {
        var ranged = Temperature with { Min = 0, Max = 100 };

        Assert.Equal("212", Format(Reading.Ok(20), ranged, new ReadingFormatOptions { DisplayUnit = Sdk.Data.DisplayUnit.Fahrenheit }).WidestValue);
    }
}
