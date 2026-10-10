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
using UrDeck.Sdk.Input;
using UrDeck.Sdk.Launch;
using UrDeck.Widgets.Shortcut;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ShortcutWidgetTests : IDisposable
{
    private static readonly System.Drawing.Size Card = new(260, 260);
    private static readonly SKColor Ink = new(0xE0, 0x30, 0x90);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-shortcut-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly Host _host = new();
    private readonly SKImage _icon;

    public ShortcutWidgetTests()
    {
        Directory.CreateDirectory(_root);
        using var bitmap = new SKBitmap(new SKImageInfo(256, 256, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(Ink);
        _icon = SKImage.FromBitmap(bitmap);
    }

    public void Dispose()
    {
        _icon.Dispose();
        _theme.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    /// <summary>The services a shortcut uses, recorded: what it launched, which icons it asked for and what it logged.</summary>
    private sealed class Host : IWidgetHost, ILauncher, IIconSource, IReadingSource
    {
        public List<(string Target, string? Arguments)> Launches { get; } = [];

        public List<(string Source, int Size)> IconRequests { get; } = [];

        public List<string> Logged { get; } = [];

        public bool LaunchSucceeds { get; set; } = true;

        public IconResult Icon { get; set; } = IconResult.Loading;

        public IReadingSource Readings => this;

        public ILauncher Launcher => this;

        public IIconSource Icons => this;

        public void Log(string message) => Logged.Add(message);

        public bool Launch(string target, string? arguments = null)
        {
            Launches.Add((target, arguments));
            return LaunchSucceeds;
        }

        public IconResult GetIcon(string source, int pixelSize)
        {
            IconRequests.Add((source, pixelSize));
            return Icon;
        }

        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;
    }

    /// <summary>A shortcut built the way the host builds one: from the widget object of the page file.</summary>
    private ShortcutWidget Create(string json, bool attach = true)
    {
        var read = JsonSerializer.Deserialize<WidgetConfig>(json, UrDeckJson.Options)!;
        var widget = new ShortcutWidget();
        if (attach)
            widget.Attach(_host);
        widget.Configure(read.ToConcrete(typeof(ShortcutConfig)));
        return widget;
    }

    private SKBitmap Paint(IWidget widget)
    {
        var bitmap = new SKBitmap(Card.Width, Card.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        WidgetPainter.Paint(widget, canvas, Card, _theme, DateTime.Now, CancellationToken.None);
        return bitmap;
    }

    private static int Count(SKBitmap bitmap, Func<SKColor, bool> match)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (match(bitmap.GetPixel(x, y)))
                    count++;
        return count;
    }

    private static bool IsInk(SKColor c) => Math.Abs(c.Red - Ink.Red) < 12 && Math.Abs(c.Green - Ink.Green) < 12 && Math.Abs(c.Blue - Ink.Blue) < 12;

    private bool IsText(SKColor c) => c == _theme.Text;

    private bool IsMuted(SKColor c)
    {
        // The muted text colour is translucent: over the card it is a light grey that is neither the text nor the ink.
        return c.Red > 150 && c.Red < 250 && Math.Abs(c.Red - c.Green) < 12 && Math.Abs(c.Red - c.Blue) < 12;
    }

    private static readonly SKPoint Middle = new(130, 130);

    [Fact]
    public void TheWidget_IsRegisteredAsAOneByOneLaunchWidget_WithoutATimer()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(ShortcutWidget), out string? reason);

        Assert.Null(reason);
        Assert.Equal("urdeck.widgets.shortcut", descriptor!.Id);
        Assert.Equal([new System.Drawing.Size(1, 1)], descriptor.SupportedSizes);
        Assert.Equal("Launch", descriptor.Category);
        Assert.Equal(RefreshStrategy.OnData, descriptor.Refresh);
        Assert.Null(descriptor.RefreshInterval);
        Assert.Equal(typeof(ShortcutConfig), descriptor.ConfigType);
        Assert.Empty(new ShortcutWidget().Subscriptions);
    }

    [Fact]
    public void AMinimalShortcut_ShowsTheIconOfItsTarget_WithNoLabel_AndATapOpensIt()
    {
        var widget = Create("""{ "typeId": "urdeck.widgets.shortcut", "target": "https://example.com" }""");
        _host.Icon = new IconResult(_icon, false);

        using var bitmap = Paint(widget);
        ((ITapTarget)widget).OnTap(Middle);

        var request = Assert.Single(_host.IconRequests);
        Assert.Equal("https://example.com", request.Source);
        // The wanted size is the shorter side of the content rectangle: the card less the theme's padding.
        var content = WidgetPainter.ContentRect(Card, _theme);
        Assert.Equal((int)Math.Ceiling(content.Width), request.Size);
        Assert.True(IsInk(bitmap.GetPixel(130, 130)));
        Assert.Equal(0, Count(bitmap, IsMuted));
        Assert.Equal([("https://example.com", null)], _host.Launches);
        Assert.Empty(_host.Logged);
    }

    [Fact]
    public void TheIcon_IsFittedToTheContentRectangle_AndCentred()
    {
        var widget = Create("""{ "target": "notepad" }""");
        _host.Icon = new IconResult(_icon, false);

        using var bitmap = Paint(widget);

        var content = WidgetPainter.ContentRect(Card, _theme);
        Assert.True(IsInk(bitmap.GetPixel((int)content.Left + 2, (int)content.Top + 2)));
        Assert.True(IsInk(bitmap.GetPixel((int)content.Right - 3, (int)content.Bottom - 3)));
        Assert.False(IsInk(bitmap.GetPixel((int)content.Left - 3, 130)));
    }

    [Fact]
    public void AnIconOverride_IsTheOnlyIconAskedFor()
    {
        var widget = Create("""{ "target": "https://example.com", "icon": "C:\\Icons\\home.png" }""");

        Paint(widget).Dispose();

        Assert.Equal([@"C:\Icons\home.png"], _host.IconRequests.Select(r => r.Source));
    }

    [Fact]
    public void AnEmptyIconSetting_MeansTheTarget()
    {
        var widget = Create("""{ "target": "notepad", "icon": "" }""");

        Paint(widget).Dispose();

        Assert.Equal(["notepad"], _host.IconRequests.Select(r => r.Source));
    }

    [Fact]
    public void WhileTheIconIsLoading_ThePlaceholderShowsTheFirstLetterOfTheHost_AndTheIconReplacesIt()
    {
        var widget = Create("""{ "target": "https://homeassistant.example.com" }""");
        var other = Create("""{ "target": "https://www.example.com" }""");

        using var loading = Paint(widget);
        using var loadingOther = Paint(other);
        using var loadingFile = Paint(Create("""{ "target": "C:\\Apps\\editor.exe" }"""));
        _host.Icon = new IconResult(_icon, false);
        using var ready = Paint(widget);

        // A letter in the text colour on the dimmed accent: "H" for the one, "E" (www. does not count) for the other.
        Assert.True(Count(loading, IsText) > 0);
        Assert.Equal(0, Count(loading, IsInk));
        Assert.NotEqual(loading.Bytes, loadingOther.Bytes);
        Assert.Equal(loadingOther.Bytes, loadingFile.Bytes);

        Assert.True(IsInk(ready.GetPixel(130, 130)));
        Assert.Equal(0, Count(ready, IsText));
    }

    [Fact]
    public void WhenThereIsNoIcon_ThePlaceholderStays()
    {
        var widget = Create("""{ "target": "C:\\Apps\\steam.exe" }""");
        using var loading = Paint(widget);
        _host.Icon = IconResult.None;

        using var none = Paint(widget);

        Assert.Equal(loading.Bytes, none.Bytes);
    }

    [Fact]
    public void ALabel_IsDrawnUnderTheIcon_AndGivesThePlaceholderItsLetter()
    {
        var labelled = Create("""{ "target": "C:\\Apps\\steam.exe", "label": "Games" }""");
        var plain = Create("""{ "target": "C:\\Apps\\steam.exe" }""");
        _host.Icon = new IconResult(_icon, false);

        using var withLabel = Paint(labelled);
        using var without = Paint(plain);

        Assert.True(Count(withLabel, IsMuted) > 0);
        Assert.Equal(0, Count(without, IsMuted));
        // The icon gave up the label's space.
        Assert.True(Count(withLabel, IsInk) < Count(without, IsInk));

        _host.Icon = IconResult.Loading;
        using var letterG = Paint(labelled);
        using var letterS = Paint(plain);
        Assert.NotEqual(letterS.Bytes, letterG.Bytes);
    }

    [Theory]
    [InlineData("""{ "target": "notepad" }""")]
    [InlineData("""{ "target": "notepad", "label": "" }""")]
    [InlineData("""{ "target": "notepad", "label": null }""")]
    public void AnUnsetOrEmptyLabel_DrawsNone_AndReservesNoSpace(string json)
    {
        _host.Icon = new IconResult(_icon, false);

        using var bitmap = Paint(Create(json));
        using var reference = Paint(Create("""{ "target": "notepad" }"""));

        Assert.Equal(reference.Bytes, bitmap.Bytes);
        Assert.Equal(0, Count(bitmap, IsMuted));
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "target": "" }""")]
    [InlineData("""{ "target": "   " }""")]
    public void AShortcutWithNoTarget_ShowsTheEmptyPlaceholder_DeclinesTaps_AndLaunchesNothing(string json)
    {
        var widget = Create(json);

        using var bitmap = Paint(widget);
        ITapTarget tap = widget;
        tap.OnTap(Middle);

        Assert.False(tap.CanTap(Middle));
        Assert.Empty(_host.Launches);
        Assert.Empty(_host.IconRequests);
        Assert.Empty(_host.Logged);
        Assert.Equal(0, Count(bitmap, IsText));
        // The dimmed accent over the card: neither the bare card nor nothing.
        Assert.NotEqual(_theme.CardFill, bitmap.GetPixel(130, 130));
    }

    [Fact]
    public void AnInvalidTarget_IsLoggedOnceWhenConfigured_AndDeclinesTaps()
    {
        var widget = Create("""{ "target": "tools\\run.exe" }""");

        Paint(widget).Dispose();
        Paint(widget).Dispose();
        ITapTarget tap = widget;
        tap.OnTap(Middle);

        Assert.Contains(@"tools\run.exe", Assert.Single(_host.Logged));
        Assert.False(tap.CanTap(Middle));
        Assert.Empty(_host.Launches);
    }

    [Fact]
    public void ATap_LaunchesTheTargetOnce_WithTheArguments()
    {
        var widget = Create("""{ "target": "C:\\Apps\\Steam\\steam.exe", "arguments": "-bigpicture" }""");
        ITapTarget tap = widget;

        Assert.True(tap.CanTap(new SKPoint(0, 0)));
        Assert.True(tap.CanTap(new SKPoint(259, 259)));
        tap.OnTap(new SKPoint(5, 250));

        Assert.Equal([(@"C:\Apps\Steam\steam.exe", "-bigpicture")], _host.Launches);
    }

    [Fact]
    public void AFailedLaunch_ChangesNothingOnTheCard()
    {
        var widget = Create("""{ "target": "C:\\Apps\\gone.exe" }""");
        _host.LaunchSucceeds = false;
        using var before = Paint(widget);

        ((ITapTarget)widget).OnTap(Middle);
        using var after = Paint(widget);

        Assert.Single(_host.Launches);
        Assert.False(widget.NeedsRender(DateTime.Now));
        Assert.Equal(before.Bytes, after.Bytes);
        Assert.Empty(_host.Logged);
    }

    [Fact]
    public void ATap_NeedsNoRepaint_AndTheShortcutNeverAnimates()
    {
        var widget = Create("""{ "target": "notepad" }""");
        Paint(widget).Dispose();

        ((ITapTarget)widget).OnTap(Middle);

        Assert.False(widget.NeedsRender(DateTime.Now));
        Assert.False(widget.IsAnimating);
        Assert.Null(widget.NextAnimationAt);
    }

    [Fact]
    public void AnUnattachedShortcut_PaintsItsPlaceholder_AndATapDoesNothing()
    {
        var widget = Create("""{ "target": "notepad" }""", attach: false);

        using var bitmap = Paint(widget);
        ((ITapTarget)widget).OnTap(Middle);

        Assert.True(Count(bitmap, IsText) > 0);
    }

    [Fact]
    public void TheSettings_AreReadFromTheWidgetObject_AndAnUnknownPropertySurvivesASave()
    {
        string path = Path.Combine(_root, "urdeck-config.json");
        File.WriteAllText(path, """
            { "pages": [ { "name": "Main", "widgets": [ {
              "typeId": "urdeck.widgets.shortcut", "col": 2, "row": 3,
              "target": "C:\\Apps\\Steam\\steam.exe", "arguments": "-silent", "icon": "C:\\Icons\\steam.png", "label": "Steam",
              "futureSetting": { "confirm": true }
            } ] } ] }
            """);
        using var store = new ConfigStore(path);

        var config = (ShortcutConfig)store.Config.Pages[0].Widgets.Single().ToConcrete(typeof(ShortcutConfig));
        store.Save();

        Assert.Equal((@"C:\Apps\Steam\steam.exe", "-silent", @"C:\Icons\steam.png", "Steam"), (config.Target, config.Arguments, config.Icon, config.Label));
        Assert.Equal((2, 3, 1, 1), (config.Col, config.Row, config.Width, config.Height));
        using var saved = JsonDocument.Parse(File.ReadAllText(path));
        var widget = saved.RootElement.GetProperty("pages")[0].GetProperty("widgets")[0];
        Assert.True(widget.GetProperty("futureSetting").GetProperty("confirm").GetBoolean());
        Assert.Equal(@"C:\Apps\Steam\steam.exe", widget.GetProperty("target").GetString());
        Assert.Equal("Steam", widget.GetProperty("label").GetString());
    }

    [Fact]
    public void AnUnknownProperty_StaysOnTheConcreteConfigToo()
    {
        var widget = Create("""{ "target": "notepad", "futureSetting": 7 }""");

        Assert.Equal(7, widget.Config.ExtensionData!["futureSetting"].GetInt32());
        Assert.Null(widget.Config.Arguments);
        Assert.Null(widget.Config.Icon);
        Assert.Null(widget.Config.Label);
    }
}
