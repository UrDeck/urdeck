// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public class DisplaySelectorTests
{
    private static readonly DisplayInfo[] Three = [TestDisplays.Panel(), TestDisplays.Primary(), TestDisplays.Side()];

    [Theory]
    [InlineData("primary", 1, "LG ULTRAGEAR")]
    [InlineData("tallest", 1, "HYTE Y70Touch")]
    [InlineData("Widest", 1, "LG ULTRAGEAR")]
    [InlineData("largest", 1, "LG ULTRAGEAR")]
    [InlineData("DISPLAY3", 1, "HP Z23n G2")]
    [InlineData(" display1 ", 2, "HYTE Y70Touch")]
    [InlineData(null, 3, "HP Z23n G2")]
    [InlineData("", 2, "LG ULTRAGEAR")]
    [InlineData("DISPLAY9", 3, "HP Z23n G2")]
    [InlineData("DISPLAY9", 7, "LG ULTRAGEAR")]
    [InlineData(null, 0, "LG ULTRAGEAR")]
    public void Choose_FollowsTheSelectorRules(string? monitorName, int monitor, string expected)
    {
        var config = new UrDeckConfig { MonitorName = monitorName, Monitor = monitor };

        Assert.Equal(expected, DisplaySelector.Choose(config, Three)!.Name);
    }

    [Fact]
    public void Choose_Tallest_WithoutAPortraitDisplay_FallsBackToTheIndex()
    {
        var config = new UrDeckConfig { MonitorName = "tallest", Monitor = 1 };

        Assert.Equal("LG ULTRAGEAR", DisplaySelector.Choose(config, [TestDisplays.Primary()])!.Name);
        Assert.Null(DisplaySelector.Choose(config, []));
    }

    [Theory]
    [InlineData("Tallest", 1, "tallest")]
    [InlineData(" DISPLAY1 ", 2, "display1")]
    [InlineData(null, 1, "index:1")]
    [InlineData("  ", 3, "index:3")]
    public void CurrentSelector_IsTheNameOrTheIndex(string? monitorName, int monitor, string expected) =>
        Assert.Equal(expected, DisplaySelector.CurrentSelector(new UrDeckConfig { MonitorName = monitorName, Monitor = monitor }));

    [Fact]
    public void LegacyConfigWithoutDisplay_ChoosesWithTheSelector_AndSaysWhatToRemember()
    {
        var config = new UrDeckConfig { MonitorName = "tallest" };

        var selection = DisplaySelector.Select(config, Three);

        Assert.Equal(DisplayMatch.Chosen, selection.Match);
        Assert.Equal("HYTE Y70Touch", selection.Display!.Name);
        Assert.Equal("HYTE Y70Touch", selection.Remember!.Name);
        Assert.Equal("RTD1100", selection.Remember.Model);
        Assert.Equal(TestDisplays.PanelPath, selection.Remember.Path);
        Assert.Equal("tallest", selection.Remember.Selector);
        Assert.Null(config.Display);
    }

    [Fact]
    public void RememberedDisplay_IsFoundUnderAnotherDeviceName_PositionAndScaling()
    {
        var config = new UrDeckConfig { MonitorName = "DISPLAY1", Display = TestDisplays.Remembered(TestDisplays.Panel(), "display1") };
        var moved = TestDisplays.Panel(@"\\.\DISPLAY3") with { Bounds = new Rectangle(0, 0, 1100, 3840), Scaling = 1.25 };

        var selection = DisplaySelector.Select(config, [TestDisplays.Primary(@"\\.\DISPLAY1"), TestDisplays.Side(@"\\.\DISPLAY2"), moved]);

        Assert.Equal(DisplayMatch.Path, selection.Match);
        Assert.Same(moved, selection.Display);
        Assert.Null(selection.Remember);
    }

    [Fact]
    public void PathIsMatchedWithoutRegardToCase()
    {
        var config = new UrDeckConfig { Display = TestDisplays.Remembered(TestDisplays.Panel()) };
        config.Display.Path = TestDisplays.PanelPath.ToUpperInvariant();

        Assert.Equal(DisplayMatch.Path, DisplaySelector.Select(config, Three).Match);
    }

    [Fact]
    public void ATallerDisplayAddedLater_DoesNotTakeTheDeck()
    {
        var config = new UrDeckConfig { MonitorName = "tallest", Display = TestDisplays.Remembered(TestDisplays.Panel(), "tallest") };
        var taller = new DisplayInfo("Tower", "TWR0001", @"\\?\DISPLAY#TWR0001#1", @"\\.\DISPLAY4", new Rectangle(9000, 0, 1440, 5120), 1, false);

        var selection = DisplaySelector.Select(config, [taller, .. Three]);

        Assert.Equal("HYTE Y70Touch", selection.Display!.Name);
    }

    [Fact]
    public void MovedToAnotherOutput_IsFoundByModel_AndThePathIsRePinned()
    {
        var remembered = TestDisplays.Remembered(TestDisplays.Panel(), "tallest");
        remembered.ExtensionData = new Dictionary<string, JsonElement> { ["future"] = JsonSerializer.SerializeToElement("x") };
        var config = new UrDeckConfig { MonitorName = "tallest", Display = remembered };
        var moved = TestDisplays.Panel(path: TestDisplays.PanelPathOtherOutput);

        var selection = DisplaySelector.Select(config, [moved, TestDisplays.Primary(), TestDisplays.Side()]);

        Assert.Equal(DisplayMatch.Model, selection.Match);
        Assert.Same(moved, selection.Display);
        Assert.Equal(TestDisplays.PanelPathOtherOutput, selection.Remember!.Path);
        Assert.Equal("tallest", selection.Remember.Selector);
        Assert.Equal("x", selection.Remember.ExtensionData!["future"].GetString());
    }

    [Fact]
    public void TwoIdenticalModels_WithoutTheRememberedPath_UseTheFirstByPathOrder()
    {
        var config = new UrDeckConfig { Display = TestDisplays.Remembered(TestDisplays.Panel()) };
        var b = TestDisplays.Panel(@"\\.\DISPLAY1", @"\\?\DISPLAY#RTD1100#b");
        var a = TestDisplays.Panel(@"\\.\DISPLAY4", @"\\?\DISPLAY#RTD1100#A");

        var selection = DisplaySelector.Select(config, [b, TestDisplays.Primary(), a]);
        var reversed = DisplaySelector.Select(config, [a, TestDisplays.Primary(), b]);

        Assert.Equal(DisplayMatch.AmbiguousModel, selection.Match);
        Assert.Same(a, selection.Display);
        Assert.Same(a, reversed.Display);
        Assert.Equal([a, b], selection.Candidates);
        Assert.Equal(a.Path, selection.Remember!.Path);
        Assert.Null(selection.Remember.Selector);
    }

    [Fact]
    public void TwoIdenticalModels_WithTheRememberedPath_AreNotAmbiguous()
    {
        var config = new UrDeckConfig { Display = TestDisplays.Remembered(TestDisplays.Panel()) };
        var other = TestDisplays.Panel(@"\\.\DISPLAY4", @"\\?\DISPLAY#RTD1100#A");

        var selection = DisplaySelector.Select(config, [other, TestDisplays.Panel()]);

        Assert.Equal(DisplayMatch.Path, selection.Match);
        Assert.Equal(TestDisplays.PanelPath, selection.Display!.Path);
    }

    [Fact]
    public void EmptyModel_MatchesByPathAlone()
    {
        // A display Windows reports no target for: its device name is its path and it has no model.
        var virtualDisplay = new DisplayInfo("DISPLAY1", "", @"\\.\DISPLAY1", @"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1080), 1, true);
        var config = new UrDeckConfig { Display = TestDisplays.Remembered(virtualDisplay) };

        Assert.Equal(DisplayMatch.Path, DisplaySelector.Select(config, [virtualDisplay]).Match);

        var another = virtualDisplay with { Path = @"\\.\DISPLAY2", DeviceName = @"\\.\DISPLAY2" };
        var gone = DisplaySelector.Select(config, [another]);
        Assert.Equal(DisplayMatch.Absent, gone.Match);
        Assert.Null(gone.Display);
    }

    [Fact]
    public void SelectorEdited_ChoosesAgain()
    {
        var config = new UrDeckConfig { MonitorName = "primary", Display = TestDisplays.Remembered(TestDisplays.Panel(), "tallest") };

        var selection = DisplaySelector.Select(config, Three);

        Assert.Equal(DisplayMatch.Chosen, selection.Match);
        Assert.Equal("LG ULTRAGEAR", selection.Display!.Name);
        Assert.Equal("primary", selection.Remember!.Selector);
    }

    [Fact]
    public void DisplayWithoutSelector_IsKeptWhateverTheSelectorsSay()
    {
        var config = new UrDeckConfig { MonitorName = "primary", Monitor = 3, Display = TestDisplays.Remembered(TestDisplays.Panel()) };

        var selection = DisplaySelector.Select(config, Three);

        Assert.Equal(DisplayMatch.Path, selection.Match);
        Assert.Equal("HYTE Y70Touch", selection.Display!.Name);
    }

    [Fact]
    public void RememberedDisplayNotConnected_IsAbsent_AndThePrimaryIsNotTaken()
    {
        var config = new UrDeckConfig { MonitorName = "tallest", Display = TestDisplays.Remembered(TestDisplays.Panel(), "tallest") };

        var selection = DisplaySelector.Select(config, [TestDisplays.Primary(), TestDisplays.Side()]);

        Assert.Equal(DisplayMatch.Absent, selection.Match);
        Assert.Null(selection.Display);
        Assert.Null(selection.Remember);
    }

    [Fact]
    public void AnEntryThatIdentifiesNothing_ChoosesAgain_AndNoDisplayAtAllIsAbsent()
    {
        var config = new UrDeckConfig { MonitorName = "tallest", Display = new RememberedDisplay { Name = "?" } };

        Assert.Equal(DisplayMatch.Chosen, DisplaySelector.Select(config, Three).Match);
        Assert.Equal(DisplayMatch.Absent, DisplaySelector.Select(new UrDeckConfig(), []).Match);
    }

    [Fact]
    public void DisplayInfo_DescribesItselfForTheLog()
    {
        Assert.Equal(@"HYTE Y70Touch (RTD1100) \\.\DISPLAY1 1100x3840 at 3840,-3573, 100%", TestDisplays.Panel().ToString());
        Assert.Equal(@"LG ULTRAGEAR (GSM5CE2) \\.\DISPLAY2 3840x2160 at 0,0, 150% (primary)", TestDisplays.Primary().ToString());
    }
}

public class DisplayConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public DisplayConfigTests()
    {
        Directory.CreateDirectory(_dir);
        UrDeckLog.LogPath = Path.Combine(_dir, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        try
        { Directory.Delete(_dir, true); }
        catch { }
    }

    private string ConfigPath => Path.Combine(_dir, "urdeck-config.json");

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    [Fact]
    public void Display_IsAbsentFromADefaultConfiguration()
    {
        using var store = new ConfigStore(ConfigPath);

        Assert.Null(store.Config.Display);
        Assert.DoesNotContain("\"display\"", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void Display_RoundTripsWithUnknownProperties()
    {
        File.WriteAllText(ConfigPath, """
            { "monitorName": "tallest",
              "display": { "name": "HYTE Y70Touch", "model": "RTD1100", "path": "\\\\?\\DISPLAY#RTD1100#x", "selector": "tallest", "future": { "x": 1 } } }
            """);
        using (var store = new ConfigStore(ConfigPath))
        {
            var display = store.Config.Display!;
            Assert.Equal(("HYTE Y70Touch", "RTD1100", @"\\?\DISPLAY#RTD1100#x", "tallest"), (display.Name, display.Model, display.Path, display.Selector));
            store.Save();
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        var saved = doc.RootElement.GetProperty("display");
        Assert.Equal("HYTE Y70Touch", saved.GetProperty("name").GetString());
        Assert.Equal("RTD1100", saved.GetProperty("model").GetString());
        Assert.Equal(@"\\?\DISPLAY#RTD1100#x", saved.GetProperty("path").GetString());
        Assert.Equal("tallest", saved.GetProperty("selector").GetString());
        Assert.Equal(1, saved.GetProperty("future").GetProperty("x").GetInt32());
    }

    [Fact]
    public void Display_WithoutSelector_IsWrittenWithoutOne()
    {
        File.WriteAllText(ConfigPath, "{\"display\":{\"name\":\"A\",\"model\":\"AAA0001\",\"path\":\"p\"}}");
        using (var store = new ConfigStore(ConfigPath))
            store.Save();

        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        Assert.False(doc.RootElement.GetProperty("display").TryGetProperty("selector", out _));
    }

    [Fact]
    public void AnOlderFile_LoadsWithoutAWarning_AndItsSelectorChooses()
    {
        File.WriteAllText(ConfigPath, "{\"pages\":[{\"name\":\"Main\",\"widgets\":[]}],\"monitor\":1,\"monitorName\":\"tallest\"}");
        using var store = new ConfigStore(ConfigPath);

        Assert.Null(store.Config.Display);
        Assert.DoesNotContain("WARN", Log);
        var selection = DisplaySelector.Select(store.Config, [TestDisplays.Primary(), TestDisplays.Panel()]);
        Assert.Equal("HYTE Y70Touch", selection.Display!.Name);
    }
}
