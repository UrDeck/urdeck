// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Display;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public class DisplayChoiceTests
{
    [Fact]
    public void Choose_WithNothingRemembered_RemembersTheDisplayWithoutASelector()
    {
        var config = new UrDeckConfig();

        Assert.True(DisplayChoice.Choose(config, TestDisplays.Panel()));

        Assert.Equal("HYTE Y70Touch", config.Display!.Name);
        Assert.Equal("RTD1100", config.Display.Model);
        Assert.Equal(TestDisplays.PanelPath, config.Display.Path);
        Assert.Null(config.Display.Selector);
    }

    [Fact]
    public void Choose_TheDisplayAlreadyChosen_ChangesNothing()
    {
        var config = new UrDeckConfig { Display = TestDisplays.Remembered(TestDisplays.Panel()) };
        var before = config.Display;

        Assert.False(DisplayChoice.Choose(config, TestDisplays.Panel(@"\\.\DISPLAY9")));

        Assert.Same(before, config.Display);
    }

    [Fact]
    public void Choose_TheDisplayTheSelectorsPicked_PinsIt()
    {
        var config = new UrDeckConfig { Display = TestDisplays.Remembered(TestDisplays.Panel(), "tallest") };

        Assert.True(DisplayChoice.Choose(config, TestDisplays.Panel()));

        Assert.Null(config.Display!.Selector);
        Assert.Equal(TestDisplays.PanelPath, config.Display.Path);
    }

    [Fact]
    public void Choose_AnotherDisplay_ReplacesTheRememberedOne_AndKeepsUnknownProperties()
    {
        using var doc = JsonDocument.Parse("42");
        var remembered = TestDisplays.Remembered(TestDisplays.Panel(), "tallest");
        remembered.ExtensionData = new Dictionary<string, JsonElement> { ["note"] = doc.RootElement.Clone() };
        var config = new UrDeckConfig { Display = remembered };

        Assert.True(DisplayChoice.Choose(config, TestDisplays.Primary()));

        Assert.Equal("LG ULTRAGEAR", config.Display!.Name);
        Assert.Null(config.Display.Selector);
        Assert.Equal(42, config.Display.ExtensionData!["note"].GetInt32());
    }

    [Fact]
    public void Choose_FindsTheSamePathWithoutRegardToCase()
    {
        var config = new UrDeckConfig { Display = TestDisplays.Remembered(TestDisplays.Panel()) };

        Assert.False(DisplayChoice.Choose(config, TestDisplays.Panel(path: TestDisplays.PanelPath.ToLowerInvariant())));
    }

    [Fact]
    public void ChooseAutomatically_ForgetsAnExplicitChoice()
    {
        var config = new UrDeckConfig { Display = TestDisplays.Remembered(TestDisplays.Panel()) };

        Assert.True(DisplayChoice.ChooseAutomatically(config));

        Assert.Null(config.Display);
    }

    [Fact]
    public void ChooseAutomatically_WhenTheSelectorsAreAlreadyInCharge_ChangesNothing()
    {
        var chosenBySelector = new UrDeckConfig { Display = TestDisplays.Remembered(TestDisplays.Panel(), "tallest") };
        var nothing = new UrDeckConfig();

        Assert.False(DisplayChoice.ChooseAutomatically(chosenBySelector));
        Assert.False(DisplayChoice.ChooseAutomatically(nothing));

        Assert.NotNull(chosenBySelector.Display);
    }
}
