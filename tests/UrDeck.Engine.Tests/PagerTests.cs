// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Input;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class PageNavigatorTests
{
    private static readonly string[] Three = ["Home", "Games", "Work"];

    [Fact]
    public void StartsOnTheActivePage()
    {
        var nav = new PageNavigator(Three, 1);

        Assert.Equal(1, nav.Index);
        Assert.Equal("Games", nav.Name);
        Assert.Equal(3, nav.Count);
    }

    [Theory]
    [InlineData(-4, 0)]
    [InlineData(9, 2)]
    public void ClampsTheStartPage(int active, int expected) =>
        Assert.Equal(expected, new PageNavigator(Three, active).Index);

    [Fact]
    public void Settle_MovesToThePageAndClamps()
    {
        var nav = new PageNavigator(Three, 0);

        nav.Settle(2);
        Assert.Equal("Work", nav.Name);
        nav.Settle(7);
        Assert.Equal(2, nav.Index);
    }

    [Fact]
    public void Reload_KeepsThePageByName_EvenWhenItMoved()
    {
        var nav = new PageNavigator(Three, 1);

        nav.Reload(["Work", "Home", "Games"]);

        Assert.Equal(2, nav.Index);
        Assert.Equal("Games", nav.Name);
    }

    [Fact]
    public void Reload_WithRepeatedNames_UsesTheFirst()
    {
        var nav = new PageNavigator(["A", "B", "B"], 2);

        nav.Reload(["A", "B", "B"]);

        Assert.Equal(1, nav.Index);
    }

    [Fact]
    public void Reload_WhenThePageWasRemoved_KeepsTheIndexClamped()
    {
        var nav = new PageNavigator(Three, 2);

        nav.Reload(["Home", "Games"]);

        Assert.Equal(1, nav.Index);
    }

    [Fact]
    public void SinglePage_AndEmptyList_AreSafe()
    {
        var one = new PageNavigator(["Only"], 3);
        Assert.Equal(0, one.Index);

        var none = new PageNavigator([], 0);
        Assert.Equal(0, none.Index);
        Assert.Equal("", none.Name);
        none.Settle(4);
        none.Reload(["A"]);
        Assert.Equal(0, none.Index);
    }
}

public class ChromeLayoutTests
{
    private static readonly Size Panel = new(1100, 3840);

    [Fact]
    public void Panel_WithTheBand_KeepsThirteenRows()
    {
        var always = ChromeLayout.Compute(Panel, IndicatorMode.Always, 2, 0.25);
        var off = ChromeLayout.Compute(Panel, IndicatorMode.Off, 2, 0.25);

        Assert.Equal(13, always.Rows);
        Assert.Equal(13, off.Rows);
        Assert.Equal(new Rectangle(0, 0, 1100, 3771), always.Grid);
        Assert.Equal(new Rectangle(0, 3771, 1100, 69), always.Indicator);
        Assert.Equal(new Rectangle(0, 0, 1100, 3840), off.Grid);
        Assert.True(always.ReservesBand);
    }

    [Fact]
    public void SquareScreen_BandCostsARow()
    {
        var r = ChromeLayout.Compute(new Size(1000, 1000), IndicatorMode.Always, 2, 0.24);

        Assert.Equal(940, r.Grid.Height);
        Assert.Equal(3, r.Rows);
    }

    [Fact]
    public void Fade_ReservesNothing_ButHasARectangle()
    {
        var r = ChromeLayout.Compute(Panel, IndicatorMode.Fade, 2, 0.25);

        Assert.Equal(IndicatorMode.Fade, r.Mode);
        Assert.False(r.ReservesBand);
        Assert.Equal(new Rectangle(0, 0, 1100, 3840), r.Grid);
        Assert.Equal(new Rectangle(0, 3771, 1100, 69), r.Indicator);
    }

    [Fact]
    public void Off_ShowsNothing()
    {
        var r = ChromeLayout.Compute(Panel, IndicatorMode.Off, 5, 0.25);

        Assert.Equal(IndicatorMode.Off, r.Mode);
        Assert.Equal(Rectangle.Empty, r.Indicator);
    }

