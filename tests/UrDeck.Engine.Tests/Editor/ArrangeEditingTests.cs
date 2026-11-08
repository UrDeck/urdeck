// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Tests.Support;
using UrDeck.Preview;
using Xunit;

namespace UrDeck.Engine.Tests.Editor;

/// <summary>The operations behind dragging: drop targets, swaps, resize stops and the dock moves.</summary>
public class ArrangeEditingTests
{
    private static readonly GridSize Grid = new(4, 12);

    private readonly FakeTime _time = new();

    private (ConfigDocument Document, PageEditing Editing) Open(string json)
    {
        var document = new ConfigDocument((JsonObject)JsonNode.Parse(json)!, readOnly: false, time: _time);
        return (document, new PageEditing(document, PreviewWidgets.Get));
    }

    // Stats 2x2 at (2,0); a second Stats 2x2 at (0,0); Weather 4x2 at (0,2); a Shortcut 1x1 at (0,4).
    private const string Page = """
        { "version": 1, "startPage": "p",
          "pages": [ { "id": "p", "name": "Main", "widgets": [
            { "typeId": "urdeck.widgets.stats", "col": 2, "row": 0, "width": 2, "height": 2 },
            { "typeId": "urdeck.widgets.stats", "col": 0, "row": 0, "width": 2, "height": 2 },
            { "typeId": "urdeck.widgets.weather", "col": 0, "row": 2, "width": 4, "height": 2 },
            { "typeId": "urdeck.widgets.shortcut", "col": 0, "row": 4, "width": 1, "height": 1 } ] },
            { "id": "q", "name": "Other", "widgets": [] } ] }
        """;

    private static readonly WidgetRef Right = new("p", 0);
    private static readonly WidgetRef Left = new("p", 1);
    private static readonly WidgetRef Weather = new("p", 2);
    private static readonly WidgetRef Shortcut = new("p", 3);

    [Fact]
    public void DropTarget_FreeCells_AreFree_AndTheColumnIsClamped()
    {
        var (_, editing) = Open(Page);

        Assert.Equal(new DropTarget(DropKind.Free, new Cell(2, 6)), editing.DropTarget(Right, new Cell(2, 6), Grid));
        Assert.Equal(new Cell(2, 6), editing.DropTarget(Right, new Cell(3, 6), Grid).Cell);
        Assert.Equal(DropKind.Free, editing.DropTarget(Right, new Cell(2, 0), Grid).Kind); // onto itself
    }

    [Fact]
    public void DropTarget_OverAWidgetOfTheSameSize_IsASwap()
    {
        var (_, editing) = Open(Page);

        var target = editing.DropTarget(Right, new Cell(0, 0), Grid);

        Assert.Equal(DropKind.Swap, target.Kind);
        Assert.Equal(Left, target.Other);
        Assert.Equal("Release to swap with Stats", target.Message);
    }

    [Fact]
    public void DropTarget_OverAWidgetOfAnotherSize_OrTwoWidgets_IsBlockedWithTheBlocker()
    {
        var (_, editing) = Open(Page);

        var weather = editing.DropTarget(Right, new Cell(0, 3), Grid);
        Assert.Equal(DropKind.Blocked, weather.Kind);
        Assert.Equal("Weather is in the way", weather.Message);

        // A 2x2 at column 1, row 1 overlaps the other Stats card and Weather: not a swap.
        Assert.Equal(DropKind.Blocked, editing.DropTarget(Left, new Cell(1, 1), Grid).Kind);
    }

    [Fact]
    public void DropTarget_PastTheBottom_IsBlocked()
    {
        var (_, editing) = Open(Page);

        var target = editing.DropTarget(Right, new Cell(0, 11), Grid);

        Assert.Equal(DropKind.Blocked, target.Kind);
        Assert.True(target.PastBottom);
        Assert.Equal("It would run past the bottom of the page", target.Message);
    }

    [Fact]
    public void Swap_ExchangesTwoWidgetsOfTheSameSize_InOneUndoStep()
    {
        var (document, editing) = Open(Page);
        string before = document.Root.ToJsonString();

        Assert.True(editing.Swap(Right, Left).Applied);

        var widgets = document.Typed.Pages[0].Widgets;
        Assert.Equal((0, 0), (widgets[0].Col, widgets[0].Row));
        Assert.Equal((2, 0), (widgets[1].Col, widgets[1].Row));
        Assert.Equal("Swap Stats and Stats", document.History.UndoLabel);
        document.History.Undo();
        Assert.Equal(before, document.Root.ToJsonString());
    }

    [Fact]
    public void Swap_RefusesWidgetsOfDifferentSizes()
    {
        var (document, editing) = Open(Page);
        string before = document.Root.ToJsonString();

        Assert.False(editing.Swap(Right, Weather).Applied);
        Assert.Equal(before, document.Root.ToJsonString());
    }

