// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class ChromeLayoutDockTests
{
    private static readonly Size Panel = new(1100, 3840);
    private static readonly double BuiltInDock = new ThemeStore("").Load(ThemeStore.DefaultName).Definition.Dock!.Height!.Value;

    [Theory]
    [InlineData(IndicatorMode.Always)]
    [InlineData(IndicatorMode.Auto)]
    public void Panel_WithTheBuiltInDock_KeepsThirteenRows(IndicatorMode configured)
    {
        var r = ChromeLayout.Compute(Panel, configured, 2, 0.25, BuiltInDock);

        Assert.Equal(IndicatorMode.Always, r.Mode);
        Assert.Equal(13, r.Rows);
        Assert.Equal(new Rectangle(0, 3645, 1100, 195), r.Dock);
        Assert.Equal(new Rectangle(0, 3576, 1100, 69), r.Indicator);
        Assert.Equal(new Rectangle(0, 0, 1100, 3576), r.Grid);
    }

    [Fact]
    public void Always_TheThreeRectangles_DoNotOverlap_AndAddUpToTheScreen()
    {
        var r = ChromeLayout.Compute(Panel, IndicatorMode.Always, 2, 0.25, BuiltInDock);

        Assert.Equal(0, r.Grid.Top);
        Assert.Equal(r.Grid.Bottom, r.Indicator.Top);
        Assert.Equal(r.Indicator.Bottom, r.Dock.Top);
        Assert.Equal(Panel.Height, r.Dock.Bottom);
        Assert.Equal(Panel.Height, r.Grid.Height + r.Indicator.Height + r.Dock.Height);
        Assert.All(new[] { r.Grid, r.Indicator, r.Dock }, rect => Assert.Equal(Panel.Width, rect.Width));
    }

    [Fact]
    public void Fade_PutsTheIndicatorDirectlyAboveTheDock_OverTheGrid()
    {
        var r = ChromeLayout.Compute(Panel, IndicatorMode.Fade, 2, 0.25, BuiltInDock);

        Assert.Equal(IndicatorMode.Fade, r.Mode);
        Assert.Equal(new Rectangle(0, 0, 1100, 3645), r.Grid);
        Assert.Equal(r.Dock.Top, r.Indicator.Bottom);
        Assert.Equal(69, r.Indicator.Height);
        Assert.False(r.Indicator.IntersectsWith(r.Dock));
    }

    [Fact]
    public void Off_ReservesTheDockAlone()
    {
        var r = ChromeLayout.Compute(Panel, IndicatorMode.Off, 2, 0.25, BuiltInDock);

        Assert.Equal(Rectangle.Empty, r.Indicator);
        Assert.Equal(new Rectangle(0, 3645, 1100, 195), r.Dock);
        Assert.Equal(new Rectangle(0, 0, 1100, 3645), r.Grid);
        Assert.Equal(13, r.Rows);
    }

    [Fact]
    public void ShortScreen_LosesRows_AndAutoBecomesFade()
    {
        // 1000 wide: a cell is 250, the dock 178 and the band 60.
        var plain = ChromeLayout.Compute(new Size(1000, 1000), IndicatorMode.Off, 2, 0.24);
        var docked = ChromeLayout.Compute(new Size(1000, 1000), IndicatorMode.Auto, 2, 0.24, 0.71);

        Assert.Equal(4, plain.Rows);
        Assert.Equal(IndicatorMode.Fade, docked.Mode);
        Assert.Equal(178, docked.Dock.Height);
        Assert.Equal(822, docked.Grid.Height);
        Assert.Equal(3, docked.Rows);
    }

    [Fact]
    public void Auto_CountsTheRowsLeftAfterBothBands()
    {
        // Without a dock the band leaves four rows; the dock takes one of them, so the indicator floats.
        var plain = ChromeLayout.Compute(new Size(1000, 1060), IndicatorMode.Auto, 2, 0.24);
        var docked = ChromeLayout.Compute(new Size(1000, 1060), IndicatorMode.Auto, 2, 0.24, 0.71);

        Assert.Equal(IndicatorMode.Always, plain.Mode);
        Assert.Equal(IndicatorMode.Fade, docked.Mode);
    }

    [Fact]
    public void ADockTallerThanTheScreen_IsClamped()
    {
        var r = ChromeLayout.Compute(new Size(1000, 100), IndicatorMode.Always, 2, 0.24, 0.71);

        Assert.Equal(new Rectangle(0, 0, 1000, 100), r.Dock);
        Assert.Equal(0, r.Indicator.Height);
        Assert.Equal(0, r.Grid.Height);
        Assert.Equal(0, r.Rows);
    }

    [Theory]
    [InlineData(IndicatorMode.Always)]
    [InlineData(IndicatorMode.Fade)]
    [InlineData(IndicatorMode.Off)]
    [InlineData(IndicatorMode.Auto)]
    public void WithoutADock_TheResultIsWhatItWas(IndicatorMode configured)
    {
        var r = ChromeLayout.Compute(Panel, configured, 2, 0.25, 0);

        Assert.Equal(Rectangle.Empty, r.Dock);
        Assert.Equal(ChromeLayout.Compute(Panel, configured, 2, 0.25), r);
    }
}

