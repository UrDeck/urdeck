// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Tests.Support;
using UrDeck.Preview;
using Xunit;

namespace UrDeck.Engine.Tests.Editor;

public class PageEditingTests
{
    private static readonly GridSize Grid = new(4, 12);

    private readonly FakeTime _time = new();

    private (ConfigDocument Document, PageEditing Editing) Open(string json)
    {
        var root = (JsonObject)JsonNode.Parse(json)!;
        var document = new ConfigDocument(root, readOnly: false, time: _time);
        return (document, new PageEditing(document, PreviewWidgets.Get, id => id == "system:cpu/load" ? "CPU load" : null));
    }

    private static string Json(ConfigDocument document) => document.Root.ToJsonString();

    // A page "p-main": Stats 2x2 at column 3 of row 1 (0-based 2,0), Weather 4x2 below it at row 2.
    private const string Main = """
        { "version": 1, "startPage": "p-main",
          "pages": [
            { "id": "p-main", "name": "Main", "widgets": [
              { "typeId": "urdeck.widgets.stats", "col": 2, "row": 0, "width": 2, "height": 2, "slots": [ { "reading": "system:cpu/load" } ] },
              { "typeId": "urdeck.widgets.weather", "col": 0, "row": 2, "width": 4, "height": 2 } ] },
            { "id": "p-home", "name": "Home", "widgets": [] },
            { "id": "p-last", "name": "Last", "widgets": [] } ] }
        """;

    private static readonly WidgetRef Stats = new("p-main", 0);

    [Fact]
    public void Fits_Blocker_AndFirstFree_ScanRowsThenColumns()
    {
        var (document, _) = Open(Main);
        var widgets = document.Typed.Pages[0].Widgets;

        Assert.True(PageEditing.Fits(widgets, null, 0, 0, 2, 2, Grid));
        Assert.False(PageEditing.Fits(widgets, null, 1, 0, 2, 2, Grid));
        Assert.True(PageEditing.Fits(widgets, 0, 1, 0, 2, 2, Grid));
        Assert.False(PageEditing.Fits(widgets, null, 0, 11, 1, 2, Grid));
        Assert.Equal(1, PageEditing.BlockerIndex(widgets, 0, 0, 1, 4, 2));
        Assert.Equal(new Cell(0, 0), PageEditing.FirstFree(widgets, 2, 2, Grid));
        Assert.Equal(new Cell(0, 4), PageEditing.FirstFree(widgets, 4, 1, Grid));
        Assert.Equal(48 - 4 - 8, PageEditing.EmptyCells(widgets, Grid).Count);
    }

    [Fact]
    public void AddWidget_AtACell_TakesTheDefaultSizeAndSettings_AndIsOneUndoStep()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        var outcome = editing.AddWidget("p-main", "urdeck.widgets.shortcut", Grid, new Cell(2, 8));

        Assert.True(outcome.Applied);
        Assert.Equal(new WidgetRef("p-main", 2), outcome.Widget);
        var added = document.Typed.Pages[0].Widgets[2];
        Assert.Equal(("urdeck.widgets.shortcut", 2, 8, 1, 1), (added.WidgetTypeId, added.Col, added.Row, added.Width, added.Height));
        Assert.Equal("https://urdeck.app", added.ExtensionData!["target"].GetString());
        Assert.Equal("Add Shortcut", document.History.UndoLabel);

