// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using SkiaSharp;
using UrDeck.Engine.Data;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Stats;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The preview renderer (preview-rendering): what a preview is made of, frames, one widget alone, determinism.</summary>
public sealed class PreviewTests : IDisposable
{
    private readonly BackgroundFixture _fixture = new();
    private readonly PreviewRenderer _renderer;

    public PreviewTests() => _renderer = new PreviewRenderer(_fixture.Themes, _fixture.Store, _fixture.Root);

    public void Dispose() => _fixture.Dispose();

    private static LoadedTheme Theme(string name) => ThemeStore.LoadBuiltIn(name)!;

    private List<SKBitmap> Frames(PreviewRequest request, int count, double fps)
    {
        var frames = new List<SKBitmap>();
        _renderer.RenderFrames(request, count, fps, (_, bitmap) => frames.Add(bitmap.Copy()));
        return frames;
    }

    private static bool Same(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    private static void DisposeAll(IEnumerable<SKBitmap> bitmaps)
    {
        foreach (var bitmap in bitmaps)
            bitmap.Dispose();
    }

    [Fact]
    public void TheSamePreview_TwiceGivesTheSameBytes()
    {
        var request = new PreviewRequest { Theme = Theme("rigline"), Background = PreviewBackground.Theme, Width = 550, Height = 960, WithDock = true };

        using var first = _renderer.Render(request);
        using var second = _renderer.Render(request);

        Assert.True(Same(first, second));
    }

    [Fact]
    public void ThemesChoice_DrawsTheThemesOwnBackground_AndNoneDrawsItsColour()
    {
        var theme = Theme("rigline");
        Assert.Equal("horizon", theme.Definition.Background?.Default?.Name);
        using var own = _renderer.Render(new PreviewRequest { Theme = theme, Background = PreviewBackground.Theme, Width = 275, Height = 480 });
        using var none = _renderer.Render(new PreviewRequest { Theme = theme, Background = PreviewBackground.None, Width = 275, Height = 480 });

        Assert.False(Same(own, none));
        // Below the cards, where nothing else is drawn, a page without a background is the theme's colour (in a theme without a pattern).
        var plain = Theme("default-dark");
        using var colour = _renderer.Render(new PreviewRequest { Theme = plain, Background = PreviewBackground.None, Width = 275, Height = 480 });
        Assert.Equal(SKColor.Parse(plain.Definition.Colors!.Background), colour.GetPixel(137, 470));
    }

    [Fact]
    public void FrameZero_EqualsTheStillPreview_WithAMovingBackgroundAndATurningBorder()
    {
        var request = new PreviewRequest { Theme = Theme("rigline"), Background = PreviewBackground.Theme, Width = 275, Height = 480, WithDock = true };

        using var still = _renderer.Render(request);
        var frames = Frames(request, 2, 15);

        Assert.True(Same(still, frames[0]));
        Assert.False(Same(frames[0], frames[1]));
        DisposeAll(frames);
    }

    [Fact]
    public void AMovingBackground_MovesFromFrameToFrame()
    {
        var request = new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.clock", new System.Drawing.Size(4, 2)),
            Theme = Theme("default-dark"),
            Background = PreviewBackground.Named("aurora"),
            Width = 200,
        };

        using var still = _renderer.Render(request);
        var frames = Frames(request, 3, 30);

        Assert.True(Same(still, frames[0]));
        Assert.False(Same(frames[0], frames[1]));
        Assert.False(Same(frames[1], frames[2]));
        DisposeAll(frames);
    }

    [Fact]
    public void AtReducedMotion_TheBackgroundAndTheBorderStayPut()
    {
        var request = new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.clock", new System.Drawing.Size(4, 2)),
            Theme = Theme("rigline"),
            Background = PreviewBackground.Theme,
            Width = 400,
            Motion = MotionLevel.Reduced,
        };

        var reduced = Frames(request, 4, 15);
        var full = Frames(request with { Motion = MotionLevel.Full }, 4, 15);