    [Fact]
    public void ResizeStops_MarkWhatFitsAndWhatIsInTheWay()
    {
        var (_, editing) = Open(Page);

        var stops = editing.ResizeStops(Right, Grid).ToDictionary(s => s.Label);

        Assert.True(stops["2x2"].Fits);
        Assert.True(stops["1x1"].Fits);
        // 4x2 at the right edge moves left to column 0 and covers the other Stats card.
        Assert.False(stops["4x2"].Fits);
        Assert.Equal("Stats", stops["4x2"].Blocker);
        Assert.False(stops["4x4"].Fits);
    }

    [Fact]
    public void ResizeStops_AtTheBottomEdge_AreMarkedPastTheBottom()
    {
        var (_, editing) = Open(Page);
        editing.MoveTo(Shortcut, new Cell(0, 11), 1, 1, Grid);

        var stops = editing.ResizeStops(Shortcut, Grid);

        Assert.All(stops.Where(s => s.Height > 1), s => Assert.True(s.PastBottom));
        Assert.Empty(editing.ResizeStops(WidgetRef.Dock(0), Grid));
    }

    [Fact]
    public void MoveToDock_RemovesFromThePage_AndIsOneUndoStep()
    {
        var (document, editing) = Open(Page);
        string before = document.Root.ToJsonString();

        var outcome = editing.MoveToDock(Shortcut, 0);

        Assert.True(outcome.Applied);
        Assert.Equal(WidgetRef.Dock(0), outcome.Widget);
        Assert.Equal(3, document.Typed.Pages[0].Widgets.Count);
        Assert.Equal("urdeck.widgets.shortcut", Assert.Single(document.Typed.Dock).WidgetTypeId);
        Assert.Equal("Move Shortcut to the dock", document.History.UndoLabel);
        document.History.Undo();
        Assert.Equal(before, document.Root.ToJsonString());
    }

    [Fact]
    public void MoveToDock_RefusesATypeWithout1x1_AndAFullDock()
    {
        var (document, editing) = Open(Page);
        Assert.False(editing.MoveToDock(Weather, 0).Applied);
        Assert.Empty(document.Typed.Dock);

        var (full, fullEditing) = Open("""
            { "pages": [ { "id": "p", "name": "Main", "widgets": [ { "typeId": "urdeck.widgets.shortcut", "col": 0, "row": 0, "width": 1, "height": 1 } ] } ],
              "dock": [ { "typeId": "urdeck.widgets.shortcut" }, { "typeId": "urdeck.widgets.shortcut" },
                        { "typeId": "urdeck.widgets.shortcut" }, { "typeId": "urdeck.widgets.shortcut" } ] }
            """);
        Assert.False(fullEditing.MoveToDock(new WidgetRef("p", 0), 4).Applied);
        Assert.Equal(4, full.Typed.Dock.Count);
    }

    [Fact]
    public void AddToDock_AppendsA1x1Widget_ForTypesThatSupportIt()
    {
        var (document, editing) = Open(Page);

        Assert.False(editing.AddToDock("urdeck.widgets.weather").Applied);
        var outcome = editing.AddToDock("urdeck.widgets.shortcut");

        Assert.Equal(WidgetRef.Dock(0), outcome.Widget);
        Assert.Equal((1, 1), (document.Typed.Dock[0].Width, document.Typed.Dock[0].Height));
        Assert.Equal("Add Shortcut to the dock", document.History.UndoLabel);
    }

    [Fact]
    public void MoveWidgetToPage_KeepsSizeAndSettings_InOneUndoStep()
    {
        var (document, editing) = Open(Page);
        string before = document.Root.ToJsonString();

        var outcome = editing.MoveWidgetToPage(Weather, "q", Grid);

        Assert.True(outcome.Applied);
        Assert.Equal(new WidgetRef("q", 0), outcome.Widget);
        Assert.Equal(3, document.Typed.Pages[0].Widgets.Count);
        var moved = document.Typed.Pages[1].Widgets[0];
        Assert.Equal(("urdeck.widgets.weather", 0, 0, 4, 2), (moved.WidgetTypeId, moved.Col, moved.Row, moved.Width, moved.Height));
        Assert.Equal("Move Weather to Other", document.History.UndoLabel);
        document.History.Undo();
        Assert.Equal(before, document.Root.ToJsonString());
    }

    [Fact]
    public void MoveWidgetToPage_RefusesWhenNoSpaceFits_AndForTheSamePageOrTheDock()
    {
        var (document, editing) = Open("""
            { "pages": [ { "id": "p", "name": "Main", "widgets": [ { "typeId": "urdeck.widgets.weather", "col": 0, "row": 0, "width": 4, "height": 2 } ] },
                         { "id": "q", "name": "Full", "widgets": [ { "typeId": "urdeck.widgets.weather", "col": 0, "row": 0, "width": 4, "height": 12 } ] } ] }
            """);
        string before = document.Root.ToJsonString();

        var refused = editing.MoveWidgetToPage(new WidgetRef("p", 0), "q", Grid);

        Assert.False(refused.Applied);
        Assert.Equal("There is no free 4x2 space on Full.", refused.Message);
        Assert.False(editing.MoveWidgetToPage(new WidgetRef("p", 0), "p", Grid).Applied);
        Assert.False(editing.MoveWidgetToPage(WidgetRef.Dock(0), "q", Grid).Applied);
        Assert.Equal(before, document.Root.ToJsonString());
    }