    [Fact]
    public void Auto_WithOnePage_IsOff()
    {
        var r = ChromeLayout.Compute(Panel, IndicatorMode.Auto, 1, 0.25);

        Assert.Equal(IndicatorMode.Off, r.Mode);
        Assert.Equal(3840, r.Grid.Height);
    }

    [Fact]
    public void Auto_WithSeveralPages_OnThePanel_IsAlways() =>
        Assert.Equal(IndicatorMode.Always, ChromeLayout.Compute(Panel, IndicatorMode.Auto, 2, 0.25).Mode);

    [Fact]
    public void Auto_ReservesWhenFourRowsRemain_AndFloatsWhenOnlyThreeWould()
    {
        // 1000 wide: a cell is 250. Height 1060 with a 60 px band leaves 1000 = 4 rows.
        var four = ChromeLayout.Compute(new Size(1000, 1060), IndicatorMode.Auto, 2, 0.24);
        // Height 1000 leaves 940 = 3 rows.
        var three = ChromeLayout.Compute(new Size(1000, 1000), IndicatorMode.Auto, 2, 0.24);

        Assert.Equal(IndicatorMode.Always, four.Mode);
        Assert.Equal(4, four.Rows);
        Assert.Equal(IndicatorMode.Fade, three.Mode);
        Assert.Equal(4, three.Rows);
        Assert.Equal(1000, three.Grid.Height);
    }
}

public class GridBandTests
{
    private static List<WidgetConfig> One(int row, int height, int col = 0, int width = 4) =>
        [new WidgetConfig { WidgetTypeId = "t", Col = col, Row = row, Width = width, Height = height }];

    private static List<WidgetLayoutItem> Layout(List<WidgetConfig> widgets, double? gridHeight)
    {
        var screen = new Size(1100, 3840);
        return new GridLayoutManager(1100, 3840, 0, gridHeight).RenderWidgetLayout(widgets, screen);
    }

    [Fact]
    public void WidgetEndingBelowTheGrid_IsNotPlaced_AndAnErrorIsLogged()
    {
        string log = Path.Combine(Path.GetTempPath(), "urdeck-grid-" + Guid.NewGuid().ToString("N") + ".log");
        string before = UrDeckLog.LogPath;
        UrDeckLog.LogPath = log;
        try
        {
            var items = Layout(One(row: 12, height: 2), 3771);

            Assert.False(items.Single().Placed);
            Assert.Contains("does not fit", File.ReadAllText(log));
        }
        finally
        {
            UrDeckLog.LogPath = before;
            File.Delete(log);
        }
    }

    [Fact]
    public void WidgetFittingExactly_IsPlaced()
    {
        // 13 rows of 275 end at 3575; a grid exactly that tall holds a widget on row 12.
        Assert.True(Layout(One(row: 12, height: 1), 3575).Single().Placed);
        Assert.False(Layout(One(row: 12, height: 1), 3574).Single().Placed);
    }

    [Fact]
    public void RowsZeroToSeven_AreUnchangedWithTheBand()
    {
        var widgets = new List<WidgetConfig>
        {
            new() { WidgetTypeId = "a", Row = 0, Height = 2 },
            new() { WidgetTypeId = "b", Row = 2, Height = 4 },
            new() { WidgetTypeId = "c", Row = 6, Height = 2 },
        };

        var plain = Layout(widgets, null);
        var banded = Layout(widgets, 3771);

        Assert.All(banded, i => Assert.True(i.Placed));
        Assert.Equal(plain.Select(i => (i.Position, i.Size)), banded.Select(i => (i.Position, i.Size)));
    }

