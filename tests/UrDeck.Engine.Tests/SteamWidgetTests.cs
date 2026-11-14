// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Icons;
using UrDeck.Sdk.Input;
using UrDeck.Sdk.Launch;
using UrDeck.Widgets.Steam;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>widget-steam: what the Steam widget draws and does with taps, over fake readings of the Steam provider.</summary>
[Collection("TextLayoutCache")]
public sealed class SteamWidgetTests : IDisposable
{
    private sealed class FakeSource : IReadingSource
    {
        public Dictionary<string, Reading> Readings { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Reading Read(string id) => Readings.TryGetValue(id, out var r) ? r : Reading.Unavailable("unknown");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;
    }

    private sealed class Host(IReadingSource readings) : IWidgetHost, ILauncher, IIconSource
    {
        public List<string> Launched { get; } = [];

        public List<string> Released { get; } = [];

        public IReadingSource Readings { get; } = readings;

        public ILauncher Launcher => this;

        public IIconSource Icons => this;

        public bool Launch(string target, string? arguments = null)
        {
            Launched.Add(target);
            return true;
        }

        public IconResult GetIcon(string source, int pixelSize) => IconResult.Loading;

        public void Release(string source) => Released.Add(source);
    }

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly FakeSource _source = new();

    public SteamWidgetTests() => Connected();

    public void Dispose() => _theme.Dispose();

    private static ReadingList Friends(params (string Name, string Game, string State)[] rows) => new(rows.Select((r, i) =>
    {
        string id = (76561190000000001 + i).ToString(CultureInfo.InvariantCulture);
        var fields = new List<KeyValuePair<string, ReadingValue>>
        {
            new("name", r.Name), new("state", r.State), new("avatar", ""), new("link", "steam://url/SteamIDPage/" + id),
        };
        if (r.Game.Length > 0)
            fields.Add(new("game", r.Game));
        return new ReadingRow(id, fields);
    }));

    private static ReadingList Games(int count) => new(Enumerable.Range(1, count).Select(i =>
    {
        string id = (900000 + i).ToString(CultureInfo.InvariantCulture);
        return new ReadingRow(id,
        [
            new("name", "Game " + id), new("hours2wks", 1.5 * i), new("art", ""), new("link", "steam://nav/games/details/" + id),
        ]);
    }));

    /// <summary>A connected Steam: me playing a game, three friends online of whom two play, two recent games.</summary>
    private void Connected()
    {
        var r = _source.Readings;
        r["steam:client/running"] = Reading.Ok(true);
        r["steam:me/name"] = Reading.Ok("Juniper");
        r["steam:me/state"] = Reading.Ok("Online");
        r["steam:me/avatar"] = Reading.Ok("");
        r["steam:me/playing"] = Reading.Ok(true);
        r["steam:me/game"] = Reading.Ok("Starfall Odyssey");
        r["steam:me/game/art"] = Reading.Ok("");
        var online = Friends(("Marlowe", "Harbor Lights", "Online"), ("Okoye", "Tilt Garden", "Busy"), ("Bram", "", "Away"));
        r["steam:friends/online"] = Reading.Ok(ReadingValue.FromList(online));
        r["steam:friends/playing"] = Reading.Ok(ReadingValue.FromList(new ReadingList(online.Rows.Take(2))));
        r["steam:games/recent"] = Reading.Ok(ReadingValue.FromList(Games(2)));
        r["steam:download/name"] = Reading.Unavailable("Nothing downloading");
        r["steam:download/progress"] = Reading.Unavailable("Nothing downloading");
    }

    private (SteamWidget Widget, Host Host) Widget(int width, int height, Action<SteamConfig>? configure = null)
    {
        var host = new Host(_source);
        var widget = new SteamWidget();
        widget.Attach(host);
        var config = new SteamConfig { WidgetTypeId = "urdeck.widgets.steam", Width = width, Height = height };
        configure?.Invoke(config);
        widget.Configure(config);
        return (widget, host);
    }

    private SKBitmap Paint(SteamWidget widget, int cellsWide, int cellsHigh)
    {
        int w = 275 * cellsWide, h = 275 * cellsHigh;
        var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);
        float pad = _theme.Padding;
        widget.Render(new WidgetRenderContext(
            canvas, new DateTime(2026, 10, 13, 21, 47, 32, DateTimeKind.Local), new System.Drawing.Size(w, h), _theme, new SKRect(pad, pad, w - pad, h - pad), widget.Config, CancellationToken.None));
        return bitmap;
    }

