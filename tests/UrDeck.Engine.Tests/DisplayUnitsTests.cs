// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The public unit conversion that a form and the formatter share (settings-metadata).</summary>
public class DisplayUnitsTests
{
    private static readonly ReadingDescriptor Snow = new("daily/0/snow", ReadingKind.Depth, "Snow", "Snow") { DepthUnit = DepthUnit.Centimetre };
    private static readonly ReadingDescriptor Rain = new("daily/0/rain", ReadingKind.Depth, "Rain", "Rain");

    [Fact]
    public void Temperature_converts_both_ways()
    {
        Assert.Equal(167, DisplayUnits.ToDisplay(ReadingKind.Temperature, 75, DisplayUnit.Fahrenheit, null), 6);
        Assert.Equal(75, DisplayUnits.FromDisplay(ReadingKind.Temperature, 167, DisplayUnit.Fahrenheit, null), 6);
        Assert.Equal(75, DisplayUnits.ToDisplay(ReadingKind.Temperature, 75, DisplayUnit.Celsius, null));
    }

    [Fact]
    public void Speed_converts_both_ways()
    {
        double mph = DisplayUnits.ToDisplay(ReadingKind.Speed, 100, DisplayUnit.Fahrenheit, null);
        Assert.Equal(62.137, mph, 3);
        Assert.Equal(100, DisplayUnits.FromDisplay(ReadingKind.Speed, mph, DisplayUnit.Fahrenheit, null), 6);
    }

    [Theory]
    [InlineData(true, 25.4, 10)]
    [InlineData(false, 25.4, 1)]
    public void Depth_converts_by_the_canonical_unit(bool centimetres, double canonical, double inches)
    {
        var descriptor = centimetres ? Snow : Rain;
        Assert.Equal(inches, DisplayUnits.ToDisplay(ReadingKind.Depth, canonical, DisplayUnit.Fahrenheit, descriptor), 6);
        Assert.Equal(canonical, DisplayUnits.FromDisplay(ReadingKind.Depth, inches, DisplayUnit.Fahrenheit, descriptor), 6);
        Assert.Equal(canonical, DisplayUnits.ToDisplay(ReadingKind.Depth, canonical, DisplayUnit.Celsius, descriptor));
    }

    [Fact]
    public void A_kind_with_no_conversion_returns_the_value()
    {
        Assert.Equal(42, DisplayUnits.ToDisplay(ReadingKind.Percent, 42, DisplayUnit.Fahrenheit, null));
        Assert.Equal(42, DisplayUnits.FromDisplay(ReadingKind.Number, 42, DisplayUnit.Fahrenheit, null));
    }

    [Fact]
    public void Unit_text_follows_the_kind_and_the_unit()
    {
        Assert.Equal("km/h", DisplayUnits.UnitText(ReadingKind.Speed, DisplayUnit.Celsius, null));
        Assert.Equal("mph", DisplayUnits.UnitText(ReadingKind.Speed, DisplayUnit.Fahrenheit, null));
        Assert.Equal("cm", DisplayUnits.UnitText(ReadingKind.Depth, DisplayUnit.Celsius, Snow));
        Assert.Equal("mm", DisplayUnits.UnitText(ReadingKind.Depth, DisplayUnit.Celsius, Rain));
        Assert.Equal("\"", DisplayUnits.UnitText(ReadingKind.Depth, DisplayUnit.Fahrenheit, Rain));
        Assert.Equal("°", DisplayUnits.UnitText(ReadingKind.Temperature, DisplayUnit.Celsius, null));
        Assert.Null(DisplayUnits.UnitText(ReadingKind.Text, DisplayUnit.Celsius, null));
    }
}
