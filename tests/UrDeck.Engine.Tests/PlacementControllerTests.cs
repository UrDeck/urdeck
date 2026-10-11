// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The placement state machine against scripted display sequences. Time is a number the test advances.</summary>
public class PlacementControllerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly UrDeckConfig _config = new() { MonitorName = "tallest" };
    private readonly FakeDeck _deck = new();
    private readonly PlacementController _controller;
    private TimeSpan _now = TimeSpan.FromHours(1);
    private TimeSpan? _pending;

    public PlacementControllerTests()
    {
        Directory.CreateDirectory(_dir);
        UrDeckLog.LogPath = Path.Combine(_dir, "test.log");
        _controller = new PlacementController(_deck, _deck, () => _config);
        _deck.Displays.AddRange([TestDisplays.Panel(), TestDisplays.Primary(), TestDisplays.Side()]);
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        try
        { Directory.Delete(_dir, true); }
        catch { }
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private static Rectangle PanelBounds => TestDisplays.Panel().Bounds;

    private void Signal(PlacementSignal signal) => _pending = _controller.Signal(signal, _now);

    /// <summary>Calls the controller whenever it asked to be called, until it asks for nothing more.</summary>
    private void Run()
    {
        while (_pending is { } delay)
        {
            _now += delay;
            _pending = _controller.Tick(_now);
        }
    }

    /// <summary>The same for a stretch of time; what is due later stays pending.</summary>
    private void RunFor(TimeSpan time)
    {
        var end = _now + time;
        while (_pending is { } delay && _now + delay <= end)
        {
            _now += delay;
            _pending = _controller.Tick(_now);
        }

        if (_pending is { } rest)
            _pending = rest - (end - _now);
        _now = end;
    }

    /// <summary>Started, placed on the panel, confirm checks done, and the records empty.</summary>
    private void StartPlaced()
    {
        Signal(PlacementSignal.Startup);
        Run();
        Assert.Equal(PlacementState.Placed, _controller.State);
        _deck.ClearRecords();
    }

    private void RemovePanel() => _deck.Displays.RemoveAll(d => d.Model == "RTD1100");

    [Fact]
    public void FirstStart_RemembersTheDisplay_CoversItBeforeShowing_AndBuildsThePageOnce()
    {
        Signal(PlacementSignal.Startup);

        Assert.Equal(["save", "cover", "show"], _deck.Calls);
        Assert.Equal(PlacementState.Settling, _controller.State);
        Assert.Equal(PlacementController.QuietPeriod, _pending);
        var saved = Assert.Single(_deck.Saves);
        Assert.Equal(("HYTE Y70Touch", "RTD1100", TestDisplays.PanelPath, "tallest"), (saved.Name, saved.Model, saved.Path, saved.Selector));
        Assert.Same(saved, _config.Display);

        Run();

        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.Equal(PanelBounds, _deck.ContentBounds);
        Assert.Equal([(PanelBounds, false)], _deck.Covers);
        Assert.Equal([true], _deck.Rebuilds);
        Assert.Single(_deck.Saves);
        Assert.Contains("Display chosen with 'tallest': HYTE Y70Touch", Log);
        Assert.Contains("remove that entry to choose again", Log);
        Assert.Contains("Placement verified on HYTE Y70Touch", Log);
    }

    [Fact]
    public void SecondStart_MatchesByPath_AndSavesNothing()
    {
        _config.Display = TestDisplays.Remembered(TestDisplays.Panel(), "tallest");

        Signal(PlacementSignal.Startup);
        Run();

        Assert.Empty(_deck.Saves);
        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.Single(Log.Split('\n'), line => line.Contains("Display matched by device path: HYTE Y70Touch"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resume_DisplaysReturnOneAfterAnother_ThePanelLast(bool primaryFirst)
    {
        StartPlaced();
        DisplayInfo[] others = primaryFirst ? [TestDisplays.Primary(), TestDisplays.Side()] : [TestDisplays.Side(), TestDisplays.Primary()];

        _deck.Displays.Clear();
        Signal(PlacementSignal.DisplaysChanged);
        Assert.Equal(PlacementState.Absent, _controller.State);
        Assert.Null(_pending);
        Assert.Equal((1, 1), (_deck.Hides, _deck.Releases));

        Signal(PlacementSignal.Resumed);
        foreach (var other in others)
        {
            _now += TimeSpan.FromMilliseconds(400);
            _deck.Displays.Add(other);
            Signal(PlacementSignal.DisplaysChanged);
            Assert.Equal(PlacementState.Absent, _controller.State);
            Assert.False(_deck.Visible);
            Assert.Null(_pending);
        }

        Assert.Empty(_deck.Covers);

        _now += TimeSpan.FromMilliseconds(900);
        _deck.Displays.Add(TestDisplays.Panel(@"\\.\DISPLAY7"));
        Signal(PlacementSignal.DisplaysChanged);
        Signal(PlacementSignal.DisplaysChanged);
        Run();

        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.True(_deck.Visible);
        Assert.Equal(1, _deck.Shows);
        Assert.All(_deck.Covers, c => Assert.Equal(PanelBounds, c.Bounds));
        Assert.Equal([true], _deck.Rebuilds);
        Assert.Equal(PanelBounds, _deck.ContentBounds);
        Assert.Equal(1, _deck.WindowScaling);
    }

    [Fact]
    public void Resume_WithEveryDisplayStillThere_RebuildsOnce_AndDoesNotMoveTheWindow()
    {
        StartPlaced();

        Signal(PlacementSignal.Resumed);
        _now += TimeSpan.FromMilliseconds(100);
        Signal(PlacementSignal.DisplaysChanged);
        _now += TimeSpan.FromMilliseconds(100);
        Signal(PlacementSignal.DisplaysChanged);
        Run();

        Assert.Empty(_deck.Covers);
        Assert.Equal([true], _deck.Rebuilds);
        Assert.Equal(0, _deck.Hides);
    }

    [Fact]
    public void ScalingThatSettlesLate_EndsLaidOutForThePanelsOwnScaling()
    {
        StartPlaced();
        RemovePanel();
        Signal(PlacementSignal.DisplaysChanged);

        // Back on the bus, the window keeps the primary display's scaling for a moment.
        _deck.Displays.Add(TestDisplays.Panel());
        _deck.OnCover = (bounds, _) =>
        {
            _deck.ContentBounds = bounds;
            _deck.WindowScaling = 1.5;
            _deck.LayoutSize = new SizeF(bounds.Width / 1.5f, bounds.Height / 1.5f);
        };
        Signal(PlacementSignal.DisplaysChanged);
        RunFor(TimeSpan.FromSeconds(1));

        Assert.Equal(PlacementState.Placing, _controller.State);
        Assert.Empty(_deck.Rebuilds);
        Assert.Contains("window scaling 150% instead of 100%", Log);

        // The scaling arrives; the layout still has the size of the old one, which the resize refreshes.
        _deck.OnCover = null;
        _deck.WindowScaling = 1;
        Run();

        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.True(_deck.Covers[^1].Nudge);
        Assert.Equal(new SizeF(1100, 3840), _deck.LayoutSize);
        Assert.Equal(PanelBounds, _deck.ContentBounds);
        Assert.Equal([true], _deck.Rebuilds);
    }

    [Fact]
    public void ABurstOfSignals_IsOnePlacementAndOneRebuild()
    {
        StartPlaced();
        var smaller = new Rectangle(3840, -1920, 550, 1920);
        _deck.Displays[0] = TestDisplays.Panel() with { Bounds = smaller };

        for (int i = 0; i < 6; i++)
        {
            Signal(PlacementSignal.DisplaysChanged);
            RunFor(TimeSpan.FromMilliseconds(100));
            Assert.Empty(_deck.Covers);
        }

        Run();

        Assert.Equal([(smaller, false)], _deck.Covers);
        Assert.Equal([false], _deck.Rebuilds);
        Assert.Equal(PlacementState.Placed, _controller.State);
    }

    [Fact]
    public void AStreamOfSignals_PostponesPlacementByTwoSecondsAtMost()
    {
        StartPlaced();
        var smaller = new Rectangle(3840, -1920, 550, 1920);
        _deck.Displays[0] = TestDisplays.Panel() with { Bounds = smaller };
        var start = _now;
        TimeSpan? firstCover = null;
        _deck.OnCover = (bounds, _) =>
        {
            firstCover ??= _now;
            _deck.Land(bounds);
        };

        for (int i = 0; i < 25; i++)
        {
            Signal(PlacementSignal.DisplaysChanged);
            RunFor(TimeSpan.FromMilliseconds(200));
        }

        Run();

        Assert.Equal(start + PlacementController.LongestSettle, firstCover);
        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.Single(_deck.Rebuilds);
    }

    [Fact]
    public void NothingChanged_TheWindowIsNotMoved_AndThePageIsNotRebuilt()
    {
        StartPlaced();

        Signal(PlacementSignal.DisplaysChanged);
        Run();
        Signal(PlacementSignal.WindowChanged);
        Run();
        Signal(PlacementSignal.ConfigReloaded);
        Run();

        Assert.Empty(_deck.Calls);
        Assert.Equal(PlacementState.Placed, _controller.State);
    }

    [Fact]
    public void APlacementThatNeverVerifies_WarnsAfterThirtySeconds_AndTheNextSignalStartsOver()
    {
        StartPlaced();
        var moved = new Rectangle(5000, 0, 1100, 3840);
        _deck.Displays[0] = TestDisplays.Panel() with { Bounds = moved };
        _deck.OnCover = (_, _) => { };
        var start = _now;

        Signal(PlacementSignal.DisplaysChanged);
        Run();

        Assert.Equal(PlacementState.GaveUp, _controller.State);
        Assert.InRange(_now - start, PlacementController.GiveUpAfter, PlacementController.GiveUpAfter + TimeSpan.FromSeconds(5));
        Assert.True(_deck.Covers.Count >= 5);
        Assert.All(_deck.Covers, c => Assert.Equal(moved, c.Bounds));
        string warning = Assert.Single(Log.Split('\n'), line => line.Contains("WARN"));
        Assert.Contains("not reached after 30 s", warning);
        Assert.Contains("expected 1100x3840 at 5000,0", warning);
        Assert.Contains("content area is 1100x3840 at 3840,-3573", warning);

        // The retries grow: 250 ms, 500 ms, 1 s, 2 s, then every 4 s.
        Assert.Contains("Placement attempt 5 on HYTE Y70Touch", Log);

        _deck.OnCover = null;
        int before = _deck.Covers.Count;
        Signal(PlacementSignal.DisplaysChanged);
        Run();

        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.Equal(before + 1, _deck.Covers.Count);
        Assert.Equal(moved, _deck.ContentBounds);
    }

    [Fact]
    public void WindowEvents_WhileAttemptsRun_DoNotStartOver()
    {
        StartPlaced();
        _deck.Displays[0] = TestDisplays.Panel() with { Bounds = new Rectangle(5000, 0, 1100, 3840) };
        _deck.OnCover = (_, _) => { };
        Signal(PlacementSignal.DisplaysChanged);
        RunFor(TimeSpan.FromSeconds(3));
        Assert.Equal(PlacementState.Placing, _controller.State);
        var due = _now + _pending!.Value;

        _now += TimeSpan.FromMilliseconds(10);
        Signal(PlacementSignal.WindowChanged);

        Assert.Equal(PlacementState.Placing, _controller.State);
        Assert.Equal(due, _now + _pending!.Value);

        Signal(PlacementSignal.DisplaysChanged);
        Assert.Equal(PlacementState.Settling, _controller.State);
    }

    [Fact]
    public void AConfirmCheckThatFails_PlacesAgain()
    {
        Signal(PlacementSignal.Startup);
        RunFor(TimeSpan.FromSeconds(1));
        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.NotNull(_pending);
        _deck.ClearRecords();

        // The panel's scaling changes and nothing reports it: the window follows, the layout does not.
        _deck.Displays[0] = TestDisplays.Panel() with { Scaling = 1.25 };
        _deck.WindowScaling = 1.25;
        Run();

        Assert.Contains("no longer holds 2 s after it was verified", Log);
        Assert.Equal([(PanelBounds, true)], _deck.Covers);
        Assert.Equal([false], _deck.Rebuilds);
        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.Equal(new SizeF(880, 3072), _deck.LayoutSize);
    }

    [Fact]
    public void ConfirmChecksThatPass_DoNothing_AndThenNothingIsPending()
    {
        Signal(PlacementSignal.Startup);
        RunFor(TimeSpan.FromSeconds(1));
        _deck.ClearRecords();
        var placedAt = _now - TimeSpan.FromSeconds(1) + PlacementController.QuietPeriod;

        Run();

        Assert.Empty(_deck.Calls);
        Assert.Empty(_deck.States);
        Assert.Equal(placedAt + TimeSpan.FromSeconds(10), _now);
        Assert.Null(_controller.Tick(_now + TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Tick_BeforeItIsDue_SaysHowLongIsLeft()
    {
        Assert.Null(_controller.Tick(_now));

        Signal(PlacementSignal.Startup);

        Assert.Equal(TimeSpan.FromMilliseconds(150), _controller.Tick(_now + TimeSpan.FromMilliseconds(100)));
        Assert.Equal(PlacementState.Settling, _controller.State);
    }

    [Fact]
    public void Unplugged_TheDeckHidesAtOnce_AndReturnsOnAnotherOutput()
    {
        StartPlaced();

        RemovePanel();
        Signal(PlacementSignal.DisplaysChanged);

        Assert.Null(_pending);
        Assert.Equal(["hide", "release"], _deck.Calls);
        Assert.False(_deck.Visible);
        Assert.Equal((PlacementState.Absent, "HYTE Y70Touch"), _deck.States[^1]);
        Assert.Contains("Deck hidden: waiting for 'HYTE Y70Touch'", Log);
        Assert.Contains("LG ULTRAGEAR", Log.Split('\n').Single(line => line.Contains("Deck hidden")));

        // Window events of the hidden window and further display changes leave it hidden.
        Signal(PlacementSignal.WindowChanged);
        Signal(PlacementSignal.DisplaysChanged);
        Assert.Equal(["hide", "release"], _deck.Calls);

        _deck.Displays.Add(TestDisplays.Panel(@"\\.\DISPLAY5", TestDisplays.PanelPathOtherOutput));
        Signal(PlacementSignal.DisplaysChanged);
        Run();

        Assert.Equal(["hide", "release", "save", "cover", "show", "rebuild"], _deck.Calls);
        Assert.Equal(TestDisplays.PanelPathOtherOutput, _config.Display!.Path);
        Assert.Equal("tallest", _config.Display.Selector);
        Assert.Equal([true], _deck.Rebuilds);
        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.Contains("found by its model RTD1100", Log);
        Assert.Contains("Deck shown on HYTE Y70Touch", Log);
    }

    [Fact]
    public void StartedWithoutTheDisplay_ShowsNothing_UntilItIsConnected()
    {
        _config.Display = TestDisplays.Remembered(TestDisplays.Panel(), "tallest");
        RemovePanel();

        Signal(PlacementSignal.Startup);

        Assert.Null(_pending);
        Assert.Empty(_deck.Calls);
        Assert.Equal([(PlacementState.Absent, "HYTE Y70Touch")], _deck.States);
        Assert.Contains("waiting for 'HYTE Y70Touch'", Log);

        _deck.Displays.Add(TestDisplays.Panel());
        Signal(PlacementSignal.DisplaysChanged);
        Run();

        Assert.Equal(["cover", "show", "rebuild"], _deck.Calls);
        Assert.Equal(PanelBounds, _deck.ContentBounds);
        Assert.Equal(PlacementState.Placed, _controller.State);
        Assert.Empty(_deck.Saves);
    }

    [Fact]
    public void WithoutAnyDisplay_NothingIsRememberedOrShown()
    {
        _deck.Displays.Clear();

        Signal(PlacementSignal.Startup);

        Assert.Equal(PlacementState.Absent, _controller.State);
        Assert.Null(_config.Display);
        Assert.Empty(_deck.Calls);
        Assert.Contains("waiting for a display", Log);
    }

    [Fact]
    public void SelectorEdited_MovesTheDeck_AndRemembersTheOtherDisplay()
    {
        StartPlaced();

        _config.MonitorName = "primary";
        Signal(PlacementSignal.ConfigReloaded);
        Run();

        Assert.Equal(TestDisplays.Primary().Bounds, _deck.ContentBounds);
        Assert.Equal(1.5, _deck.WindowScaling);
        Assert.Equal("LG ULTRAGEAR", _config.Display!.Name);
        Assert.Equal("primary", _config.Display.Selector);
        Assert.Single(_deck.Saves);
        Assert.Equal([false], _deck.Rebuilds);
    }

    [Fact]
    public void TwoIdenticalDisplays_WarnOnce_AndThePathIsPinned()
    {
        _config.Display = TestDisplays.Remembered(TestDisplays.Panel());
        RemovePanel();
        _deck.Displays.Add(TestDisplays.Panel(@"\\.\DISPLAY5", @"\\?\DISPLAY#RTD1100#b"));
        _deck.Displays.Add(TestDisplays.Panel(@"\\.\DISPLAY6", @"\\?\DISPLAY#RTD1100#a") with { Bounds = new Rectangle(9000, 0, 1100, 3840) });

        Signal(PlacementSignal.Startup);
        Run();

        Assert.Equal(@"\\?\DISPLAY#RTD1100#a", _config.Display!.Path);
        Assert.Equal(new Rectangle(9000, 0, 1100, 3840), _deck.ContentBounds);
        string warning = Assert.Single(Log.Split('\n'), line => line.Contains("WARN"));
        Assert.Contains("2 displays have the model RTD1100", warning);
        Assert.Contains(@"\\.\DISPLAY5", warning);
        Assert.Contains(@"\\.\DISPLAY6", warning);
    }
}