public class DockLayoutTests
{
    // The dock's band on the 1100x3840 panel; the built-in gap of 0.06 is 11.7 pixels of a 195 pixel slot.
    private static readonly Rectangle Band = new(0, 3645, 1100, 195);
    private const double Gap = 0.06;

    [Fact]
    public void FourEntries_AreFourEqualCards_CentredAsAGroup()
    {
        var items = DockLayout.Compute(Band, 4, Gap);

        Assert.Equal(4, items.Count);
        Assert.Equal(195, DockLayout.SlotSide(Band));
        Assert.All(items, i => Assert.Equal(new Size(183, 183), i.Size));
        Assert.All(items, i => Assert.Equal(new Size(195, 195), i.CellSize));
        Assert.All(items, i => Assert.Equal(new Size(1, 1), i.GridSize));
        Assert.Equal([160, 355, 550, 745], items.Select(i => i.CellPosition.X));
        Assert.Equal(new System.Drawing.Point(166, 3651), items[0].Position);
        // The same space to the left of the first card and to the right of the last.
        Assert.Equal(items[0].Position.X - Band.Left, Band.Right - (items[3].Position.X + items[3].Size.Width));
    }

    [Fact]
    public void NeighbouringCards_AreOneGapApart_AndNoCardTouchesTheBandsEdge()
    {
        var items = DockLayout.Compute(Band, 4, Gap);

        for (int i = 1; i < items.Count; i++)
            Assert.Equal(12, items[i].Position.X - (items[i - 1].Position.X + items[i - 1].Size.Width));
        Assert.All(items, i => Assert.True(i.Position.Y > Band.Top && i.Position.Y + i.Size.Height < Band.Bottom));
    }

    [Fact]
    public void TwoEntries_AreACentredPair_TheFirstAtTheLeft()
    {
        var items = DockLayout.Compute(Band, 2, Gap);

        Assert.Equal(2, items.Count);
        Assert.Equal([355, 550], items.Select(i => i.CellPosition.X));
        Assert.Equal(items[0].Position.X - Band.Left, Band.Right - (items[1].Position.X + items[1].Size.Width));
    }

    [Fact]
    public void OneEntry_IsCentred_AndItsCardIsTheSizeOfAnyOther()
    {
        var one = DockLayout.Compute(Band, 1, Gap).Single();

        Assert.Equal(new Size(183, 183), one.Size);
        Assert.InRange(one.Position.X - Band.Left - (Band.Right - (one.Position.X + one.Size.Width)), -1, 1);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(40)]
    public void MoreThanFourEntries_GiveFourSlots(int entries) =>
        Assert.Equal(4, DockLayout.Compute(Band, entries, Gap).Count);