    private static bool SameImage(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    private static bool HasInk(SKBitmap bitmap)
    {
        var first = bitmap.GetPixel(0, 0);
        for (int y = 0; y < bitmap.Height; y += 3)
            for (int x = 0; x < bitmap.Width; x += 3)
                if (bitmap.GetPixel(x, y) != first)
                    return true;
        return false;
    }

    /// <summary>What a tap does at each point of a sweep over the widget: the target it launches, or null when it does nothing.</summary>
    private static List<(SKPoint Point, string? Target)> Sweep(SteamWidget widget, Host host, int width, int height)
    {
        var found = new List<(SKPoint, string?)>();
        var target = (ITapTarget)widget;
        for (int y = 4; y < height; y += 6)
        {
            var point = new SKPoint(width / 2f, y);
            if (!target.CanTap(point))
            {
                found.Add((point, null));
                continue;
            }

            int before = host.Launched.Count;
            target.OnTap(point);
            found.Add((point, host.Launched.Count > before ? host.Launched[^1] : null));
        }

        return found;
    }

    [Fact]
    public void ItSubscribesToTheCardAndTheFriends_AndToDownloadsAndRecentGamesOnlyWhenTheyAreShown()
    {
        var (card, _) = Widget(2, 1);
        Assert.Contains("steam:me/game", card.Subscriptions);
        Assert.Contains("steam:friends/online", card.Subscriptions);
        Assert.Contains("steam:download/progress", card.Subscriptions);
        Assert.DoesNotContain("steam:games/recent", card.Subscriptions);

        var (large, _) = Widget(4, 4);
        Assert.Contains("steam:games/recent", large.Subscriptions);

        var (plain, _) = Widget(4, 4, c =>
        {
            c.ShowDownloads = false;
            c.ShowRecent = false;
        });
        Assert.DoesNotContain("steam:download/progress", plain.Subscriptions);
        Assert.DoesNotContain("steam:games/recent", plain.Subscriptions);
    }

    [Fact]
    public void ItDeclaresItsThreeSettings_WithTheirDefaults()
    {
        var schema = SettingSchema.Read(typeof(SteamConfig), typeof(WidgetConfig));
        string[] keys = schema.Settings.Select(s => s.Key).ToArray();

        Assert.Equal(["friends", "showDownloads", "showRecent"], keys);
        var config = new SteamConfig();
        Assert.Equal("online", config.Friends);
        Assert.True(config.ShowDownloads);
        Assert.True(config.ShowRecent);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 2)]
    [InlineData(4, 4)]
    public void ItDrawsSomethingAtEverySize(int width, int height)
    {
        var (widget, _) = Widget(width, height);

        using var bitmap = Paint(widget, width, height);

        Assert.True(HasInk(bitmap));
    }

    [Fact]
    public void ABiggerCardShowsMoreOfTheFriendsThanASmallerOne()
    {
        var (small, _) = Widget(2, 2);
        var (large, _) = Widget(4, 4, c => c.ShowRecent = false);

        using var one = Paint(small, 2, 2);
        using var two = Paint(large, 4, 4);

        Assert.True(HasInk(one) && HasInk(two));
    }

    [Fact]
    public void WhenSteamIsNotConnected_TheFriendsAreaSaysSo_AndTheCardStillShowsWhatThePcKnows()
    {
        var (widget, host) = Widget(4, 2);
        using var connected = Paint(widget, 4, 2);

        _source.Readings["steam:friends/online"] = Reading.Unavailable("Connect Steam in Settings");
        _source.Readings["steam:friends/playing"] = Reading.Unavailable("Connect Steam in Settings");
        _source.Readings["steam:me/state"] = Reading.Unavailable("Connect Steam in Settings");
        using var notConnected = Paint(widget, 4, 2);

        Assert.False(SameImage(connected, notConnected));
        Assert.True(HasInk(notConnected));
        // Only the card answers a tap: there are no friends to open.
        string?[] taps = Sweep(widget, host, 1100, 550).Where(t => t.Target != null).Select(t => t.Target).Distinct().ToArray();
        Assert.Equal([SteamWidget.FriendsTarget], taps);
    }

    [Fact]
    public void WhenSteamIsNotRunning_TheCardSaysSo()
    {
        var (widget, _) = Widget(2, 1);
        using var running = Paint(widget, 2, 1);

        _source.Readings["steam:client/running"] = Reading.Ok(false);
        _source.Readings["steam:me/playing"] = Reading.Ok(false);
        _source.Readings["steam:me/game"] = Reading.Ok("");
        using var closed = Paint(widget, 2, 1);

        Assert.False(SameImage(running, closed));
        Assert.True(HasInk(closed));
    }

    [Fact]
    public void WhenSteamIsNotInstalled_TheCardGivesTheReason()
    {
        var (widget, _) = Widget(2, 1);
        using var installed = Paint(widget, 2, 1);

        _source.Readings["steam:client/running"] = Reading.Unavailable("Steam is not installed on this PC");
        using var missing = Paint(widget, 2, 1);

        Assert.False(SameImage(installed, missing));
    }

    [Fact]
    public void ADownloadInProgress_ReplacesTheSecondLineWithItsProgress_WhenShown()
    {
        var (widget, _) = Widget(2, 1);
        using var idle = Paint(widget, 2, 1);

        _source.Readings["steam:download/name"] = Reading.Ok("Ember Court");
        _source.Readings["steam:download/progress"] = Reading.Ok(43);
        using var downloading = Paint(widget, 2, 1);
        Assert.False(SameImage(idle, downloading));

        var (hidden, _) = Widget(2, 1, c => c.ShowDownloads = false);
        using var off = Paint(hidden, 2, 1);
        Assert.True(SameImage(idle, off));
    }