    [Fact]
    public void AddWidget_WhenTheDefaultSizeHasNoRoom_TakesTheLargestSizeThatFits()
    {
        // Rows 0-9 are full; row 10 and 11 hold seven Shortcuts, so one 1x1 cell is left and nothing larger.
        var cells = Enumerable.Range(0, 8).Select(i => (Col: i % 4, Row: 10 + i / 4)).Take(7);
        string shortcuts = string.Join(", ", cells.Select(c => $$"""{ "typeId": "urdeck.widgets.shortcut", "col": {{c.Col}}, "row": {{c.Row}}, "width": 1, "height": 1 }"""));
        var (document, editing) = Open($$"""
            { "pages": [ { "id": "p", "name": "Main", "widgets": [
                { "typeId": "urdeck.widgets.weather", "col": 0, "row": 0, "width": 4, "height": 2 },
                { "typeId": "urdeck.widgets.stats", "col": 0, "row": 2, "width": 4, "height": 4 },
                { "typeId": "urdeck.widgets.stats", "col": 0, "row": 6, "width": 4, "height": 4 },
                {{shortcuts}} ] } ] }
            """);

        var outcome = editing.AddWidget("p", "urdeck.widgets.stats", Grid);

        Assert.True(outcome.Applied);
        var added = document.Typed.Pages[0].Widgets[^1];
        Assert.Equal((3, 11, 1, 1), (added.Col, added.Row, added.Width, added.Height));
    }

    [Fact]
    public void MoveInDock_ReordersTheSlots()
    {
        var (document, editing) = Open("""
            { "pages": [ { "id": "p", "name": "Main" } ],
              "dock": [ { "typeId": "urdeck.widgets.shortcut", "label": "A" }, { "typeId": "urdeck.widgets.shortcut", "label": "B" },
                        { "typeId": "urdeck.widgets.shortcut", "label": "C" } ] }
            """);

        Assert.True(editing.MoveInDock(0, 2).Applied);

        Assert.Equal(["B", "C", "A"], document.Typed.Dock.Select(d => d.ExtensionData!["label"].GetString()));
        Assert.Equal("Move Shortcut in the dock", document.History.UndoLabel);
        Assert.False(editing.MoveInDock(1, 1).Applied);
        Assert.False(editing.MoveInDock(0, 3).Applied);
    }

    [Fact]
    public void MoveToPage_ReturnsASlotWithItsEarlierSize_WhenItFits()
    {
        var (document, editing) = Open("""
            { "pages": [ { "id": "p", "name": "Main", "widgets": [] } ],
              "dock": [ { "typeId": "urdeck.widgets.stats", "col": 1, "row": 3, "width": 2, "height": 2 } ] }
            """);

        var outcome = editing.MoveToPage(0, "p", Grid, new Cell(2, 5));

        Assert.True(outcome.Applied);
        var back = document.Typed.Pages[0].Widgets[0];
        Assert.Equal((2, 5, 2, 2), (back.Col, back.Row, back.Width, back.Height));
        Assert.Empty(document.Typed.Dock);
        Assert.Equal("Move Stats to the page", document.History.UndoLabel);
    }

    [Fact]
    public void MoveToPage_FallsBackTo1x1_AndRefusesWhenNothingFits()
    {
        var (document, editing) = Open("""
            { "pages": [ { "id": "p", "name": "Main", "widgets": [
                { "typeId": "urdeck.widgets.weather", "col": 0, "row": 1, "width": 4, "height": 2 } ] } ],
              "dock": [ { "typeId": "urdeck.widgets.stats", "width": 2, "height": 2 } ] }
            """);

        // 2x2 at row 0 would cover Weather's row 1: it comes back as 1x1.
        Assert.True(editing.MoveToPage(0, "p", Grid, new Cell(0, 0)).Applied);
        var back = document.Typed.Pages[0].Widgets[1];
        Assert.Equal((0, 0, 1, 1), (back.Col, back.Row, back.Width, back.Height));

        // Onto Weather itself: nothing fits, nothing changes.
        Assert.True(editing.MoveToDock(new WidgetRef("p", 1), 0).Applied);
        var refused = editing.MoveToPage(0, "p", Grid, new Cell(0, 1));
        Assert.False(refused.Applied);
        Assert.NotNull(refused.Message);
        Assert.Single(document.Typed.Dock);
    }
}