    [Fact]
    public void NoEntries_OrNoBand_GiveNothing()
    {
        Assert.Empty(DockLayout.Compute(Band, 0, Gap));
        Assert.Empty(DockLayout.Compute(Rectangle.Empty, 3, Gap));
    }

    [Fact]
    public void AScreenNarrowerThanFourSlots_ShrinksTheSlotToAQuarterOfTheWidth()
    {
        var narrow = new Rectangle(0, 800, 400, 195);

        var items = DockLayout.Compute(narrow, 4, Gap);

        Assert.Equal(100, DockLayout.SlotSide(narrow));
        Assert.Equal([0, 100, 200, 300], items.Select(i => i.CellPosition.X));
        Assert.All(items, i => Assert.Equal(items[0].Size, i.Size));
        Assert.All(items, i => Assert.True(narrow.Contains(new Rectangle(i.Position, i.Size))));
        // The smaller squares sit in the middle of the band's height.
        Assert.InRange(items[0].Position.Y - narrow.Top - (narrow.Bottom - (items[0].Position.Y + items[0].Size.Height)), -1, 1);
    }

    [Fact]
    public void WithoutAGap_TheCardIsTheSlot()
    {
        var items = DockLayout.Compute(Band, 2, 0);

        Assert.Equal(new Rectangle(355, 3645, 195, 195), new Rectangle(items[0].Position, items[0].Size));
        Assert.Equal(items[0].Position.X + items[0].Size.Width, items[1].Position.X);
    }
}