    [Fact]
    public void TheResultStaysAlignedWithTheWidgets()
    {
        var widgets = new List<WidgetConfig>
        {
            new() { WidgetTypeId = "a", Row = 0, Height = 1 },
            new() { WidgetTypeId = "b", Row = 40, Height = 1 },
            new() { WidgetTypeId = "c", Row = 1, Height = 1 },
        };

        var items = Layout(widgets, null);

        Assert.Equal(["a", "b", "c"], items.Select(i => i.WidgetTypeId));
        Assert.Equal([true, false, true], items.Select(i => i.Placed));
    }
}

public class GestureRecognizerTests
{
    private const double Cell = 275; // slop is 11 px
    private readonly List<GestureEvent> _events = new();
    private readonly GestureRecognizer _g;

    public GestureRecognizerTests()
    {
        _g = new GestureRecognizer(Cell);
        _g.Raised += _events.Add;
    }

    private void Send(int id, PointerPhase phase, double x, double y, long t) =>
        _g.Process(new PointerSample(id, phase, x, y, t));

    private IEnumerable<GestureKind> Kinds => _events.Select(e => e.Kind);

    [Fact]
    public void QuickPressAndRelease_IsATap()
    {
        Send(1, PointerPhase.Down, 100, 200, 0);
        Send(1, PointerPhase.Move, 102, 201, 30);
        Send(1, PointerPhase.Up, 102, 201, 90);

        Assert.Equal([GestureKind.Pressed, GestureKind.Tap], Kinds);
        Assert.Equal((100, 200), (_events[0].X, _events[0].Y));
        Assert.Equal((102, 201), (_events[1].X, _events[1].Y));
    }

    [Fact]
    public void ThePress_IsRaisedWhenThePointerGoesDown_NotWhenTheTapIsRecognised()
    {
        Send(1, PointerPhase.Down, 100, 200, 0);

        var press = Assert.Single(_events);
        Assert.Equal(GestureKind.Pressed, press.Kind);
        Assert.Equal((100, 200), (press.X, press.Y));
    }

    [Fact]
    public void LongPress_IsNotATap_AndThePressEndsOnRelease()
    {
        Send(1, PointerPhase.Down, 100, 200, 0);
        Send(1, PointerPhase.Move, 101, 200, 1500);
        Assert.Equal([GestureKind.Pressed], Kinds);

        Send(1, PointerPhase.Up, 100, 200, 2000);

        Assert.Equal([GestureKind.Pressed, GestureKind.PressCancelled], Kinds);
    }

    [Fact]
    public void ASecondPointer_ChangesNothing_WhileThePressIsPending()
    {
        Send(1, PointerPhase.Down, 100, 200, 0);
        Send(2, PointerPhase.Down, 600, 600, 10);
        Send(2, PointerPhase.Move, 900, 600, 20);
        Send(2, PointerPhase.Up, 900, 600, 30);
        Assert.Equal([GestureKind.Pressed], Kinds);

        Send(1, PointerPhase.Up, 100, 200, 90);

        Assert.Equal([GestureKind.Pressed, GestureKind.Tap], Kinds);
    }