    [Fact]
    public void WithNobodyOnline_TheListShowsAnEmptyLine_NotRows()
    {
        var (widget, host) = Widget(4, 2);
        _source.Readings["steam:friends/online"] = Reading.Ok(ReadingValue.FromList(ReadingList.Empty));
        _source.Readings["steam:friends/playing"] = Reading.Ok(ReadingValue.FromList(ReadingList.Empty));

        using var bitmap = Paint(widget, 4, 2);

        Assert.True(HasInk(bitmap));
        Assert.Equal([SteamWidget.FriendsTarget], Sweep(widget, host, 1100, 550).Where(t => t.Target != null).Select(t => t.Target).Distinct().ToArray());
    }

    [Fact]
    public void AStaleList_IsDrawnFainterThanAFreshOne()
    {
        var (widget, _) = Widget(2, 2);
        using var fresh = Paint(widget, 2, 2);

        var online = _source.Read("steam:friends/online").Value!.Value;
        _source.Readings["steam:friends/online"] = Reading.Stale(online);
        using var stale = Paint(widget, 2, 2);

        Assert.False(SameImage(fresh, stale));
    }

    [Fact]
    public void ATapOnTheCardOpensTheFriendsList_AndATapOnAFriendOpensThatProfile()
    {
        var (widget, host) = Widget(4, 2);
        using var bitmap = Paint(widget, 4, 2);

        string[] targets = Sweep(widget, host, 1100, 550).Where(t => t.Target != null).Select(t => t.Target!).Distinct().ToArray();

        Assert.Contains(SteamWidget.FriendsTarget, targets);
        Assert.Contains("steam://url/SteamIDPage/76561190000000001", targets);
        Assert.All(targets, t => Assert.True(t == SteamWidget.FriendsTarget || t.StartsWith("steam://url/SteamIDPage/", StringComparison.Ordinal)));
        // The card is the top of the widget and the friends are below it.
        var sweep = Sweep(widget, host, 1100, 550);
        Assert.Equal(SteamWidget.FriendsTarget, sweep.First(t => t.Target != null).Target);
    }

    [Fact]
    public void ATapOnARecentGame_OpensItInTheLibrary()
    {
        var (widget, host) = Widget(4, 4);
        using var bitmap = Paint(widget, 4, 4);

        string[] targets = Sweep(widget, host, 1100, 1100).Where(t => t.Target != null).Select(t => t.Target!).Distinct().ToArray();

        Assert.Contains("steam://nav/games/details/900001", targets);
        Assert.Contains("steam://url/SteamIDPage/76561190000000001", targets);
    }

    [Fact]
    public void WithTheFriendsSettingOnPlaying_OnlyThosePlayingAreShown()
    {
        var (widget, host) = Widget(4, 4, c =>
        {
            c.Friends = "playing";
            c.ShowRecent = false;
        });
        using var bitmap = Paint(widget, 4, 4);

        string[] profiles = Sweep(widget, host, 1100, 1100).Where(t => t.Target?.Contains("SteamIDPage", StringComparison.Ordinal) == true)
            .Select(t => t.Target!).Distinct().Order().ToArray();

        Assert.Equal(["steam://url/SteamIDPage/76561190000000001", "steam://url/SteamIDPage/76561190000000002"], profiles);
    }

    [Fact]
    public void ANoTapIsDoneAtOneByOneButTheCardOpensTheFriendsList()
    {
        var (widget, host) = Widget(1, 1);
        using var bitmap = Paint(widget, 1, 1);

        string?[] taps = Sweep(widget, host, 275, 275).Where(t => t.Target != null).Select(t => t.Target!).Distinct().ToArray();

        Assert.Equal([SteamWidget.FriendsTarget], taps);
    }

    [Fact]
    public void TheCardLetsGoOfThePictureItStopsShowing()
    {
        _source.Readings["steam:me/game/art"] = Reading.Ok(@"C:\art\one.png");
        var (widget, host) = Widget(2, 1);
        using var first = Paint(widget, 2, 1);

        _source.Readings["steam:me/playing"] = Reading.Ok(false);
        using var second = Paint(widget, 2, 1);

        Assert.Equal([@"C:\art\one.png"], host.Released);
    }

    [Fact]
    public void ItsPageDoesNotNameAFontOrProvideACard()
    {
        var attribute = (WidgetAttribute)Attribute.GetCustomAttribute(typeof(SteamWidget), typeof(WidgetAttribute))!;

        Assert.Equal("urdeck.widgets.steam", attribute.Id);
        Assert.Equal(["1x1", "2x1", "2x2", "4x2", "4x4"], WidgetSizes());

        static string[] WidgetSizes() => Attribute.GetCustomAttributes(typeof(SteamWidget), typeof(WidgetSizeAttribute))
            .Cast<WidgetSizeAttribute>().Select(s => $"{s.Width}x{s.Height}").Order(StringComparer.Ordinal).ToArray();
    }
}