public sealed class DockEntryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-dock-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly WidgetRegistry _registry = new();

    [Widget("Small", "d", Id = "test.small")]
    [WidgetSize(1, 1)]
    [RefreshOnData]
    private sealed class Small : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context) { }
    }

    [Widget("Wide", "d", Id = "test.wide")]
    [WidgetSize(4, 2)]
    [RefreshOnData]
    private sealed class Wide : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context) { }
    }

    public DockEntryTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
        _registry.Register(WidgetDescriptor.TryCreate(typeof(Small), out _)!);
        _registry.Register(WidgetDescriptor.TryCreate(typeof(Wide), out _)!);
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private static WidgetConfig Entry(string typeId, bool visible = true) => new() { WidgetTypeId = typeId, IsVisible = visible };

    private List<DockEntry> Select(params WidgetConfig[] dock) => DockLayout.Select(dock, _registry.GetDescriptor);

    [Fact]
    public void AWidgetWithTheSizeOneByOne_IsShown_WithoutAWarning()
    {
        var entries = Select(Entry("test.small"), Entry("TEST.SMALL"));

        Assert.All(entries, e => Assert.True(e.Shown));
        Assert.Equal("", Log);
    }

    [Fact]
    public void EntriesAfterTheFourth_AreDropped_WithOneWarning_AndStayInTheList()
    {
        var dock = Enumerable.Range(0, 6).Select(_ => Entry("test.small")).ToArray();

        var entries = Select(dock);

        Assert.Equal(4, entries.Count);
        Assert.Equal(6, dock.Length);
        Assert.Single(Log.Split('\n'), line => line.Contains("6 entries"));
    }

    [Fact]
    public void AnEntryWithoutATypeId_LeavesItsSlotEmpty_WithAWarning()
    {
        // The shape the field had before it was used: no typeId, the rest in the extension data.
        var old = JsonSerializer.Deserialize<WidgetConfig>("""{ "type": "app", "command": "notepad.exe", "url": "" }""")!;

        var entries = Select(old, Entry("test.small"));

        Assert.False(entries[0].Shown);
        Assert.True(entries[1].Shown);
        Assert.Contains("Dock entry 1 has no typeId", Log);
    }

    [Fact]
    public void AnUnregisteredType_LeavesItsSlotEmpty_WithAWarning()
    {
        var entries = Select(Entry("test.small"), Entry("test.gone"));

        Assert.True(entries[0].Shown);
        Assert.False(entries[1].Shown);
        Assert.Contains("'test.gone'", Log);
        Assert.Contains("not registered", Log);
    }

    [Fact]
    public void ATypeWithoutTheSizeOneByOne_LeavesItsSlotEmpty_WithAWarningNamingIt()
    {
        var entries = Select(Entry("test.wide"), Entry("test.small"));

        Assert.False(entries[0].Shown);
        Assert.True(entries[1].Shown);
        Assert.Contains("'test.wide' does not support the size 1x1", Log);
    }

    [Fact]
    public void AnInvisibleEntry_LeavesItsSlotEmpty_WithoutAWarning()
    {
        var entries = Select(Entry("test.small", visible: false), Entry("test.small"));

        Assert.False(entries[0].Shown);
        Assert.True(entries[1].Shown);
        Assert.Equal("", Log);
    }

    [Fact]
    public void TheWidgetIsCreatedFromAOneByOneCopy_AndTheLoadedEntryIsUntouched()
    {
        var loaded = JsonSerializer.Deserialize<WidgetConfig>(
            """{ "typeId": "test.small", "col": 3, "row": 9, "width": 4, "height": 2, "target": "notepad.exe", "parameters": { "a": "b" } }""")!;

        var copy = Select(Entry("test.small"), loaded)[1].Config;

        Assert.NotSame(loaded, copy);
        Assert.Equal(("test.small", 1, 1), (copy.WidgetTypeId, copy.Width, copy.Height));
        Assert.Equal((1, 0), (copy.Col, copy.Row));
        Assert.Equal("notepad.exe", copy.ExtensionData!["target"].GetString());
        Assert.Equal("b", copy.Parameters["a"]);
        Assert.NotSame(loaded.ExtensionData, copy.ExtensionData);
        Assert.NotSame(loaded.Parameters, copy.Parameters);
        Assert.Equal((3, 9, 4, 2), (loaded.Col, loaded.Row, loaded.Width, loaded.Height));
    }

    [Fact]
    public void TheLayoutItems_CarryTheType_AndAnEmptySlotIsNotPlaced()
    {
        var entries = Select(Entry("test.small"), Entry("test.wide"), Entry("test.small", visible: false));

        var items = DockLayout.Compute(new Rectangle(0, 3645, 1100, 195), entries, 0.06);

        Assert.Equal(["test.small", "test.wide", "test.small"], items.Select(i => i.WidgetTypeId));
        Assert.Equal([true, false, false], items.Select(i => i.Placed));
        Assert.Equal([true, true, false], items.Select(i => i.Visible));
        // An empty slot still takes its place: the third entry is in the third slot.
        Assert.Equal(items[0].CellPosition.X + 2 * 195, items[2].CellPosition.X);
    }

    [Fact]
    public void HitTest_FindsTheCardUnderAPoint_AndNothingElsewhereInTheBand()
    {
        var band = new Rectangle(0, 3645, 1100, 195);
        var entries = Select(Entry("test.small"), Entry("test.gone"), Entry("test.small"));
        var items = DockLayout.Compute(band, entries, 0.06);
        int y = band.Top + band.Height / 2;

        // On a card: its index, and the point relative to the card.
        var first = WidgetHitTest.Find(items, items[0].Position.X + 20, y);
        Assert.Equal(new WidgetHit(0, 20, y - items[0].Position.Y), first);
        Assert.Equal(2, WidgetHitTest.Find(items, items[2].Position.X + 5, y)!.Value.Index);

        // On the border between two slots, half a gap from either card.
        Assert.Null(WidgetHitTest.Find(items, items[1].CellPosition.X, y));
        // In the band beside the group, to the left and to the right.
        Assert.Null(WidgetHitTest.Find(items, 10, y));
        Assert.Null(WidgetHitTest.Find(items, band.Right - 10, y));
        // On the slot that stays empty.
        Assert.Null(WidgetHitTest.Find(items, items[1].Position.X + 50, y));
        // In the strip between the cards and the band's edge.
        Assert.Null(WidgetHitTest.Find(items, items[0].Position.X + 20, band.Top + 1));
    }
}

