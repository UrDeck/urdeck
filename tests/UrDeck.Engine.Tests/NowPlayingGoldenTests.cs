// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The now-playing card drawn with the built-in themes (playing in every one, paused and idle in four that differ most), compared
/// with the pictures in <c>Goldens/</c>. The readings are the media sample's track, or what the state needs. Windows only:
/// FreeType and DirectWrite rasterise text differently. See CONTRIBUTING.md, "Golden images".
/// </summary>
public sealed class NowPlayingGoldenTests
{
    private const int Width = 1100;
    private const int Height = 550;

    private static readonly DateTime Moment = new(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);

    /// <summary>The readings of a state; a position stamped at the moment, so a playing bar is where the player reported it.</summary>
    private sealed class Readings(string status) : IReadingSource
    {
        public bool RegionUsesFahrenheit => false;

        public ReadingDescriptor? Describe(string id) => null;

        public Reading Read(string id)
        {
            if (status == "none")
                return id == "media:status" ? Reading.Ok("none") : Reading.Unavailable("nothing is playing");

            return id switch
            {
                "media:status" => Reading.Ok(status),
                "media:title" => Reading.Ok("Midnight Drive"),
                "media:artist" => Reading.Ok("The Sample Band"),
                "media:album" => Reading.Ok("Demo Tracks"),
                "media:app" => Reading.Ok("Music"),
                "media:position" => Reading.Ok(83),
                "media:position/at" => Reading.Ok(new DateTimeOffset(Moment)),
                "media:duration" => Reading.Ok(215),
                "media:art" => Reading.Ok(""),
                _ => Reading.Ok(true),
            };
        }
    }

    private static SKBitmap Render(string theme, string status)
    {
        var loaded = new ThemeStore("").Load(theme);
        var descriptor = PreviewWidgets.Get("urdeck.widgets.nowplaying")!;
        var widget = PreviewWidgets.DefaultConfig(descriptor);
        widget.Col = 0;
        widget.Row = 0;
        widget.Width = 4;
        widget.Height = 2;
        var config = new UrDeckConfig
        {
            Theme = theme,
            Pager = new PagerConfig { Indicator = "off" },
            Pages = [new PageConfig { Name = "Media", Widgets = [widget] }],
            Dock = [],
        };

        using var page = PreviewPage.Build(config, 0, false, new PreviewHost(new Readings(status)));
        return PageRenderer.RenderToBitmap(Width, Height, page.Widgets, loaded, Moment);
    }

    [WindowsGlyphTheory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    [InlineData("carbon")]
    [InlineData("paper")]
    [InlineData("prism")]
    [InlineData("obsidian")]
    [InlineData("ember")]
    [InlineData("blueprint")]
    [InlineData("pop")]
    [InlineData("rigline")]
    public void Playing(string theme)
    {
        using var bitmap = Render(theme, "playing");
        GoldenImage.Compare(bitmap, $"nowplaying-playing-{theme}");
    }

    [WindowsGlyphTheory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("paper")]
    [InlineData("rigline")]
    public void Paused(string theme)
    {
        using var bitmap = Render(theme, "paused");
        GoldenImage.Compare(bitmap, $"nowplaying-paused-{theme}");
    }

    [WindowsGlyphTheory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("paper")]
    [InlineData("rigline")]
    public void Idle(string theme)
    {
        using var bitmap = Render(theme, "none");
        GoldenImage.Compare(bitmap, $"nowplaying-idle-{theme}");
    }

    [Fact]
    public void TheCard_RendersToTheSameBytesTwice()
    {
        using var first = Render("default-dark", "playing");
        using var second = Render("default-dark", "playing");

        Assert.True(first.Bytes.AsSpan().SequenceEqual(second.Bytes));
    }
}
