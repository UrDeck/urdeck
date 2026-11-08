// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Input;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;
using MotionLevel = UrDeck.Engine.Motion.MotionLevel;

namespace UrDeck.Engine.Tests;

public sealed class MotionGateTests
{
    [Fact]
    public void MayRun_OnlyAtFullMotion_WithTheDisplayOn_AndTheDeckShown_AndSaysWhyItChanged()
    {
        var gate = new MotionGate(MotionLevel.Off);
        var changes = new List<(bool, string)>();
        gate.MayRunChanged += (run, reason) => changes.Add((run, reason));

        gate.SetDeckShown(true);
        Assert.False(gate.MayRun);
        gate.SetLevel(MotionLevel.Full);
        Assert.True(gate.MayRun);
        gate.SetDisplayOn(false);
        gate.SetDisplayOn(true);
        gate.SetDeckShown(false);
        // No change, no event.
        gate.SetDisplayOn(true);

        Assert.Equal(MotionLevel.Full, gate.Level);
        Assert.Equal([(true, "motion level"), (false, "display off"), (true, "display on"), (false, "deck hidden")], changes);
    }
}

public sealed class WidgetSlotTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-slot-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly ManualScheduler _scheduler = new();
    private readonly FakeFrames _frames = new();
    private readonly FakeReadings _readings = new();
    private readonly FakeWidgets _widgets = new();
    private readonly Theme _theme = ThemeResolver.Resolve(DeckRig.Dark, 100);
    private readonly ResolvedCardStyle _style = ResolvedCardStyle.Resolve(DeckRig.Dark, 100);

    public WidgetSlotTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        _theme.Dispose();
        _style.Dispose();
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private (WidgetSlot Slot, IWidget Widget) Make(WidgetConfig config)
    {
        var widget = _widgets.CreateWidget(config, out var services)!;
        var slot = new WidgetSlot(widget, services, _widgets.GetDescriptor(config.WidgetTypeId)!, _theme, new CardLook(_style, 0),
            PressStyle.Resolve(DeckRig.Dark), _scheduler, _frames, _readings);
        return (slot, widget);
    }

    private static WidgetConfig Probe(params (string Key, string Value)[] parameters)
    {
        var config = DeckRig.Probe(0, 0);
        foreach (var (key, value) in parameters)
            config.Parameters[key] = value;
        return config;
    }

    private static void Paint(WidgetSlot slot)
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        slot.Paint(canvas, new System.Drawing.Size(100, 100));
    }

    [Fact]
    public void Readings_AreSubscribedWhileShown_AndReleasedOnHideAndDispose()
    {
        var (slot, _) = Make(Probe(("readings", "cpu.load,CPU.LOAD,gpu.load")));

        Assert.Equal(["cpu.load", "gpu.load"], slot.Subscriptions);
        Assert.False(slot.IsSubscribed);
        slot.Show();
        slot.Show();
        Assert.True(slot.IsSubscribed);
        Assert.Equal(1, _readings.UsersOf("cpu.load"));
        slot.Hide();
        Assert.Equal(0, _readings.UsersOf("cpu.load"));

        slot.Show();
        slot.Dispose();
        Assert.Equal(0, _readings.UsersOf("gpu.load"));
        // Nothing comes back after the end.
        slot.Show();
        Assert.Equal(0, _readings.UsersOf("gpu.load"));
    }

    [Fact]
    public void ADisposedSlot_HoldsNoWidget_AndDisposesIt()
    {
        var (slot, widget) = Make(Probe());
        int paints = 0;
        slot.PaintRequested += () => paints++;

        slot.Dispose();
        slot.OnReadingsChanged();
        Paint(slot);

        Assert.False(slot.HasWidget);
        Assert.True(((ProbeWidget)widget).Disposed);
        Assert.Equal(0, ((ProbeWidget)widget).Renders);
        Assert.Equal(0, paints);
    }

    [Fact]
    public void AWidgetWithARefreshInterval_IsUpdatedOnShow_ThenEveryInterval_UntilHidden()
    {
        var (slot, widget) = Make(new WidgetConfig { WidgetTypeId = "test.tick", Width = 1, Height = 1 });
        var tick = (TickWidget)widget;
        int paints = 0;
        slot.PaintRequested += () => paints++;

        slot.Show();
        Assert.Equal(1, tick.Updates);
        // The first refresh always asks for a paint.
        Assert.Equal(1, paints);
        Assert.True(slot.IsRefreshing);

        _scheduler.Advance(TimeSpan.FromMilliseconds(2500));
        Assert.Equal(3, tick.Updates);

        slot.Hide();
        Assert.False(slot.IsRefreshing);
        _scheduler.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(3, tick.Updates);
    }

    [Fact]
    public void AWidgetThatAnimatesNowAndThen_IsWokenWhenItSays_AndNeverSoonerThan50Ms()
    {
        var (slot, _) = Make(Probe(("wakeMs", "400")));
        int paints = 0;
        slot.PaintRequested += () => paints++;

        Paint(slot);
        Assert.True(slot.IsWakeScheduled);
        _scheduler.Advance(TimeSpan.FromMilliseconds(399));
        Assert.Equal(0, paints);
        _scheduler.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, paints);

        var (soon, _) = Make(Probe(("wakeMs", "5")));
        int soonPaints = 0;
        soon.PaintRequested += () => soonPaints++;
        Paint(soon);
        _scheduler.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(0, soonPaints);
        _scheduler.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, soonPaints);
    }

    [Fact]
    public void AnAnimatingWidget_IsInTheFrameSource_UntilItStops_OrTheSlotGoes()
    {
        var (slot, widget) = Make(Probe());
        var probe = (ProbeWidget)widget;
        int paints = 0;
        slot.PaintRequested += () => paints++;

        probe.Animating = true;
        Paint(slot);
        Assert.True(slot.IsAnimating);
        Assert.Contains(slot, _frames.Clients);
        _frames.Tick();
        Assert.Equal(1, paints);

        probe.Animating = false;
        Paint(slot);
        Assert.False(slot.IsAnimating);
        Assert.Empty(_frames.Clients);

        probe.Animating = true;
        Paint(slot);
        slot.Dispose();
        Assert.Empty(_frames.Clients);
    }

    [Fact]
    public void ATap_ReachesTheWidget_InCardPixels_AndAPressIsShownAndTakenBack()
    {
        var (slot, widget) = Make(new WidgetConfig { WidgetTypeId = "test.tap", Width = 1, Height = 1 });
        var pressed = new List<bool>();
        slot.PressedChanged += pressed.Add;

        Assert.True(slot.CanTap(new SKPoint(5, 5)));
        slot.PressDown();
        slot.PressDown();
        slot.ReleasePress();
        slot.Tap(new SKPoint(10, 20));

        Assert.Equal([new SKPoint(10, 20)], ((TapWidget)widget).Taps);
        Assert.Equal(slot.Press.IsVisible ? [true, false] : [], pressed);

        var (probe, _) = Make(Probe());
        Assert.False(probe.CanTap(new SKPoint(5, 5)));
    }
}