        document.History.Undo();
        Assert.Equal(before, Json(document));
    }

    [Fact]
    public void AddWidget_ACellWhereItDoesNotFit_GoesToTheFirstFreeSpace()
    {
        var (document, editing) = Open(Main);

        // Stats is 2x2 by default; a 2x2 at column 3 of row 3 would overlap the weather card.
        var outcome = editing.AddWidget("p-main", "urdeck.widgets.stats", Grid, new Cell(3, 3));

        var added = document.Typed.Pages[0].Widgets[outcome.Widget!.Index];
        Assert.Equal((0, 0, 2, 2), (added.Col, added.Row, added.Width, added.Height));
    }

    [Fact]
    public void AddWidget_ACellNearTheRightEdge_MovesLeftToFit()
    {
        var (document, editing) = Open(Main);

        var outcome = editing.AddWidget("p-main", "urdeck.widgets.weather", Grid, new Cell(3, 6));

        var added = document.Typed.Pages[0].Widgets[outcome.Widget!.Index];
        Assert.Equal((0, 6, 4, 2), (added.Col, added.Row, added.Width, added.Height));
    }

    [Fact]
    public void AddWidget_WithoutRoom_AddsNothing_AndSaysSo()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        var outcome = editing.AddWidget("p-main", "urdeck.widgets.weather", new GridSize(4, 1));

        Assert.False(outcome.Applied);
        Assert.Contains("no free space", outcome.Message);
        Assert.Equal(before, Json(document));
        Assert.False(document.History.CanUndo);
    }

    [Theory]
    [InlineData("urdeck.widgets.clock", 4, 2)]
    [InlineData("urdeck.widgets.stats", 2, 2)]
    [InlineData("urdeck.widgets.weather", 4, 2)]
    [InlineData("urdeck.widgets.shortcut", 1, 1)]
    [InlineData("urdeck.widgets.control", 1, 1)]
    [InlineData("urdeck.widgets.nowplaying", 4, 2)]
    public void TheDefaultSize_FollowsTheOrder(string typeId, int width, int height) =>
        Assert.Equal(new System.Drawing.Size(width, height), UrDeck.Engine.Plugin.WidgetDefaults.Size(PreviewWidgets.Get(typeId)!));

    [Fact]
    public void Resize_KeepsTheRow_AndMovesLeftOnlyAsFarAsNeeded()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        var outcome = editing.Resize(Stats, 4, 2, Grid);

        Assert.True(outcome.Applied);
        var stats = document.Typed.Pages[0].Widgets[0];
        Assert.Equal((0, 0, 4, 2), (stats.Col, stats.Row, stats.Width, stats.Height));
        Assert.Equal("Make Stats 4x2", document.History.UndoLabel);
        Assert.Equal(2, document.Typed.Pages[0].Widgets[1].Row);
        document.History.Undo();
        Assert.Equal(before, Json(document));
    }

    [Fact]
    public void Resize_Blocked_NamesWhatIsInTheWay_AndChangesNothing()
    {
        var (document, editing) = Open(Main);
        editing.Resize(Stats, 4, 2, Grid);
        string before = Json(document);

        var outcome = editing.Resize(Stats, 4, 4, Grid);

        Assert.False(outcome.Applied);
        Assert.Equal("4x4 does not fit here.", outcome.Problem!.Title);
        Assert.Equal("Weather is in the way.", outcome.Problem.Message);
        Assert.Equal(new Cell(0, 4), outcome.Problem.FirstFree);
        Assert.Equal(before, Json(document));

        Assert.True(editing.MoveTo(Stats, outcome.Problem.FirstFree!.Value, 4, 4, Grid).Applied);
        var stats = document.Typed.Pages[0].Widgets[0];
        Assert.Equal((0, 4, 4, 4), (stats.Col, stats.Row, stats.Width, stats.Height));
        Assert.Equal("Move Stats", document.History.UndoLabel);
        document.History.Undo();
        Assert.Equal(before, Json(document));
    }

    [Fact]
    public void Resize_PastTheBottom_SaysSo()
    {
        var (_, editing) = Open("""
            { "pages": [ { "id": "p-a", "name": "A", "widgets": [ { "typeId": "urdeck.widgets.stats", "col": 0, "row": 11, "width": 1, "height": 1 } ] } ] }
            """);

        var outcome = editing.Resize(new WidgetRef("p-a", 0), 2, 2, Grid);

        Assert.True(outcome.Problem!.PastBottom);
        Assert.Equal("It would run past the bottom of the page.", outcome.Problem.Message);
        Assert.Equal(new Cell(0, 0), outcome.Problem.FirstFree);
    }

    [Fact]
    public void Remove_IsOneStep_AndUndoRestoresItsSettings()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        Assert.True(editing.RemoveWidget(Stats).Applied);

        Assert.Single(document.Typed.Pages[0].Widgets);
        Assert.Equal("Remove Stats", document.History.UndoLabel);
        document.History.Undo();
        Assert.Equal(before, Json(document));
    }

    [Fact]
    public void Describe_UsesTheLabel_TheFirstReading_OrTheTarget()
    {
        var (document, editing) = Open("""
            { "pages": [ { "id": "p-a", "name": "A", "widgets": [
              { "typeId": "urdeck.widgets.stats", "slots": [ { "reading": "system:cpu/load" } ] },
              { "typeId": "urdeck.widgets.shortcut", "target": "C:\\Tools\\steam.exe" },
              { "typeId": "urdeck.widgets.shortcut", "label": "Docs", "target": "https://urdeck.app" },
              { "typeId": "urdeck.widgets.clock" },
              { "typeId": "acme.missing" } ] } ] }
            """);
        var widgets = document.Typed.Pages[0].Widgets;

        Assert.Equal("Stats \"CPU load\"", editing.Describe(widgets[0]));
        Assert.Equal("Shortcut \"steam\"", editing.Describe(widgets[1]));
        Assert.Equal("Shortcut \"Docs\"", editing.Describe(widgets[2]));
        Assert.Equal("Clock", editing.Describe(widgets[3]));
        Assert.Equal("acme.missing", editing.Describe(widgets[4]));
    }

    [Fact]
    public void AddPage_IsEmpty_NamedPageN_AfterTheCurrent_WithANewId()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        var outcome = editing.AddPage("p-main");

        var pages = document.Typed.Pages;
        Assert.Equal(["Main", "Page 4", "Home", "Last"], pages.Select(p => p.Name));
        Assert.Equal(outcome.PageId, pages[1].Id);
        Assert.Matches("^p-[0-9a-z]{6}$", pages[1].Id);
        Assert.Empty(pages[1].Widgets);
        Assert.Equal("Add a page", document.History.UndoLabel);
        document.History.Undo();
        Assert.Equal(before, Json(document));
    }

    [Fact]
    public void RenamePage_TypingIsOneStep_AndTheIdStays()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        editing.RenamePage("p-home", "G");
        _time.Advance(TimeSpan.FromMilliseconds(200));
        editing.RenamePage("p-home", "Games");

        Assert.Equal(("p-home", "Games"), (document.Typed.Pages[1].Id, document.Typed.Pages[1].Name));
        document.History.Undo();
        Assert.Equal(before, Json(document));
        Assert.False(document.History.CanUndo);
    }

    [Fact]
    public void DuplicatePage_CopiesTheWidgets_WithANewId_AndACopyName()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        var outcome = editing.DuplicatePage("p-main");

        var pages = document.Typed.Pages;
        Assert.Equal(["Main", "Main copy", "Home", "Last"], pages.Select(p => p.Name));
        Assert.NotEqual("p-main", pages[1].Id);
        Assert.Equal(outcome.PageId, pages[1].Id);
        Assert.Equal(2, pages[1].Widgets.Count);
        Assert.Equal(pages.Count, pages.Select(p => p.Id).Distinct().Count());
        document.History.Undo();
        Assert.Equal(before, Json(document));
    }

    [Fact]
    public void MovePage_KeepsTheStartPage()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        Assert.True(editing.MovePage("p-main", 1));
        Assert.True(editing.MovePage("p-main", 1));
        Assert.False(editing.MovePage("p-main", 1));

        Assert.Equal(["Home", "Last", "Main"], document.Typed.Pages.Select(p => p.Name));
        Assert.Equal(2, StartPageResolver.Resolve(document.Typed));
        Assert.Equal("Move Main right", document.History.UndoLabel);
        document.History.Undo();
        document.History.Undo();
        Assert.Equal(before, Json(document));
    }

    [Fact]
    public void DeleteTheStartPage_MakesTheNextOneTheStartPage_InTheSameStep()
    {
        var (document, editing) = Open(Main);
        string before = Json(document);

        Assert.Equal("p-home", editing.NextStartPage("p-main"));
        Assert.True(editing.DeletePage("p-main"));

        Assert.Equal(["Home", "Last"], document.Typed.Pages.Select(p => p.Name));
        Assert.Equal("p-home", document.Typed.StartPage);
        document.History.Undo();
        Assert.Equal(before, Json(document));
    }

    [Fact]
    public void DeleteTheLastPage_WhenItIsTheStartPage_MakesThePreviousOneTheStartPage()
    {
        var (document, editing) = Open(Main);
        editing.SetStartPage("p-last");

        Assert.True(editing.DeletePage("p-last"));

        Assert.Equal("p-home", document.Typed.StartPage);
    }

    [Fact]
    public void ThePageThatIsNotTheStartPage_IsDeletedAlone_AndTheOnlyPageCannotBe()
    {
        var (document, editing) = Open(Main);

        Assert.Null(editing.NextStartPage("p-home"));
        Assert.True(editing.DeletePage("p-home"));
        Assert.Equal("p-main", document.Typed.StartPage);
        Assert.True(editing.DeletePage("p-last"));
        Assert.False(editing.DeletePage("p-main"));
        Assert.Single(document.Typed.Pages);
    }

    [Fact]
    public void SetStartPage_IsAStep()
    {
        var (document, editing) = Open(Main);

        Assert.False(editing.SetStartPage("p-main"));
        Assert.True(editing.SetStartPage("p-home"));

        Assert.Equal("p-home", document.Typed.StartPage);
        Assert.Equal("Make Home the start page", document.History.UndoLabel);
    }

    [Fact]
    public void SetIndicator_WritesTheMode_AndAutomaticRemovesTheKey()
    {
        var (document, editing) = Open(Main);

        Assert.False(editing.SetIndicator(IndicatorMode.Auto));
        Assert.True(editing.SetIndicator(IndicatorMode.Fade));
        Assert.Equal("fade", document.Typed.Pager.Indicator);
        Assert.True(editing.SetIndicator(IndicatorMode.Auto));
        Assert.Null(document.Typed.Pager.Indicator);
    }

    [Fact]
    public void SetUpdates_Off_WritesOff_AndOnRemovesTheKey()
    {
        var (document, editing) = Open(Main);

        Assert.False(editing.SetUpdates(true));
        Assert.True(editing.SetUpdates(false));
        Assert.Equal("off", document.Typed.Updates);
        Assert.False(editing.SetUpdates(false));
        Assert.True(editing.SetUpdates(true));
        Assert.Null(document.Root["updates"]);
    }

    [Fact]
    public void TheSelection_SurvivesAnUndo_ThatMovesItsIndex_AndIsGoneWithTheWidget()
    {
        var (document, editing) = Open(Main);
        var added = editing.AddWidget("p-main", "urdeck.widgets.shortcut", Grid, new Cell(0, 6)).Widget!;
        var selection = WidgetSelection.Of(document.Typed, new WidgetRef("p-main", 1))!; // the weather card
        editing.RemoveWidget(Stats);

        var after = selection.Resolve(document.Typed);
        Assert.Equal(new WidgetRef("p-main", 0), after!.Ref);
        document.History.Undo();
        Assert.Equal(new WidgetRef("p-main", 1), after.Resolve(document.Typed)!.Ref);

        var shortcut = WidgetSelection.Of(document.Typed, added)!;
        document.History.Undo(); // the shortcut is gone
        Assert.Null(shortcut.Resolve(document.Typed));
    }

    [Fact]
    public void TheDock_HasASelection_AndCanBeRemovedFrom_ButNotResized()
    {
        var (document, editing) = Open("""
            { "pages": [ { "id": "p-a", "name": "A" } ], "dock": [ { "typeId": "urdeck.widgets.shortcut", "label": "Docs" } ] }
            """);

        Assert.NotNull(WidgetSelection.Of(document.Typed, WidgetRef.Dock(0)));
        Assert.False(editing.Resize(WidgetRef.Dock(0), 2, 2, Grid).Applied);
        Assert.True(editing.RemoveWidget(WidgetRef.Dock(0)).Applied);
        Assert.Empty(document.Typed.Dock);
    }

    [Fact]
    public void AReadOnlyDocument_ChangesNothing()
    {
        var root = (JsonObject)JsonNode.Parse(Main)!;
        var document = new ConfigDocument(root, readOnly: true, time: _time);
        var editing = new PageEditing(document, PreviewWidgets.Get);

        Assert.False(editing.AddWidget("p-main", "urdeck.widgets.shortcut", Grid).Applied);
        Assert.False(editing.RemoveWidget(Stats).Applied);
        Assert.False(editing.DeletePage("p-home"));
    }

    [Fact]
    public void GridSize_ComesFromTheLayout_NotAFixedNumber()
    {
        var theme = DeckRig.Dark.Definition;
        var config = ConfigStore.Parse(Main);

        var tall = GridSize.For(new UrDeck.Engine.Deck.DeckSurface(new System.Drawing.Size(1100, 3840), 1), config, theme);
        var short_ = GridSize.For(new UrDeck.Engine.Deck.DeckSurface(new System.Drawing.Size(1100, 1920), 1), config, theme);

        Assert.Equal(4, tall.Columns);
        Assert.True(tall.Rows > short_.Rows);
        Assert.InRange(tall.Rows, 10, 14);
    }
}