    [Fact]
    public void Abort_RaisesNothing_AndForgetsThePress()
    {
        Send(1, PointerPhase.Down, 100, 200, 0);
        _events.Clear();

        _g.Abort();
        Send(1, PointerPhase.Up, 100, 200, 50);

        Assert.Empty(_events);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void HorizontalDrag_StartsASwipe_FollowsIt_AndEnds(int sign)
    {
        Send(1, PointerPhase.Down, 500, 500, 0);
        Send(1, PointerPhase.Move, 500 + sign * 5, 500, 10);
        Assert.Equal([GestureKind.Pressed], Kinds);

        Send(1, PointerPhase.Move, 500 + sign * 20, 502, 20);
        Send(1, PointerPhase.Move, 500 + sign * 60, 502, 40);
        Send(1, PointerPhase.Up, 500 + sign * 60, 502, 50);

        // The press ends just before the swipe starts, and a swipe never ends in a tap.
        Assert.Equal(
            [GestureKind.Pressed, GestureKind.PressCancelled, GestureKind.SwipeStarted, GestureKind.SwipeMoved, GestureKind.SwipeMoved, GestureKind.SwipeEnded],
            Kinds);
        Assert.Equal(sign * 20, _events[2].Dx);
        Assert.Equal(sign * 60, _events[^1].Dx);
        Assert.Equal(sign, Math.Sign(_events[^1].Velocity));
    }

    [Fact]
    public void VerticalDrag_IsNotASwipe()
    {
        Send(1, PointerPhase.Down, 500, 500, 0);
        Send(1, PointerPhase.Move, 503, 530, 20);
        Send(1, PointerPhase.Move, 560, 600, 40);
        Send(1, PointerPhase.Up, 560, 600, 50);

        Assert.Equal([GestureKind.Pressed, GestureKind.PressCancelled], Kinds);
        Assert.False(_g.IsSwiping);
    }

    [Fact]
    public void ADiagonalDrag_IsDecidedAtTheFirstMovePastTheSlop()
    {
        Send(1, PointerPhase.Down, 500, 500, 0);
        Send(1, PointerPhase.Move, 515, 510, 20); // 15 right, 10 down: horizontal
        Send(1, PointerPhase.Move, 520, 700, 40); // a later vertical turn does not undo it

        Assert.Equal(GestureKind.SwipeStarted, _events[2].Kind);
        Assert.True(_g.IsSwiping);
    }

    [Fact]
    public void AFastFlick_HasAHighVelocity_AndASlowDragALowOne()
    {
        Send(1, PointerPhase.Down, 500, 500, 0);
        Send(1, PointerPhase.Move, 480, 500, 20);
        Send(1, PointerPhase.Move, 440, 500, 40);
        Send(1, PointerPhase.Up, 400, 500, 60);
        double fast = _events[^1].Velocity;

        _events.Clear();
        Send(2, PointerPhase.Down, 500, 500, 1000);
        Send(2, PointerPhase.Move, 480, 500, 1200);
        Send(2, PointerPhase.Move, 470, 500, 1400);
        Send(2, PointerPhase.Up, 460, 500, 1600);
        double slow = _events[^1].Velocity;

        Assert.True(fast < -1000, $"fast was {fast}");
        Assert.True(Math.Abs(slow) < 300, $"slow was {slow}");
    }

    [Fact]
    public void ASecondPointer_IsIgnored_WhileTheFirstIsDown()
    {
        Send(1, PointerPhase.Down, 500, 500, 0);
        Send(1, PointerPhase.Move, 450, 500, 20);
        _events.Clear();

        Send(2, PointerPhase.Down, 100, 100, 25);
        Send(2, PointerPhase.Move, 900, 100, 30);
        Send(2, PointerPhase.Up, 900, 100, 35);
        Assert.Empty(_events);
        Assert.True(_g.IsSwiping);

        Send(1, PointerPhase.Move, 400, 500, 40);
        Assert.Equal(GestureKind.SwipeMoved, Assert.Single(_events).Kind);
        Assert.Equal(-100, _events[0].Dx);
    }

    [Fact]
    public void Cancel_DuringASwipe_RaisesCancelled_AndAllowsANewGesture()
    {
        Send(1, PointerPhase.Down, 500, 500, 0);
        Send(1, PointerPhase.Move, 450, 500, 20);
        _events.Clear();

        Send(1, PointerPhase.Cancel, 450, 500, 30);
        Assert.Equal(GestureKind.Cancelled, Assert.Single(_events).Kind);
        Assert.False(_g.IsSwiping);

        _events.Clear();
        Send(3, PointerPhase.Down, 10, 10, 100);
        Send(3, PointerPhase.Up, 10, 10, 120);
        Assert.Equal([GestureKind.Pressed, GestureKind.Tap], Kinds);
    }

    [Fact]
    public void Cancel_BeforeTheDirectionIsDecided_EndsThePress_AndNothingElse()
    {
        Send(1, PointerPhase.Down, 500, 500, 0);
        Send(1, PointerPhase.Cancel, 500, 500, 10);

        Assert.Equal([GestureKind.Pressed, GestureKind.PressCancelled], Kinds);
    }

    [Fact]
    public void MovesWithoutAPress_AreIgnored()
    {
        Send(1, PointerPhase.Move, 500, 500, 0);
        Send(1, PointerPhase.Up, 500, 500, 10);

        Assert.Empty(_events);
    }
}

public class SwipeMathTests
{
    private const double Width = 1000;

