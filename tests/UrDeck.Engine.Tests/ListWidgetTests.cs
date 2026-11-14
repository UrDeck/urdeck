// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Launch;
using UrDeck.Sdk.Settings;
using UrDeck.Widgets.List;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>widget-list: what the list widget draws and does with taps, over fake readings.</summary>
[Collection("TextLayoutCache")]
public sealed class ListWidgetTests : IDisposable
{
    private const string Friends = "steam:friends/playing";

    private sealed class FakeSource : IReadingSource
    {
        public Dictionary<string, Reading> Readings { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ReadingDescriptor> Catalog { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Reading Read(string id) => Readings.TryGetValue(id, out var r) ? r : Reading.Unavailable("unknown");

        public ReadingDescriptor? Describe(string id) => Catalog.TryGetValue(id, out var d) ? d : null;

        public bool RegionUsesFahrenheit => false;
    }

    private sealed class Host(IReadingSource readings) : IWidgetHost, ILauncher
    {
        public List<string> Launched { get; } = [];

        public IReadingSource Readings { get; } = readings;

        public ILauncher Launcher => this;

        public bool Launch(string target, string? arguments = null)
        {
            Launched.Add(target);
            return true;
        }
    }

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly FakeSource _source = new();

    public void Dispose() => _theme.Dispose();

    private static ReadingDescriptor Description(string? emptyText = null, bool link = true) => new("friends/playing", ReadingKind.List, "Friends playing", "Friends playing")
    {
        Unit = "playing",
        EmptyText = emptyText,
        Fields =
        [
            new ReadingField("name", ReadingKind.Text, "Name") { Role = FieldRole.Title },
            new ReadingField("game", ReadingKind.Text, "Game") { Role = FieldRole.Detail },
            .. link ? [new ReadingField("profile", ReadingKind.Text, "Profile") { Role = FieldRole.Link }] : Array.Empty<ReadingField>(),
        ],
    };

    private static ReadingList Rows(int count, bool link = true) => new(Enumerable.Range(0, count).Select(i =>
    {
        string key = i.ToString(CultureInfo.InvariantCulture);
        var fields = new List<KeyValuePair<string, ReadingValue>> { new("name", "Friend " + key), new("game", "Game " + key) };
        if (link)
            fields.Add(new("profile", "steam://url/SteamIDPage/" + key));
        return new ReadingRow(key, fields);
    }));

    private (ListWidget Widget, Host Host) Widget(Reading reading, ReadingDescriptor? description, int width = 2, int height = 2, Action<ListConfig>? configure = null)
    {
        _source.Readings[Friends] = reading;
        if (description != null)
            _source.Catalog[Friends] = description;
        var host = new Host(_source);
        var widget = new ListWidget();
        widget.Attach(host);
        var config = new ListConfig { WidgetTypeId = "urdeck.widgets.list", Reading = Friends, Width = width, Height = height };
        configure?.Invoke(config);
        widget.Configure(config);
        return (widget, host);
    }

    private SKBitmap Paint(ListWidget widget, int cellsWide = 2, int cellsHigh = 2)
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

    /// <summary>The first point down the middle of the widget where a tap does something.</summary>
    private static SKPoint? FirstTappable(ListWidget widget, int height = 550)
    {
        for (int y = 0; y < height; y += 4)
        {
            var point = new SKPoint(120, y);
            if (((UrDeck.Sdk.Input.ITapTarget)widget).CanTap(point))
                return point;
        }

        return null;
    }

    [Fact]
    public void ItSubscribesToItsReading_AndTheDefaultIsTheProcessList()
    {
        var (widget, _) = Widget(Reading.Ok(ReadingValue.FromList(Rows(2))), Description());

        Assert.Equal([Friends], widget.Subscriptions);
        Assert.Equal("system:processes/top", new ListWidget().DefaultConfig.Reading);
    }

    [Fact]
    public void ItsReadingSetting_AcceptsListsOnly()
    {
        var schema = SettingSchema.Read(typeof(ListConfig), typeof(WidgetConfig));

        var reading = schema.Settings.Single(s => s.Key == "reading");
        Assert.Equal(ReadingFilter.ListsOnly, reading.Reading!.Filter);
    }

    [Fact]
    public void ARowsLink_IsOpenedThroughTheLauncher()
    {
        var (widget, host) = Widget(Reading.Ok(ReadingValue.FromList(Rows(4))), Description());
        using var _ = Paint(widget);

        var point = FirstTappable(widget);
        Assert.NotNull(point);
        ((UrDeck.Sdk.Input.ITapTarget)widget).OnTap(point!.Value);

        Assert.Equal(["steam://url/SteamIDPage/0"], host.Launched);
    }

    [Fact]
    public void WithoutALinkField_AListThatFits_HasNothingToTap()
    {
        var (widget, host) = Widget(Reading.Ok(ReadingValue.FromList(Rows(2, link: false))), Description(link: false));
        using var _ = Paint(widget);

        Assert.Null(FirstTappable(widget));
        ((UrDeck.Sdk.Input.ITapTarget)widget).OnTap(new SKPoint(120, 150));
        Assert.Empty(host.Launched);
    }

    [Fact]
    public void InScrollMode_ATapOnTheLowerPartPagesForward_AndTheUpperPartBack()
    {
        var (widget, host) = Widget(Reading.Ok(ReadingValue.FromList(Rows(40, link: false))), Description(link: false));
        using var _ = Paint(widget);
        var tap = (UrDeck.Sdk.Input.ITapTarget)widget;

        Assert.True(tap.CanTap(new SKPoint(120, 500)));
        tap.OnTap(new SKPoint(120, 500));
        using var paged = Paint(widget);
        using var pagedAgain = Paint(widget);
        Assert.True(SameImage(paged, pagedAgain));

        // Back again: the upper part pages to the rows that were drawn first.
        tap.OnTap(new SKPoint(120, 60));
        using var back = Paint(widget);
        Assert.Empty(host.Launched);
        Assert.False(SameImage(paged, back));
    }

    [Fact]
    public void InRotateMode_ATapDoesNothing()
    {
        var (widget, host) = Widget(Reading.Ok(ReadingValue.FromList(Rows(40, link: false))), Description(link: false), configure: c => c.Overflow = "rotate");
        using var _ = Paint(widget);

        Assert.False(((UrDeck.Sdk.Input.ITapTarget)widget).CanTap(new SKPoint(120, 500)));
        Assert.Empty(host.Launched);
    }

    [Fact]
    public void AnEmptyList_ShowsTheDescriptionsText_OrNothingToShow()
    {
        var (nobody, _) = Widget(Reading.Ok(ReadingValue.FromList(ReadingList.Empty)), Description("Nobody is playing"));
        using var withText = Paint(nobody);
        var (plain, _) = Widget(Reading.Ok(ReadingValue.FromList(ReadingList.Empty)), Description());
        using var withoutText = Paint(plain);

        Assert.True(HasInk(withText));
        Assert.False(SameImage(withText, withoutText));
        Assert.Null(FirstTappable(nobody));
    }

    [Fact]
    public void AnUnavailableReading_ShowsItsReason()
    {
        var (a, _) = Widget(Reading.Unavailable("Connect Steam in Settings"), Description());
        using var connect = Paint(a);
        var (b, _) = Widget(Reading.Unavailable("Steam is offline"), Description());
        using var offline = Paint(b);

        Assert.True(HasInk(connect));
        Assert.False(SameImage(connect, offline));
    }

    [Fact]
    public void APendingReading_ShowsPlaceholderRows_ThatAreNotTheRows()
    {
        var (pending, _) = Widget(Reading.Pending(), Description());
        using var placeholders = Paint(pending);
        var (ready, _) = Widget(Reading.Ok(ReadingValue.FromList(Rows(3))), Description());
        using var rows = Paint(ready);

        Assert.True(HasInk(placeholders));
        Assert.False(SameImage(placeholders, rows));
    }

    [Fact]
    public void AStaleList_KeepsItsRows_DrawnFainter()
    {
        var list = ReadingValue.FromList(Rows(3));
        var (ok, _) = Widget(Reading.Ok(list), Description());
        using var current = Paint(ok);
        var (stale, _) = Widget(Reading.Stale(list), Description());
        using var faint = Paint(stale);

        Assert.True(HasInk(faint));
        Assert.False(SameImage(current, faint));
    }

    [Fact]
    public void At1x1_ItShowsTheCount()
    {
        var (three, _) = Widget(Reading.Ok(ReadingValue.FromList(Rows(3))), Description(), 1, 1);
        using var a = Paint(three, 1, 1);
        var (four, _) = Widget(Reading.Ok(ReadingValue.FromList(Rows(4))), Description(), 1, 1);
        using var b = Paint(four, 1, 1);

        Assert.False(SameImage(a, b));
    }

    [Fact]
    public void TheTitleSetting_ReplacesTheLabel_AndEmptyHidesIt()
    {
        var list = Reading.Ok(ReadingValue.FromList(Rows(3)));
        var (own, _) = Widget(list, Description());
        using var withLabel = Paint(own);
        var (named, _) = Widget(list, Description(), configure: c => c.Title = "Online");
        using var withTitle = Paint(named);
        var (hidden, _) = Widget(list, Description(), configure: c => c.Title = "");
        using var withNone = Paint(hidden);

        Assert.False(SameImage(withLabel, withTitle));
        Assert.False(SameImage(withLabel, withNone));
    }

    [Fact]
    public void MaxRows_TakesTheRowsFromTheTop()
    {
        var list = Reading.Ok(ReadingValue.FromList(Rows(6)));
        var (all, _) = Widget(list, Description());
        using var everyone = Paint(all);
        var (two, _) = Widget(list, Description(), configure: c => c.MaxRows = 2);
        using var few = Paint(two);

        Assert.False(SameImage(everyone, few));
    }

    [Fact]
    public void ADescriptionlessList_StillDrawsItsRows()
    {
        var (widget, _) = Widget(Reading.Ok(ReadingValue.FromList(Rows(3))), null);
        using var bitmap = Paint(widget);

        Assert.True(HasInk(bitmap));
    }
}