        Assert.All(reduced, frame => Assert.True(Same(reduced[0], frame)));
        Assert.False(Same(full[0], full[3]));
        DisposeAll(reduced.Concat(full));
    }

    /// <summary>A spin of 8 s at 2 s has turned a quarter: the top's colour is the still border's colour at the left.</summary>
    [Fact]
    public void ASpinningBorder_HasTurnedAQuarter_AfterAQuarterOfItsPeriod()
    {
        var theme = _fixture.WriteTheme("spin", """
            { "format": 2,
              "colors": { "background": "#000000", "cardFill": "#000000", "accent": "#ffffff" },
              "card": { "radius": 0, "gap": 0.05, "borderWidth": 0.04,
                        "borderGradient": { "kind": "conic", "colors": ["#ff0000", "#ffff00", "#00ff00", "#00ffff", "#0000ff", "#ff00ff", "#ff0000"] },
                        "borderAnimate": { "mode": "spin", "period": 8 } } }
            """);
        var request = new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.shortcut", new System.Drawing.Size(1, 1)),
            Theme = theme,
            Background = PreviewBackground.None,
            Width = 1600,
        };

        using var still = _renderer.Render(request);
        var frames = Frames(request, 31, 15);
        var turned = frames[30];

        var (top, left) = (BorderPoint(still, vertical: true), BorderPoint(still, vertical: false));
        Assert.True(Near(turned.GetPixel(top.X, top.Y), still.GetPixel(left.X, left.Y), 40),
            $"the top after 2 s is {turned.GetPixel(top.X, top.Y)}, the still border's left is {still.GetPixel(left.X, left.Y)}");
        Assert.False(Near(turned.GetPixel(top.X, top.Y), still.GetPixel(top.X, top.Y), 40), "the border has moved");
        DisposeAll(frames);
    }

    /// <summary>The middle of the top edge's border (scanning down the middle column) or of the left edge's (along the middle row).</summary>
    private static SKPointI BorderPoint(SKBitmap bitmap, bool vertical)
    {
        int mid = (vertical ? bitmap.Width : bitmap.Height) / 2;
        for (int i = 0; i < (vertical ? bitmap.Height : bitmap.Width); i++)
        {
            var point = vertical ? new SKPointI(mid, i) : new SKPointI(i, mid);
            var c = bitmap.GetPixel(point.X, point.Y);
            if (Math.Max(c.Red, Math.Max(c.Green, c.Blue)) > 200)
                return vertical ? new SKPointI(mid, i + 2) : new SKPointI(i + 2, mid);
        }

        throw new InvalidOperationException("no border found");
    }

    private static bool Near(SKColor a, SKColor b, int limit) =>
        Math.Abs(a.Red - b.Red) <= limit && Math.Abs(a.Green - b.Green) <= limit && Math.Abs(a.Blue - b.Blue) <= limit;

    [Fact]
    public void BorderSteps_FollowTheHost_SpinInFifteenthsAndPulseBetweenFullAndLow()
    {
        var spin = _fixture.WriteTheme("steps", """
            { "format": 2, "card": { "borderWidth": 0.01, "borderGradient": { "kind": "conic", "colors": ["#ff0000", "#0000ff"] },
              "borderAnimate": { "mode": "spin", "period": 8 } } }
            """);
        var pulse = _fixture.WriteTheme("breath", """{ "format": 2, "card": { "borderWidth": 0.01, "borderAnimate": { "mode": "pulse", "period": 2 } } }""");
        using var spinStyle = ResolvedCardStyle.Resolve(spin, 275);
        using var pulseStyle = ResolvedCardStyle.Resolve(pulse, 275);

        Assert.Null(BorderFrame.StepAt(spinStyle, null));
        Assert.Null(BorderFrame.StepAt(spinStyle, new FrameTime(0, MotionLevel.Full)));
        Assert.Null(BorderFrame.StepAt(spinStyle, new FrameTime(2, MotionLevel.Reduced)));
        Assert.Equal(90f, BorderFrame.StepAt(spinStyle, new FrameTime(2, MotionLevel.Full))!.Value.Angle, 3);
        Assert.Equal(3f, BorderFrame.StepAt(spinStyle, new FrameTime(1 / 15.0, MotionLevel.Full))!.Value.Angle, 3);
        Assert.Null(BorderFrame.StepAt(spinStyle, new FrameTime(8, MotionLevel.Full)));
        Assert.Equal(BorderFrame.PulseLow, BorderFrame.StepAt(pulseStyle, new FrameTime(1, MotionLevel.Full))!.Value.Opacity, 3);
        float early = BorderFrame.StepAt(pulseStyle, new FrameTime(0.4, MotionLevel.Full))!.Value.Opacity;
        Assert.InRange(early, BorderFrame.PulseLow + 0.01f, 0.99f);
    }

    [Fact]
    public void AGauge_RisesAcrossTheFrames_WithItsSampleKeyframes()
    {
        // The sample's GPU load goes from 82 at 0 s to 91 at 2 s.
        var config = new StatsConfig { Slots = [new StatsSlot { Reading = "system:gpu/load" }] };
        var request = new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.stats", new System.Drawing.Size(4, 2), config),
            Theme = Theme("default-dark"),
            Background = PreviewBackground.None,
            Width = 800,
        };
        var accent = SKColor.Parse(Theme("default-dark").Definition.Colors!.Accent);

        var frames = Frames(request, 60, 30);
        int[] fill = [.. frames.Select(f => Count(f, c => Near(c, accent, 24)))];

        Assert.True(fill[59] > fill[0], $"the gauge's fill grows: {fill[0]} pixels at frame 0, {fill[59]} at frame 59");
        Assert.True(fill[30] >= fill[0] && fill[59] >= fill[30]);
        DisposeAll(frames);
    }

    private static int Count(SKBitmap bitmap, Func<SKColor, bool> match)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (match(bitmap.GetPixel(x, y)))
                    count++;
            }
        }

        return count;
    }

    /// <summary>Carbon's sparkline is filled before the first frame: it reaches across the gauge, not one point of it.</summary>
    [Fact]
    public void ASparkline_IsFullInTheFirstFrame()
    {
        var with = Theme("carbon");
        Assert.NotNull(with.Definition.Gauge?.Sparkline);
        var without = Theme("carbon");
        without.Definition.Gauge!.Sparkline = null;
        var request = new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.stats", new System.Drawing.Size(4, 2)),
            Background = PreviewBackground.None,
            Width = 1100,
        };

        var clock = Stopwatch.StartNew();
        using var line = _renderer.Render(request with { Theme = with });
        long preRoll = clock.ElapsedMilliseconds;
        using var plain = _renderer.Render(request with { Theme = without });

        int left = int.MaxValue, right = int.MinValue;
        for (int y = 0; y < line.Height; y++)
        {
            for (int x = 0; x < line.Width; x++)
            {
                if (line.GetPixel(x, y) == plain.GetPixel(x, y))
                    continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        Assert.True(right - left > line.Width / 4, $"the sparkline spans {right - left} pixels (pre-roll and frame: {preRoll} ms)");
        Assert.True(preRoll < 30000, $"60 seconds of history took {preRoll} ms");
    }

    [Fact]
    public void AWidgetAlone_IsItsCells()
    {
        using var stats = _renderer.Render(new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.stats", new System.Drawing.Size(4, 2)),
            Theme = Theme("default-dark"),
            Width = 1100,
        });
        using var shortcut = _renderer.Render(new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.shortcut", new System.Drawing.Size(1, 1)),
            Theme = Theme("default-dark"),
            Width = 1100,
        });

        Assert.Equal((1100, 550), (stats.Width, stats.Height));
        Assert.Equal((275, 275), (shortcut.Width, shortcut.Height));
    }

    [Fact]
    public void ASizeTheWidgetDoesNotDeclare_IsRefused_NamingTheSizesItHas()
    {
        var ex = Assert.Throws<PreviewException>(() => _renderer.Render(new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.weather", new System.Drawing.Size(1, 1)),
            Theme = Theme("default-dark"),
        }));

        Assert.Contains("4x2, 4x4", ex.Message);
    }

    [Fact]
    public void AnUnknownWidget_IsRefused_NamingTheWidgetsItKnows()
    {
        var ex = Assert.Throws<PreviewException>(() => _renderer.Render(new PreviewRequest
        {
            Widget = new PreviewWidget("example.unknown", new System.Drawing.Size(1, 1)),
            Theme = Theme("default-dark"),
        }));

        Assert.Contains("urdeck.widgets.clock, urdeck.widgets.shortcut, urdeck.widgets.stats, urdeck.widgets.weather", ex.Message);
    }

    [Fact]
    public void AUsersConfiguration_IsDrawnAtTheGivenPage_AndNotChanged()
    {
        string path = Path.Combine(_fixture.Root, "urdeck-config.json");
        const string json = """
            { "theme": "glass", "pages": [
                { "name": "One", "widgets": [ { "typeId": "urdeck.widgets.clock", "col": 0, "row": 0, "width": 4, "height": 1 } ] },
                { "name": "Two", "widgets": [ { "typeId": "urdeck.widgets.stats", "col": 0, "row": 0, "width": 4, "height": 2 } ] } ] }
            """;
        File.WriteAllText(path, json);

        var config = PreviewConfig.Load(path);
        using var first = _renderer.Render(new PreviewRequest { Config = config, Page = 0, Width = 275, Height = 480 });
        using var second = _renderer.Render(new PreviewRequest { Config = config, Page = 1, Width = 275, Height = 480 });
        using var glass = _renderer.Render(new PreviewRequest { Config = config, Page = 1, Theme = Theme("glass"), Width = 275, Height = 480 });

        Assert.False(Same(first, second));
        Assert.True(Same(second, glass), "the configuration's theme is used");
        Assert.Equal(json, File.ReadAllText(path));
        Assert.Throws<PreviewException>(() => _renderer.Render(new PreviewRequest { Config = config, Page = 2 }));
    }

    [Fact]
    public void TheSamplePage_IsAConfigurationOfTheExpectedWidgets()
    {
        var config = PreviewConfig.Sample();

        Assert.Equal(
            ["urdeck.widgets.clock", "urdeck.widgets.stats", "urdeck.widgets.stats", "urdeck.widgets.weather",
             "urdeck.widgets.shortcut", "urdeck.widgets.shortcut", "urdeck.widgets.shortcut", "urdeck.widgets.shortcut"],
            config.Pages[0].Widgets.Select(w => w.WidgetTypeId));
        Assert.Equal(4, config.Dock.Count);
    }

    /// <summary>
    /// Every first-party widget's default configuration, at every size it previews, draws more than an empty card in
    /// default-dark and reads only available readings from the samples (widget-sdk, "Default Configuration Draws Something").
    /// </summary>
    [Fact]
    public void EveryDefaultConfiguration_DrawsSomething_AtEveryPreviewedSize()
    {
        var theme = Theme("default-dark");
        var failures = new List<string>();
        foreach (var descriptor in PreviewWidgets.Descriptors)
        {
            foreach (var size in descriptor.PreviewSizes)
            {
                string name = $"{descriptor.Id} at {size.Width}x{size.Height}";
                using var drawn = _renderer.Render(new PreviewRequest
                {
                    Widget = new PreviewWidget(descriptor.Id, size),
                    Theme = theme,
                    Background = PreviewBackground.None,
                    Width = 1100,
                });
                using var empty = EmptyCard(theme, size, drawn.Height);
                double share = Count(drawn, empty) / (double)(drawn.Width * drawn.Height);
                if (share < 0.02)
                    failures.Add($"{name}: only {share:P1} of the card differs from an empty card");

                foreach (var (id, reading) in DefaultReadings(descriptor, size))
                {
                    if (reading.State != ReadingState.Ok)
                        failures.Add($"{name}: '{id}' is {reading.State} ({reading.Reason})");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static int Count(SKBitmap a, SKBitmap b)
    {
        int count = 0;
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                    count++;
            }
        }

        return count;
    }

    /// <summary>The card of a widget that draws nothing, at the same place and size, cut as the preview cuts it.</summary>
    private static SKBitmap EmptyCard(LoadedTheme theme, System.Drawing.Size size, int height)
    {
        var config = new WidgetConfig { WidgetTypeId = "test.blank", Width = size.Width, Height = size.Height };
        using var page = PageRenderer.RenderToBitmap(1100, height, [(config, new Blank())], theme, PreviewConfig.SampleMoment);
        using var subset = new SKBitmap();
        page.ExtractSubset(subset, SKRectI.Create(size.Width * 275, height));
        return subset.Copy();
    }

    /// <summary>What the widget's default configuration subscribes to, as the preview's samples answer it.</summary>
    private static List<(string Id, Reading Reading)> DefaultReadings(Engine.Plugin.WidgetDescriptor descriptor, System.Drawing.Size size)
    {
        var moment = PreviewConfig.SampleMoment;
        using var hub = new ReadingHub(PreviewProviders.Registry(() => moment, moment), regionUsesFahrenheit: false, regionUses24HourClock: true,
            sampling: ReadingHubSampling.Manual);
        var config = PreviewWidgets.DefaultConfig(descriptor);
        config.Width = size.Width;
        config.Height = size.Height;
        var widget = PreviewWidgets.Create(config, new PreviewHost(hub))!;
        hub.Subscribe(widget.Subscriptions);
        hub.SampleNow();
        var readings = widget.Subscriptions.Select(id => (id, hub.Read(id))).ToList();
        (widget as IDisposable)?.Dispose();
        return readings;
    }

    private sealed class Blank : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context)
        {
        }
    }
}