    [Theory]
    [InlineData(-500, 0, true)]   // half the width
    [InlineData(500, 0, true)]
    [InlineData(-499, 0, false)]  // just short and still
    [InlineData(-100, -700, true)]   // a fast flick in the swipe's direction
    [InlineData(-100, 700, false)]    // fast the other way: returns
    [InlineData(-100, -500, false)]   // too slow
    [InlineData(0, -2000, false)]     // no movement is no swipe
    public void ShouldCommit(double dx, double velocity, bool expected) =>
        Assert.Equal(expected, SwipeMath.ShouldCommit(dx, velocity, Width));

    [Fact]
    public void RubberBand_IsZeroAtZero_Monotonic_AndBounded()
    {
        Assert.Equal(0, SwipeMath.RubberBand(0, Width));

        double previous = 0;
        for (double dx = 10; dx <= 20000; dx *= 1.5)
        {
            double moved = SwipeMath.RubberBand(dx, Width);
            Assert.True(moved > previous);
            Assert.True(moved < Width);
            Assert.True(moved < dx);
            previous = moved;
        }
    }

    [Fact]
    public void RubberBand_KeepsTheSign() =>
        Assert.Equal(-SwipeMath.RubberBand(300, Width), SwipeMath.RubberBand(-300, Width));
}

public class PageIndicatorTests
{
    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);
    private static readonly IndicatorStyle Style = IndicatorStyle.Resolve(Dark, 275);
    private static readonly RectangleF Area = new(0, 0, 400, 100);

    private static SKBitmap Draw(int count, double position, float opacity = 1, bool backdrop = false)
    {
        var bitmap = new SKBitmap(400, 100);
        using var canvas = new SKCanvas(bitmap);
        PageIndicator.Paint(canvas, Area, count, position, opacity, backdrop, Style);
        return bitmap;
    }

    private static bool IsActive(SKColor c) => c.Alpha == 255 && c.Red == 255 && c.Green == 255 && c.Blue == 255;

    [Fact]
    public void ThePillSitsOnTheCurrentDot_AndMovesBetweenDots()
    {
        float first = PageIndicator.CenterX(Area, 3, Style, 0);
        float second = PageIndicator.CenterX(Area, 3, Style, 1);
        float middle = (first + second) / 2;

        using var at0 = Draw(3, 0);
        using var half = Draw(3, 0.5);
        using var at1 = Draw(3, 1);

        Assert.True(IsActive(at0.GetPixel((int)first, 50)));
        Assert.False(IsActive(at0.GetPixel((int)second, 50)));
        Assert.True(IsActive(half.GetPixel((int)middle, 50)));
        Assert.True(IsActive(at1.GetPixel((int)second, 50)));
        Assert.False(IsActive(at1.GetPixel((int)first, 50)));
        // The other dots are drawn in the inactive colour.
        Assert.InRange(at1.GetPixel((int)first, 50).Alpha, 1, 254);
    }

    [Fact]
    public void ZeroOpacity_DrawsNothing()
    {
        using var bitmap = Draw(3, 1, opacity: 0);

        Assert.All(bitmap.Pixels, p => Assert.Equal(0, p.Alpha));
    }

