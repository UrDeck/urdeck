// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Icons;
using UrDeck.Widgets.NowPlaying;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The now-playing widget (widget-now-playing spec) over fake readings, icons and actions.</summary>
public sealed class NowPlayingWidgetTests : IDisposable
{
    private static readonly System.Drawing.Size Card = new(1100, 550);
    private static readonly DateTime Noon = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Local);

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly Host _host = new();

    public NowPlayingWidgetTests() => _host.Play();

    public void Dispose() => _theme.Dispose();

    /// <summary>The services the widget uses, recorded: the readings it reads, the icons it asked for and let go of, the actions it invoked.</summary>
    private sealed class Host : IWidgetHost, IReadingSource, IIconSource, IActionInvoker
    {
        public Dictionary<string, Reading> Values { get; } = [];

        public List<string> IconRequests { get; } = [];

        public List<string> Released { get; } = [];

        public List<string> Invoked { get; } = [];

        public IReadingSource Readings => this;

        public IIconSource Icons => this;

        public IActionInvoker Actions => this;

        public bool RegionUsesFahrenheit => false;

        public Reading Read(string id) => Values.TryGetValue(id, out var reading) ? reading : Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public IconResult GetIcon(string source, int pixelSize)
        {
            IconRequests.Add(source);
            return IconResult.Loading;
        }

        void IIconSource.Release(string source) => Released.Add(source);

        public Task<ActionResult> Invoke(string id, ReadingValue? argument = null)
        {
            Invoked.Add(id);
            return Task.FromResult(ActionResult.Done);
        }

        public void Set(string path, ReadingValue value) => Values["media:" + path] = Reading.Ok(value);

        public void Remove(string path) => Values["media:" + path] = Reading.Unavailable("test");

        /// <summary>A track thirty seconds into a hundred-second length, reported at <see cref="Noon"/>.</summary>
        public void Play(string status = "playing", string title = "Midnight Drive", string art = "")
        {
            Set("status", status);
            Set("title", title);
            Set("artist", "The Sample Band");
            Set("album", "Demo Tracks");
            Set("app", "Spotify");
            Set("position", 30);
            Set("position/at", new DateTimeOffset(Noon));
            Set("duration", 100);
            Set("art", art);
            Set("can/previous", true);
            Set("can/next", true);
            Set("can/playpause", true);
        }

        public void Idle()
        {
            Values.Clear();
            Set("status", "none");
        }
    }

    private NowPlayingWidget Create(string json = """{ "typeId": "urdeck.widgets.nowplaying" }""")
    {
        var read = JsonSerializer.Deserialize<WidgetConfig>(json, UrDeckJson.Options)!;
        var widget = new NowPlayingWidget();
        widget.Attach(_host);
        widget.Configure(read.ToConcrete(typeof(NowPlayingConfig)));
        return widget;
    }

    private SKBitmap Paint(NowPlayingWidget widget, DateTime? at = null)
    {
        var bitmap = new SKBitmap(Card.Width, Card.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        WidgetPainter.Paint(widget, canvas, Card, _theme, at ?? Noon, CancellationToken.None);
        return bitmap;
    }

    /// <summary>The filled part of the progress bar: the accent-coloured pixels in the rows the bar occupies.</summary>
    private int BarFill(SKBitmap bitmap)
    {
        int count = 0;
        for (int y = (int)(Card.Height * 0.545); y < (int)(Card.Height * 0.62); y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) == _theme.Accent)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Where a tap does something, left to right: along the row with the most tappable points, the runs of x that invoke one
    /// action. The buttons' hit areas touch, so the zones are told apart by what a tap invokes, not by gaps in the range.
    /// </summary>
    private List<(int From, int To)> TapZones(NowPlayingWidget widget, out int row)
    {
        row = -1;
        int most = 0;
        for (int y = 0; y < Card.Height; y += 4)
        {
            int count = 0;
            for (int x = 0; x < Card.Width; x += 4)
            {
                if (widget.CanTap(new SKPoint(x, y)))
                    count++;
            }

            if (count > most)
            {
                most = count;
                row = y;
            }
        }

        var zones = new List<(int From, int To)>();
        if (row < 0)
            return zones;

        string? current = null;
        int from = 0;
        for (int x = 0; x < Card.Width; x += 2)
        {
            string? action = null;
            if (widget.CanTap(new SKPoint(x, row)))
            {
                widget.OnTap(new SKPoint(x, row));
                action = _host.Invoked[^1];
            }

            if (action != current)
            {
                if (current != null)
                    zones.Add((from, x));
                from = x;
                current = action;
            }
        }

        if (current != null)
            zones.Add((from, Card.Width));
        _host.Invoked.Clear();
        return zones;
    }

    private static SKPoint Centre((int From, int To) zone, int row) => new((zone.From + zone.To) / 2f, row);

    // Identity and configuration

    [Fact]
    public void TheWidget_IsRegisteredAsAFourByTwoMediaWidget_ThatRefreshesOnData()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(NowPlayingWidget), out string? reason);

        Assert.Null(reason);
        Assert.Equal("urdeck.widgets.nowplaying", descriptor!.Id);
        Assert.Equal([new System.Drawing.Size(4, 2)], descriptor.SupportedSizes);
        Assert.Equal("Media", descriptor.Category);
        Assert.Equal(RefreshStrategy.OnData, descriptor.Refresh);
        Assert.Equal(typeof(NowPlayingConfig), descriptor.ConfigType);
        Assert.All(new NowPlayingWidget().Subscriptions, id => Assert.StartsWith("media:", id, StringComparison.Ordinal));
    }

    [Fact]
    public void ATypeIdAlone_DrawsWithEveryDefault()
    {
        var widget = Create();

        Assert.True(widget.Config.ShowAlbum);
        Assert.False(widget.Config.ShowApp);
        Assert.Equal("Nothing playing", widget.Config.IdleLabel);
        using var bitmap = Paint(widget);
        Assert.Equal(3, TapZones(widget, out _).Count);
    }

    [Fact]
    public void TheSettings_AreReadFromTheWidgetObject_AndAnUnknownPropertySurvivesASave()
    {
        string root = Path.Combine(Path.GetTempPath(), "urdeck-nowplaying-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "urdeck-config.json");
            File.WriteAllText(path, """
                { "pages": [ { "name": "Main", "widgets": [ {
                  "typeId": "urdeck.widgets.nowplaying", "col": 0, "row": 2, "width": 4, "height": 2,
                  "showAlbum": false, "showApp": true, "idleLabel": "Quiet",
                  "futureSetting": { "confirm": true }
                } ] } ] }
                """);
            using var store = new ConfigStore(path);

            var config = (NowPlayingConfig)store.Config.Pages[0].Widgets.Single().ToConcrete(typeof(NowPlayingConfig));
            store.Save();

            Assert.Equal((false, true, "Quiet"), (config.ShowAlbum, config.ShowApp, config.IdleLabel));
            using var saved = JsonDocument.Parse(File.ReadAllText(path));
            var widget = saved.RootElement.GetProperty("pages")[0].GetProperty("widgets")[0];
            Assert.True(widget.GetProperty("futureSetting").GetProperty("confirm").GetBoolean());
            Assert.Equal("Quiet", widget.GetProperty("idleLabel").GetString());
            Assert.False(widget.GetProperty("showAlbum").GetBoolean());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // States

    [Theory]
    [InlineData("playing")]
    [InlineData("paused")]
    [InlineData("stopped")]
    public void ATrack_ShowsThreeButtons_WhateverItsStatus(string status)
    {
        _host.Play(status);
        var widget = Create();
        using var bitmap = Paint(widget);

        Assert.Equal(3, TapZones(widget, out _).Count);
    }

    [Fact]
    public void NothingPlaying_ShowsThePlaceholderAndNoButtons_AndNoTapDoesAnything()
    {
        _host.Idle();
        var widget = Create();
        using var bitmap = Paint(widget);

        Assert.Empty(TapZones(widget, out _));
        for (int x = 0; x < Card.Width; x += 50)
            widget.OnTap(new SKPoint(x, 450));
        Assert.Empty(_host.Invoked);
        Assert.Empty(_host.IconRequests);
    }

    [Fact]
    public void ALongTitle_ShrinksToFit_AndNeverReachesTheCardsEdge()
    {
        _host.Play(title: new string('W', 120));
        var widget = Create();
        using var bitmap = Paint(widget);

        for (int y = 0; y < Card.Height; y++)
        {
            for (int x = Card.Width - 6; x < Card.Width; x++)
                Assert.NotEqual(_theme.Text, bitmap.GetPixel(x, y));
        }
    }

    [Fact]
    public void WithoutALength_NoBarIsDrawn_AndNothingTicks()
    {
        _host.Set("duration", 0);
        var widget = Create();
        using var bitmap = Paint(widget);

        Assert.Equal(0, BarFill(bitmap));
        Assert.Null(widget.NextAnimationAt);
        Assert.False(widget.NeedsRender(Noon.AddSeconds(30)));
    }

    [Fact]
    public void WithoutArt_NoIconIsAskedFor()
    {
        _host.Play(art: "");
        var widget = Create();
        using var bitmap = Paint(widget);

        Assert.Empty(_host.IconRequests);
    }

    [Fact]
    public void AnArtFile_IsAskedForThroughTheIconService()
    {
        _host.Play(art: @"C:\Temp\UrDeck\media-art\run-1\a.jpg");
        var widget = Create();
        using var bitmap = Paint(widget);

        Assert.Equal([@"C:\Temp\UrDeck\media-art\run-1\a.jpg"], _host.IconRequests.Distinct());
    }

    [Fact]
    public void WhenTheTrackChanges_ThePreviousCoverIsReleased_AndTheNewOneAskedFor()
    {
        _host.Play(art: "a.jpg");
        var widget = Create();
        using (Paint(widget))
        {
        }

        _host.Play(title: "Second Song", art: "b.jpg");
        using (Paint(widget))
        {
        }

        Assert.Equal(["a.jpg"], _host.Released);
        Assert.Contains("b.jpg", _host.IconRequests);
    }

    [Fact]
    public void WhenAnArtlessTrackFollows_TheCoverIsReleased_AndNoneIsAskedFor()
    {
        _host.Play(art: "a.jpg");
        var widget = Create();
        using (Paint(widget))
        {
        }

        _host.Play(title: "No Cover", art: "");
        _host.IconRequests.Clear();
        using (Paint(widget))
        {
        }

        Assert.Equal(["a.jpg"], _host.Released);
        Assert.Empty(_host.IconRequests);
    }

    [Fact]
    public void Disposing_ReleasesTheCover_OnceAndOnlyWhenOneWasShown()
    {
        _host.Play(art: "a.jpg");
        var widget = Create();
        using (Paint(widget))
        {
        }

        widget.Dispose();
        widget.Dispose();

        Assert.Equal(["a.jpg"], _host.Released);
    }

    // Progress and repaint

    [Fact]
    public void WhilePlaying_TheBarAdvancesWithTheClock()
    {
        var widget = Create();
        using var first = Paint(widget, Noon);
        using var later = Paint(widget, Noon.AddSeconds(10));
        using var end = Paint(widget, Noon.AddSeconds(500));

        // 30 of 100 seconds at noon, 40 after ten more, and the whole bar well after the end.
        Assert.InRange(BarFill(later) / (double)BarFill(first), 1.2, 1.5);
        Assert.True(BarFill(end) > BarFill(later));
    }

    [Fact]
    public void AStampAheadOfTheClock_AddsNothing()
    {
        _host.Set("position/at", new DateTimeOffset(Noon.AddHours(5)));
        var widget = Create();
        using var now = Paint(widget, Noon);
        using var soon = Paint(widget, Noon.AddMinutes(3));

        Assert.Equal(BarFill(now), BarFill(soon));
    }

    [Fact]
    public void WhilePaused_TheBarHolds_AndTheCardIsNotRepaintedEverySecond()
    {
        _host.Play("paused");
        var widget = Create();
        using var first = Paint(widget, Noon);
        using var later = Paint(widget, Noon.AddSeconds(10));

        Assert.Equal(BarFill(first), BarFill(later));
        Assert.Null(widget.NextAnimationAt);
        Assert.False(widget.NeedsRender(Noon.AddSeconds(60)));
    }

    [Fact]
    public void WhilePlaying_TheCardIsRepaintedOnceASecond_AndOnlyThen()
    {
        var widget = Create();
        using var bitmap = Paint(widget, Noon);

        Assert.False(widget.NeedsRender(Noon.AddMilliseconds(400)));
        Assert.True(widget.NeedsRender(Noon.AddSeconds(1)));
        Assert.Equal(Noon.AddSeconds(1), widget.NextAnimationAt);
    }

    [Fact]
    public void WhenNothingChanges_TheCardIsNotRepainted_NorAfterTheReadingsAreUnchanged()
    {
        _host.Idle();
        var widget = Create();
        using var bitmap = Paint(widget, Noon);

        Assert.False(widget.NeedsRender(Noon.AddSeconds(30)));
        Assert.Null(widget.NextAnimationAt);

        _host.Play();
        Assert.True(widget.NeedsRender(Noon.AddSeconds(31)));
    }

    [Fact]
    public void AChangedReading_AsksForARepaint_ThenSettles()
    {
        var widget = Create();
        using (Paint(widget))
        {
        }

        Assert.False(widget.NeedsRender(Noon));
        _host.Set("title", "Another Song");
        Assert.True(widget.NeedsRender(Noon));
        using (Paint(widget))
        {
        }

        Assert.False(widget.NeedsRender(Noon));
    }

    // Taps

    [Fact]
    public void ATapOnEachButton_InvokesItsAction_Once()
    {
        var widget = Create();
        using var bitmap = Paint(widget);
        var zones = TapZones(widget, out int row);
        Assert.Equal(3, zones.Count);

        foreach (var zone in zones)
            widget.OnTap(Centre(zone, row));

        Assert.Equal(["media:control/previous", "media:control/playpause", "media:control/next"], _host.Invoked);
    }

    [Fact]
    public void ATapInTheGapBetweenButtons_HitsTheNearerOne_NotBoth()
    {
        var widget = Create();
        using var bitmap = Paint(widget);
        var zones = TapZones(widget, out int row);

        // Every point between the first two buttons, the gap included, invokes exactly one action: the nearer button's.
        for (int x = zones[0].From; x < zones[1].To - 2; x += 3)
        {
            int before = _host.Invoked.Count;
            widget.OnTap(new SKPoint(x, row));
            Assert.Equal(before + 1, _host.Invoked.Count);
            Assert.Equal(x < zones[0].To ? "media:control/previous" : "media:control/playpause", _host.Invoked[^1]);
        }
    }

    [Fact]
    public void ATapOnTheArt_TheTextOrBeyondTheButtons_DoesNothing()
    {
        var widget = Create();
        using var bitmap = Paint(widget);
        var zones = TapZones(widget, out int row);
        var points = new[]
        {
            new SKPoint(100, 120),
            new SKPoint(600, 100),
            new SKPoint(zones[0].From - 40, row),
            new SKPoint(zones[2].To + 40, row),
            new SKPoint(Centre(zones[1], row).X, 5),
        };

        foreach (var point in points)
        {
            Assert.False(widget.CanTap(point), point.ToString());
            widget.OnTap(point);
        }

        Assert.Empty(_host.Invoked);
    }

    [Fact]
    public void ACommandThePlayerRefuses_IsDimmed_AndInert()
    {
        var widget = Create();
        using var enabled = Paint(widget);
        _host.Set("can/previous", false);
        using var disabled = Paint(widget);

        var zones = TapZones(widget, out int row);
        Assert.Equal(2, zones.Count);
        widget.OnTap(new SKPoint(enabled.Width * 0.3f, row));
        Assert.Empty(_host.Invoked);

        // The previous button's area differs between the two paints; the next button's does not.
        Assert.True(Differs(enabled, disabled, 0, zones[0].From));
        Assert.False(Differs(enabled, disabled, zones[1].From, Card.Width));
    }

    [Fact]
    public void AFlagThePlayerDoesNotSay_LeavesTheButtonEnabled()
    {
        _host.Remove("can/next");
        var widget = Create();
        using var bitmap = Paint(widget);

        var zones = TapZones(widget, out int row);
        Assert.Equal(3, zones.Count);
        widget.OnTap(Centre(zones[2], row));
        Assert.Equal(["media:control/next"], _host.Invoked);
    }

    [Fact]
    public void ATap_ChangesNothingOnTheCard_UntilTheProviderPublishes()
    {
        var widget = Create();
        using var bitmap = Paint(widget);
        var zones = TapZones(widget, out int row);

        widget.OnTap(Centre(zones[1], row));

        Assert.False(widget.NeedsRender(Noon));
    }

    [Fact]
    public void BeforeTheFirstPaint_NothingIsTappable()
    {
        var widget = Create();

        Assert.False(widget.CanTap(new SKPoint(550, 450)));
    }

    private static bool Differs(SKBitmap a, SKBitmap b, int fromX, int toX)
    {
        for (int y = (int)(Card.Height * 0.62); y < Card.Height; y++)
        {
            for (int x = fromX; x < toX; x++)
            {
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                    return true;
            }
        }

        return false;
    }
}