public sealed class DeckSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-session-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public DeckSessionTests()
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

    private static UrDeckConfig TwoPages() => DeckRig.Pages(
        [DeckRig.Probe(0, 0, "a"), DeckRig.Probe(1, 0, "b")],
        [DeckRig.Probe(0, 0, "a")]);

    private static int Count(string text, string part) => text.Split('\n').Count(line => line.Contains(part, StringComparison.Ordinal));

    [Fact]
    public void NothingIsBuilt_UntilThePresenterAsks_ThenThePageIs()
    {
        using var rig = new DeckRig(TwoPages());
        rig.Scheduler.RunPosted();
        Assert.Empty(rig.Presenter.Alive);
        Assert.Null(rig.Session.Layout);
        Assert.True(rig.Session.IsPageHeld);

        rig.Show();

        Assert.Single(rig.Presenter.Alive);
        Assert.Equal(2, rig.Session.Pager.Current!.Count);
        Assert.Contains("Theme 'default-dark' selected", Log);
        Assert.Contains("Pages: 2, showing 'P1'", Log);
        Assert.Equal(2, Count(Log, "Placed 'test.probe'"));
    }

    [Fact]
    public void AConfigReload_RebuildsOnce()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        int builds = rig.Presenter.Builds;
        int reloaded = 0;
        rig.Session.ConfigReloaded += () => reloaded++;

        var next = TwoPages();
        next.Motion = "off";
        rig.Config.Reload(next);
        rig.Scheduler.RunPosted();

        Assert.Equal(1, reloaded);
        Assert.Equal(builds + 1, rig.Presenter.Builds);
        Assert.Equal(2, Count(Log, "Pages: 2"));
    }

    [Fact]
    public void AnUnrelatedReload_KeepsTheBackgroundKey()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        var moved = TwoPages();
        moved.Pages[0].Widgets[1].Col = 2;

        rig.Config.Reload(moved);
        rig.Scheduler.RunPosted();

        Assert.Equal(2, rig.Presenter.BackgroundKeys.Count);
        Assert.Equal(rig.Presenter.BackgroundKeys[0], rig.Presenter.BackgroundKeys[1]);
    }

    [Fact]
    public void AHiddenDeck_HoldsNoSlots_AndBuildsNothingUntilAskedAgain()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        var slots = rig.LiveSlots.ToList();

        rig.Session.SetShown(false);
        rig.Session.ReleasePage();
        rig.Session.ScheduleRebuild(force: true);
        rig.Scheduler.RunPosted();

        Assert.Empty(rig.Session.Pager.LivePages);
        Assert.Empty(rig.Presenter.Alive);
        Assert.All(slots, s => Assert.False(s.HasWidget));
        Assert.Equal(0, rig.Readings.UsersOf("a"));

        rig.Show();
        Assert.Single(rig.Presenter.Alive);
        Assert.Equal(1, rig.Readings.UsersOf("a"));
    }

    [Fact]
    public void APageChange_KeepsOnePageAliveAtRest()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        var first = rig.Session.Pager.Current!;
        var changes = new List<int>();
        rig.Session.PageChanged += changes.Add;

        Assert.True(rig.Session.ShowPage(1));
        Assert.Equal(2, rig.Presenter.Alive.Count);
        Assert.True(rig.Session.Pager.IsSettling);
        rig.Presenter.Settle();

        Assert.Equal([1], changes);
        Assert.Equal(1, rig.Session.Navigator.Index);
        var alive = Assert.Single(rig.Presenter.Alive);
        Assert.NotSame(first, alive);
        Assert.Equal(0, rig.Presenter.Offsets[alive]);
        Assert.All(first.Cards, c => Assert.False(c.Slot.HasWidget));
        Assert.Contains("Page 'P2' (2/2) shown", Log);
        // One reading user left: the second page's widget.
        Assert.Equal(1, rig.Readings.UsersOf("a"));
        Assert.Equal(0, rig.Readings.UsersOf("b"));
    }

    [Fact]
    public void ASwipe_BuildsTheNeighbour_AndACancelledOneDropsIt()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        var session = rig.Session;

        Assert.True(session.AcceptsInput);
        session.Process(new PointerSample(1, PointerPhase.Down, 300, 100, 0));
        session.Process(new PointerSample(1, PointerPhase.Move, 280, 100, 10));
        session.Process(new PointerSample(1, PointerPhase.Move, 260, 100, 20));

        var neighbor = session.Pager.Neighbor;
        Assert.NotNull(neighbor);
        Assert.Equal("P2", neighbor.Name);
        Assert.Equal(2, rig.Presenter.Alive.Count);
        Assert.True(rig.Presenter.Offsets[session.Pager.Current!] < 0);
        Assert.True(rig.Presenter.Offsets[neighbor] > 0);

        // A short slow drag that ends goes back.
        session.Process(new PointerSample(1, PointerPhase.Up, 260, 100, 2000));
        Assert.False(session.AcceptsInput);
        rig.Presenter.Settle();

        Assert.Null(session.Pager.Neighbor);
        Assert.Single(rig.Presenter.Alive);
        Assert.Equal(0, session.Navigator.Index);
        Assert.All(neighbor.Cards, c => Assert.False(c.Slot.HasWidget));
    }

    [Fact]
    public void ATapOnACard_ReachesItsWidget()
    {
        var config = DeckRig.Pages([new WidgetConfig { WidgetTypeId = "test.tap", Col = 1, Row = 1, Width = 1, Height = 1 }]);
        using var rig = new DeckRig(config).Show();
        var card = Assert.Single(rig.Session.Layout!.Page);
        double x = card.Item.Position.X + 10, y = card.Item.Position.Y + 20;

        rig.Session.Process(new PointerSample(1, PointerPhase.Down, x, y, 0));
        Assert.Equal(card.Slot.Press.IsVisible, card.Slot.IsPressed);
        rig.Session.Process(new PointerSample(1, PointerPhase.Up, x, y, 50));

        Assert.False(card.Slot.IsPressed);
        Assert.Equal([new SKPoint(10, 20)], ((TapWidget)rig.Widgets.Created.Single()).Taps);
    }

    [Fact]
    public void AReadingChange_DirtiesOnlyTheSlotsThatDeclaredIt()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        var cards = rig.Session.Pager.Current!.Cards;
        rig.Presenter.ResetPaints();

        rig.Readings.Change("A", "nobody");
        rig.Readings.Change("a");
        rig.Scheduler.RunPosted();

        Assert.Equal(1, rig.Presenter.PaintsOf(cards[0].Slot));
        Assert.Equal(0, rig.Presenter.PaintsOf(cards[1].Slot));
    }

    [Fact]
    public void APluginReload_ReleasesTheSlotsFirst_ThenBuildsNewOnes()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        var old = rig.LiveSlots.ToList();

        rig.Widgets.ReloadPlugins();
        rig.Scheduler.RunPosted();

        Assert.All(old, s => Assert.False(s.HasWidget));
        Assert.All(rig.Widgets.Created.Take(2), w => Assert.True(((ProbeWidget)w).Disposed));
        var now = rig.LiveSlots.ToList();
        Assert.Equal(2, now.Count);
        Assert.All(now, s => Assert.True(s.HasWidget));
        Assert.Empty(now.Intersect(old));
    }

    [Fact]
    public void ADock_IsBuiltBesideThePage_AndStaysOnAPageChange()
    {
        var config = TwoPages();
        config.Dock = [new WidgetConfig { WidgetTypeId = "test.probe", Parameters = { ["readings"] = "dock" } }];
        using var rig = new DeckRig(config).Show();
        var dock = rig.Session.Pager.Dock!;

        rig.Session.ShowPage("p-2");
        rig.Presenter.Settle();

        Assert.Same(dock, rig.Session.Pager.Dock);
        Assert.Single(rig.Session.Layout!.Dock);
        Assert.Equal(1, rig.Readings.UsersOf("dock"));
        Assert.Contains("Dock: 1 slot(s) shown", Log);
    }

    // The second presenter (design D4).

    [Fact]
    public void TwoSessions_OnOneScheduler_HaveTheirOwnPages_AndBothSeeReadingsAndPluginReloads()
    {
        var scheduler = new ManualScheduler();
        var widgets = new FakeWidgets();
        var readings = new FakeReadings();
        var source = new FakeConfigSource(TwoPages());
        using var deck = new DeckRig(source.Config, scheduler, widgets, readings, source).Show();
        using var preview = new DeckRig(source.Config, scheduler, widgets, readings, source).Show();

        preview.Session.ShowPage(1);
        preview.Presenter.Settle();
        Assert.Equal(0, deck.Session.Navigator.Index);
        Assert.Equal(1, preview.Session.Navigator.Index);
        Assert.Equal(2, readings.UsersOf("a"));

        deck.Presenter.ResetPaints();
        preview.Presenter.ResetPaints();
        readings.Change("a");
        scheduler.RunPosted();
        Assert.Equal(1, deck.Presenter.PaintsOf(deck.Session.Pager.Current!.Cards[0].Slot));
        Assert.Equal(1, preview.Presenter.PaintsOf(preview.Session.Pager.Current!.Cards[0].Slot));

        var old = deck.LiveSlots.Concat(preview.LiveSlots).ToList();
        widgets.ReloadPlugins();
        scheduler.RunPosted();
        Assert.All(old, s => Assert.False(s.HasWidget));
        Assert.Equal(3, deck.LiveSlots.Count() + preview.LiveSlots.Count());
        Assert.Equal(1, preview.Session.Navigator.Index);

        // Ending one leaves the other running.
        preview.Session.Dispose();
        readings.Change("b");
        scheduler.RunPosted();
        Assert.Equal(1, readings.UsersOf("a"));
        Assert.Equal(1, readings.UsersOf("b"));
    }

    [Fact]
    public void ASessionWithoutInput_ShowsItsPages_AndChangesPageWhenTold()
    {
        using var rig = new DeckRig(TwoPages());
        var changes = new List<int>();
        rig.Session.PageChanged += changes.Add;

        // Before anything is built the navigator moves at once.
        Assert.True(rig.Session.ShowPage("p-2"));
        Assert.False(rig.Session.ShowPage("p-2"));
        Assert.False(rig.Session.ShowPage("nope"));
        Assert.False(rig.Session.ShowPage(7));
        rig.Show();
        Assert.Equal("P2", rig.Session.Pager.Current!.Name);

        Assert.True(rig.Session.ShowPage(0));
        // Nothing else while it settles.
        Assert.False(rig.Session.ShowPage(1));
        rig.Presenter.Settle();
        Assert.Equal([1, 0], changes);
        Assert.Equal("P1", rig.Session.Pager.Current!.Name);
    }

    [Fact]
    public void TheFramePainter_DrawsTheThemeColour_TheCards_AndTheIndicator()
    {
        var fill = DeckRig.Probe(0, 0);
        fill.Parameters["fill"] = "#ff0000";
        using var rig = new DeckRig(DeckRig.Pages([fill], [DeckRig.Probe(0, 0)])).Show();
        var setup = rig.Session.Pager.Setup!;
        using var bitmap = new SkiaSharp.SKBitmap(setup.Screen.Width, setup.Screen.Height);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        using var painter = new DeckFramePainter();

        painter.Paint(canvas, rig.Session, null, TimeSpan.Zero, animate: false);

        var card = rig.Session.Pager.Current!.Cards[0].Item;
        Assert.Equal(SkiaSharp.SKColors.Red, bitmap.GetPixel(card.Position.X + card.Size.Width / 2, card.Position.Y + card.Size.Height / 2));
        Assert.Equal(SkiaSharp.SKColor.Parse(DeckRig.Dark.Definition.Colors!.Background), bitmap.GetPixel(setup.Screen.Width / 2, setup.Chrome.Grid.Height / 2));
        var band = setup.Chrome.Indicator;
        Assert.NotEqual(UrDeck.Engine.Layout.IndicatorMode.Off, setup.Chrome.Mode);
        Assert.Contains(Enumerable.Range(band.X, band.Width), x => bitmap.GetPixel(x, band.Y + band.Height / 2) != bitmap.GetPixel(1, band.Y + band.Height / 2));
    }

    [Fact]
    public void TheSession_StartsOnTheStartPage_ById()
    {
        var config = TwoPages();
        config.StartPage = "p-2";

        using var rig = new DeckRig(config).Show();

        Assert.Equal("P2", rig.Session.Pager.Current!.Name);
    }

    [Fact]
    public void AReload_KeepsThePageById_AfterARenameAndAReorder()
    {
        using var rig = new DeckRig(TwoPages());
        rig.Session.ShowPage("p-2");
        rig.Show();

        var next = TwoPages();
        next.Pages[1].Name = "Games";
        next.Pages.Reverse();
        rig.Config.Reload(next);
        rig.Scheduler.RunPosted();

        Assert.Equal(0, rig.Session.Navigator.Index);
        Assert.Equal("Games", rig.Session.Pager.Current!.Name);
    }

    [Fact]
    public void JumpToPage_ShowsThePageAtOnce_WithoutASettle()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        var changes = new List<int>();
        rig.Session.PageChanged += changes.Add;

        Assert.True(rig.Session.JumpToPage("p-2"));
        Assert.False(rig.Session.JumpToPage("p-2"));
        Assert.False(rig.Session.JumpToPage("nope"));
        rig.Scheduler.RunPosted();

        Assert.Null(rig.Presenter.PendingSettle);
        Assert.Equal("P2", rig.Session.Pager.Current!.Name);
        Assert.Equal([1], changes);
    }

    [Fact]
    public void TheLayout_IsReadable_AndTheCardsPaintIntoOneCanvas()
    {
        var config = DeckRig.Pages([
            new WidgetConfig { WidgetTypeId = "test.probe", Col = 0, Row = 0, Width = 2, Height = 1, Parameters = { ["fill"] = "#ff0000" } },
            new WidgetConfig { WidgetTypeId = "test.probe", Col = 2, Row = 1, Width = 1, Height = 1, Parameters = { ["fill"] = "#0000ff" } },
        ]);
        using var rig = new DeckRig(config);
        rig.Presenter.Surface = new DeckSurface(new System.Drawing.Size(400, 800), 2);
        rig.Show();

        var layout = rig.Session.Layout!;
        Assert.Equal(new System.Drawing.Size(400, 800), layout.Surface.Size);
        Assert.Equal(2, layout.Page.Count);
        Assert.Empty(layout.Dock);
        var red = layout.Page[0].Item;
        var blue = layout.Page[1].Item;
        // The card lies inside its cell.
        Assert.True(red.CellSize.Width >= red.Size.Width && red.CellPosition.X <= red.Position.X);

        using var bitmap = new SKBitmap(800, 1600);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        rig.Session.PaintCards(canvas, rig.Scheduler.Elapsed);

        Assert.Equal(SKColors.Red, bitmap.GetPixel((red.Position.X + red.Size.Width / 2) * 2, (red.Position.Y + red.Size.Height / 2) * 2));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel((blue.Position.X + blue.Size.Width / 2) * 2, (blue.Position.Y + blue.Size.Height / 2) * 2));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(799, 1599));
    }

    [Fact]
    public void TheSettle_EasesFromWhereThePagesWere_ToWhereTheyRest()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        var first = rig.Session.Pager.Current!;
        rig.Session.ShowPage(1);
        var second = rig.Session.Pager.Neighbor!;
        var start = rig.Scheduler.Elapsed;

        Assert.Equal(0, rig.Session.Pager.OffsetAt(first, start));
        Assert.Equal(400, rig.Session.Pager.OffsetAt(second, start));
        double half = rig.Session.Pager.OffsetAt(first, start + PagerState.SettleTime / 2);
        Assert.InRange(half, -400, -200);
        Assert.Equal(-400, rig.Session.Pager.OffsetAt(first, start + PagerState.SettleTime), 6);
        Assert.Equal(0, rig.Session.Pager.OffsetAt(second, start + PagerState.SettleTime * 2), 6);

        Assert.Equal(0, PagerState.Ease(0));
        Assert.Equal(1, PagerState.Ease(1));
        Assert.True(PagerState.Ease(0.25) > 0.5);
        Assert.True(PagerState.Ease(0.5) < PagerState.Ease(0.6));
    }

    [Fact]
    public void Motion_FollowsWindowsAndTheDisplay_AndADisposedSessionIgnoresReloads()
    {
        using var rig = new DeckRig(TwoPages()).Show();
        Assert.Equal(MotionLevel.Full, rig.Session.Motion.Effective);
        Assert.True(rig.Session.Gate.MayRun);

        rig.Session.SetDisplayOn(false);
        Assert.False(rig.Session.Gate.MayRun);
        rig.Session.SetDisplayOn(true);
        rig.Session.SetWindowsAnimations(false);
        Assert.NotEqual(MotionLevel.Full, rig.Session.Motion.Effective);
        Assert.False(rig.Session.Gate.MayRun);

        rig.Session.Dispose();
        int builds = rig.Presenter.Builds;
        rig.Config.Reload();
        rig.Widgets.ReloadPlugins();
        rig.Readings.Change("a");
        rig.Scheduler.RunPosted();
        Assert.Equal(builds, rig.Presenter.Builds);
        Assert.Empty(rig.Session.Pager.LivePages);
    }
}