    [Fact]
    public void Backdrop_DrawsBehindTheMarks()
    {
        using var plain = Draw(3, 1);
        using var floating = Draw(3, 1, backdrop: true);
        float outside = PageIndicator.CenterX(Area, 3, Style, 2) + Style.PillLength / 2 + 3;

        Assert.Equal(0, plain.GetPixel((int)outside, 50).Alpha);
        Assert.True(floating.GetPixel((int)outside, 50).Alpha > 0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void ATapOnADot_SelectsItsPage(int page, int expected)
    {
        float x = PageIndicator.CenterX(Area, 3, Style, page);

        Assert.Equal(expected, PageIndicator.HitTest(Area, 3, Style, 275, new PointF(x, 50)));
    }

    [Fact]
    public void ATapBesideTheDot_ButInItsSlot_StillSelectsIt()
    {
        float x = PageIndicator.CenterX(Area, 3, Style, 0);
        float slot = Math.Max(PageIndicator.Pitch(Style), 275 / 3f);

        Assert.True(slot > Style.DotSize * 3);
        Assert.Equal(0, PageIndicator.HitTest(Area, 3, Style, 275, new PointF(x - slot / 2 + 1, 50)));
    }

    [Fact]
    public void ATapOutsideTheSlotsOrTheRectangle_SelectsNothing()
    {
        Assert.Equal(-1, PageIndicator.HitTest(Area, 3, Style, 275, new PointF(5, 50)));
        Assert.Equal(-1, PageIndicator.HitTest(Area, 3, Style, 275, new PointF(200, 500)));
        Assert.Equal(-1, PageIndicator.HitTest(Area, 0, Style, 275, new PointF(200, 50)));
    }

    [Fact]
    public void Snapshot_ShowsThePillOnTheMiddlePage_OfThreeInTheBand()
    {
        using var bitmap = PageRenderer.RenderToBitmap(
            1100, 3840, [], Dark, DateTime.Now, new PageRenderer.Chrome(IndicatorMode.Auto, 1, 3));
        var band = ChromeLayout.Compute(new Size(1100, 3840), IndicatorMode.Auto, 3, Dark.Definition.Indicator!.BandHeight!.Value).Indicator;
        float y = band.Top + band.Height / 2f;
        var style = IndicatorStyle.Resolve(Dark, 275);
        var area = (RectangleF)band;

        Assert.True(IsActive(bitmap.GetPixel((int)PageIndicator.CenterX(area, 3, style, 1), (int)y)));
        Assert.False(IsActive(bitmap.GetPixel((int)PageIndicator.CenterX(area, 3, style, 0), (int)y)));
    }

    [Fact]
    public void Snapshot_OfOnePageInAuto_HasNoIndicator()
    {
        using var withChrome = PageRenderer.RenderToBitmap(
            1100, 3840, [], Dark, DateTime.Now, new PageRenderer.Chrome(IndicatorMode.Auto, 0, 1));
        using var without = PageRenderer.RenderToBitmap(1100, 3840, [], Dark, DateTime.Now);

        Assert.Equal(without.Bytes, withChrome.Bytes);
    }
}

public class IndicatorThemeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-indicator-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public IndicatorThemeTests()
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

    private LoadedTheme Load(string name, string? json = null)
    {
        if (json != null)
        {
            string dir = Path.Combine(_root, "themes", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), json);
        }
        return new ThemeStore(Path.Combine(_root, "themes")).Load(name);
    }

    [Fact]
    public void EveryBuiltInTheme_HasTheWholeIndicatorGroup()
    {
        foreach (string name in ThemeStore.BuiltInNames)
        {
            var i = Load(name).Definition.Indicator!;
            Assert.NotNull(i.BandHeight);
            Assert.NotNull(i.DotSize);
            Assert.NotNull(i.PillLength);
            Assert.NotNull(i.Spacing);
            Assert.NotNull(i.Active);
            Assert.NotNull(i.Inactive);
            Assert.NotNull(i.Backdrop);
            Assert.NotNull(IndicatorStyle.Resolve(Load(name), 275));
        }
    }

    [Fact]
    public void ATheme_WithoutIndicatorValues_InheritsThemWithoutAWarning()
    {
        var older = Load("older", """{ "colors": { "accent": "#ff00ff" } }""");
        var dark = Load("default-dark");

        Assert.Equal(dark.Definition.Indicator!.PillLength, older.Definition.Indicator!.PillLength);
        Assert.Equal(dark.Definition.Indicator.Active, older.Definition.Indicator.Active);
        Assert.DoesNotContain("indicator", Log);
    }