public sealed class DockSnapshotTests : IDisposable
{
    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);
    private static readonly SKColor Background = SKColor.Parse(Dark.Definition.Colors!.Background);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-dock-snapshot-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public DockSnapshotTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private sealed class Probe(Action<WidgetRenderContext> render) : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context) => render(context);
    }

    private static (WidgetConfig, IWidget) Fill(SKColor color, int row, int height = 1) =>
        (new WidgetConfig { WidgetTypeId = "t", Col = 0, Row = row, Width = 4, Height = height }, new Probe(c => c.Canvas.Clear(color)));

    private static (WidgetConfig, IWidget?) Docked(SKColor color) =>
        (new WidgetConfig { WidgetTypeId = "t" }, new Probe(c => c.Canvas.Clear(color)));

    private static bool IsActive(SKColor c) => c.Alpha == 255 && c.Red == 255 && c.Green == 255 && c.Blue == 255;

    [Fact]
    public void TwoDockedWidgets_AreTwoCardsInTheBottomBand_UnderTheIndicator()
    {
        var chrome = new PageRenderer.Chrome(IndicatorMode.Always, 1, 2);
        var frame = ChromeLayout.Compute(new Size(1100, 3840), IndicatorMode.Always, 2,
            Dark.Definition.Indicator!.BandHeight!.Value, Dark.Definition.Dock!.Height!.Value);
        var slots = DockLayout.Compute(frame.Dock, 2, Dark.Definition.Card!.Gap!.Value);

        using var bitmap = PageRenderer.RenderToBitmap(
            1100, 3840, [Fill(SKColors.Lime, row: 12)], Dark, DateTime.Now, chrome, [Docked(SKColors.Red), Docked(SKColors.Blue)]);

        // Each card is filled by its widget, from its first pixel row to its last, and is the expected size.
        Assert.Equal(new Size(183, 183), slots[0].Size);
        foreach (var (slot, color) in new[] { (slots[0], SKColors.Red), (slots[1], SKColors.Blue) })
        {
            int centreX = slot.Position.X + slot.Size.Width / 2;
            Assert.Equal(color, bitmap.GetPixel(centreX, slot.Position.Y + slot.Size.Height / 2));
            Assert.Equal(color, bitmap.GetPixel(centreX, slot.Position.Y + 1));
            Assert.Equal(color, bitmap.GetPixel(centreX, slot.Position.Y + slot.Size.Height - 2));
            Assert.Equal(Background, bitmap.GetPixel(centreX, slot.Position.Y - 2));
            Assert.Equal(Background, bitmap.GetPixel(centreX, slot.Position.Y + slot.Size.Height + 1));
        }

        // The band beside the group and the gap between the two cards show the background.
        int y = frame.Dock.Top + frame.Dock.Height / 2;
        Assert.Equal(Background, bitmap.GetPixel(20, y));
        Assert.Equal(Background, bitmap.GetPixel(1080, y));
        Assert.Equal(Background, bitmap.GetPixel(slots[1].CellPosition.X, y));

        // The indicator's pill is in its band, directly above the dock; the thirteenth row is still drawn above that.
        var band = (RectangleF)frame.Indicator;
        Assert.Equal(frame.Dock.Top, frame.Indicator.Bottom);
        Assert.True(IsActive(bitmap.GetPixel(
            (int)PageIndicator.CenterX(band, 2, IndicatorStyle.Resolve(Dark, 275), 1), frame.Indicator.Top + frame.Indicator.Height / 2)));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(550, 12 * 275 + 137));
    }

    [Fact]
    public void ADockedCard_HasTheThemesFractionsOfTheSlot()
    {
        Theme? seen = null;
        SKRect content = default;
        Size size = default;
        float radius = 0;
        float padding = 0;
        float label = 0;
        var probe = new Probe(c =>
        {
            seen = c.Theme;
            content = c.ContentRect;
            size = c.PixelSize;
            radius = c.Theme.CardRadius;
            padding = c.Theme.Padding;
            label = c.Theme.LabelSize;
        });

        using var bitmap = PageRenderer.RenderToBitmap(
            1100, 3840, [], Dark, DateTime.Now, null, [(new WidgetConfig { WidgetTypeId = "t" }, probe)]);

        var card = Dark.Definition.Card!;
        Assert.NotNull(seen);
        Assert.Equal(new Size(183, 183), size);
        Assert.Equal(card.Radius!.Value * 195, radius, 3);
        Assert.Equal(card.Padding!.Value * 195, padding, 3);
        Assert.Equal(Dark.Definition.Typography!.LabelSize!.Value * 195, label, 3);
        Assert.Equal(new SKRect(padding, padding, 183 - padding, 183 - padding), content);

        // The corner is rounded with that radius: its outermost pixel is the background, the card's centre is the fill.
        var slot = DockLayout.Compute(new Rectangle(0, 3645, 1100, 195), 1, card.Gap!.Value).Single();
        Assert.Equal(Background, bitmap.GetPixel(slot.Position.X, slot.Position.Y));
        Assert.Equal(SKColor.Parse(Dark.Definition.Colors!.CardFill), bitmap.GetPixel(slot.Position.X + 91, slot.Position.Y + 91));
    }

    [Fact]
    public void AnEmptySlot_AndAnInvisibleEntry_DrawNothing_ButKeepTheirPlace()
    {
        var hidden = (new WidgetConfig { WidgetTypeId = "t", IsVisible = false }, (IWidget?)new Probe(c => c.Canvas.Clear(SKColors.Lime)));
        var empty = (new WidgetConfig { WidgetTypeId = "gone" }, (IWidget?)null);
        var slots = DockLayout.Compute(new Rectangle(0, 3645, 1100, 195), 3, Dark.Definition.Card!.Gap!.Value);

        using var bitmap = PageRenderer.RenderToBitmap(1100, 3840, [], Dark, DateTime.Now, null, [hidden, empty, Docked(SKColors.Red)]);

        Assert.Equal(Background, bitmap.GetPixel(slots[0].Position.X + 91, slots[0].Position.Y + 91));
        Assert.Equal(Background, bitmap.GetPixel(slots[1].Position.X + 91, slots[1].Position.Y + 91));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(slots[2].Position.X + 91, slots[2].Position.Y + 91));
    }

    [Fact]
    public void APageWidgetThatNoLongerFitsBesideTheDock_IsNotDrawn_AndAnErrorIsLogged()
    {
        // 1000x1000 has four rows; the dock takes the fourth.
        var page = new[] { Fill(SKColors.Lime, row: 3) };

        using var plain = PageRenderer.RenderToBitmap(1000, 1000, page, Dark, DateTime.Now);
        Assert.Equal(SKColors.Lime, plain.GetPixel(125, 800));
        Assert.DoesNotContain("does not fit", Log);

        using var docked = PageRenderer.RenderToBitmap(1000, 1000, page, Dark, DateTime.Now, null, [Docked(SKColors.Red)]);

        Assert.Equal(Background, docked.GetPixel(125, 800));
        Assert.Equal(Background, docked.GetPixel(125, 900));
        Assert.Equal(SKColors.Red, docked.GetPixel(500, 911));
        Assert.DoesNotContain(SKColors.Lime, docked.Pixels);
        Assert.Contains("does not fit", Log);
    }

    [Fact]
    public void WithoutDockEntries_TheImageIsWhatItWas()
    {
        var chrome = new PageRenderer.Chrome(IndicatorMode.Auto, 0, 2);
        var page = new[] { Fill(SKColors.Lime, row: 12), Fill(SKColors.Red, row: 0, height: 2) };

        using var before = PageRenderer.RenderToBitmap(1100, 3840, page, Dark, DateTime.Now, chrome);
        using var empty = PageRenderer.RenderToBitmap(1100, 3840, page, Dark, DateTime.Now, chrome, []);

        Assert.Equal(before.Bytes, empty.Bytes);
    }
}