    [Fact]
    public void APartialIndicator_OverridesOnlyWhatItSets()
    {
        var mine = Load("mine", """{ "indicator": { "pillLength": 0.3, "active": "#ff0000" } }""");
        var dark = Load("default-dark");

        Assert.Equal(0.3, mine.Definition.Indicator!.PillLength);
        Assert.Equal("#ff0000", mine.Definition.Indicator.Active);
        Assert.Equal(dark.Definition.Indicator!.DotSize, mine.Definition.Indicator.DotSize);
        Assert.Equal(0.3f * 275, IndicatorStyle.Resolve(mine, 275).PillLength, 3);
    }

    [Fact]
    public void InvalidIndicatorValues_FallBackWithAWarning()
    {
        var odd = Load("odd", """{ "indicator": { "dotSize": 12, "active": "red" } }""");
        var dark = Load("default-dark");

        Assert.Equal(dark.Definition.Indicator!.DotSize, odd.Definition.Indicator!.DotSize);
        Assert.Equal(dark.Definition.Indicator.Active, odd.Definition.Indicator.Active);
        Assert.Contains("indicator.dotSize", Log);
        Assert.Contains("indicator.active", Log);
    }
}

public class PagerConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-pager-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public PagerConfigTests()
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

    private ConfigStore Store(string? json)
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        if (json != null)
            File.WriteAllText(path, json);
        return new ConfigStore(path);
    }

    [Fact]
    public void Absent_MeansAuto_AndAnOlderFileLoads()
    {
        using var store = Store("""{ "pages": [ { "name": "A", "widgets": [] } ], "activePage": 0 }""");

        Assert.Equal(IndicatorMode.Auto, store.Config.Pager.GetIndicatorMode());
    }

    [Theory]
    [InlineData("always", IndicatorMode.Always)]
    [InlineData("fade", IndicatorMode.Fade)]
    [InlineData("off", IndicatorMode.Off)]
    [InlineData("auto", IndicatorMode.Auto)]
    [InlineData("ALWAYS", IndicatorMode.Always)]
    public void KnownValues(string value, IndicatorMode expected)
    {
        using var store = Store($$"""{ "pager": { "indicator": "{{value}}" } }""");

        Assert.Equal(expected, store.Config.Pager.GetIndicatorMode());
    }

    [Fact]
    public void AnUnknownValue_IsAuto_WithAWarning()
    {
        using var store = Store("""{ "pager": { "indicator": "sometimes" } }""");

        Assert.Equal(IndicatorMode.Auto, store.Config.Pager.GetIndicatorMode());
        Assert.Contains("pager.indicator 'sometimes'", File.ReadAllText(UrDeckLog.LogPath));
    }

    [Fact]
    public void TheSetting_RoundTripsThroughSave()
    {
        using (var store = Store("""{ "pager": { "indicator": "fade" } }"""))
            store.Save();

        using var again = Store(null);

        Assert.Equal(IndicatorMode.Fade, again.Config.Pager.GetIndicatorMode());
    }
}

public class PageSelectionTests
{
    [Theory]
    [InlineData(3, 1, null, 1)]
    [InlineData(3, 9, null, 2)]
    [InlineData(3, 1, 0, 0)]
    [InlineData(3, 1, 2, 2)]
    public void ChoosesAPage(int count, int active, int? requested, int expected)
    {
        Assert.True(PageSelection.TryChoose(count, active, requested, out int index, out string? error));
        Assert.Equal(expected, index);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(3, 5)]
    [InlineData(3, -1)]
    [InlineData(3, 3)]
    public void APageOutsideTheList_Fails_WithAReason(int count, int requested)
    {
        Assert.False(PageSelection.TryChoose(count, 0, requested, out _, out string? error));
        Assert.Contains($"Page {requested}", error);
    }
}
