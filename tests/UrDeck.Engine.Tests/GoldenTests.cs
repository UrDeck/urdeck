// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using Xunit;
using Xunit.Sdk;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The sample page drawn with the built-in themes, compared with the pictures in <c>Goldens/</c>. A change that moves a pixel
/// of a v1 theme fails here. Windows only: FreeType and DirectWrite rasterise text differently. See CONTRIBUTING.md,
/// "Golden images".
/// </summary>
public sealed class GoldenTests
{
    private const int Width = 1100;
    private const int Height = 1920;

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "themes");

    private static SKBitmap Render(string theme, bool dock = false, bool draft = false)
    {
        using var page = SamplePage.Build();
        var loaded = new ThemeStore(draft && !ThemeStore.BuiltInNames.Contains(theme) ? FixtureFolder : "").Load(theme);
        return PageRenderer.RenderToBitmap(
            Width, Height, page.Widgets, loaded, SamplePage.Time,
            dock ? new PageRenderer.Chrome(IndicatorMode.Always, 0, 2) : null,
            dock ? page.Dock : null);
    }

    private static List<WidgetLayoutItem> Layout(SamplePage page)
    {
        var loaded = new ThemeStore("").Load(ThemeStore.DefaultName);
        return new GridLayoutManager(Width, Height, loaded.Definition.Card!.Gap!.Value, Height)
            .RenderWidgetLayout(page.Widgets.Select(w => w.Config).ToList(), new System.Drawing.Size(Width, Height));
    }

    [WindowsGlyphFact]
    public void DefaultDark_Page()
    {
        using var bitmap = Render("default-dark");
        GoldenImage.Compare(bitmap, "default-dark-page");
    }

    [WindowsGlyphFact]
    public void DefaultLight_Page()
    {
        using var bitmap = Render("default-light");
        GoldenImage.Compare(bitmap, "default-light-page");
    }

    [WindowsGlyphFact]
    public void Glass_Page()
    {
        using var bitmap = Render("glass");
        GoldenImage.Compare(bitmap, "glass-page");
    }

    [WindowsGlyphFact]
    public void DefaultDark_PageWithDockAndIndicator()
    {
        using var bitmap = Render("default-dark", dock: true);
        GoldenImage.Compare(bitmap, "default-dark-dock");
    }

    /// <summary>
    /// The draft themes (test fixtures) with what card chrome draws of them so far: each later chrome change that draws
    /// more of a draft rewrites its picture, and says so.
    /// </summary>
    [WindowsGlyphTheory]
    [InlineData("ember")]
    [InlineData("blueprint")]
    [InlineData("obsidian")]
    [InlineData("pop")]
    [InlineData("prism")]
    [InlineData("rigline")]
    public void Draft_Page(string theme)
    {
        using var bitmap = Render(theme, draft: true);
        GoldenImage.Compare(bitmap, $"{theme}-page");
    }

    /// <summary>Carbon and Paper, the built-in format 2 themes, in their own fonts.</summary>
    [WindowsGlyphTheory]
    [InlineData("carbon")]
    [InlineData("paper")]
    public void BuiltIn_Page(string theme)
    {
        using var bitmap = Render(theme);
        GoldenImage.Compare(bitmap, $"{theme}-page");
    }

    /// <summary>With the dock: Carbon's corner marks and compact cards, Paper's shadows kept inside the band.</summary>
    [WindowsGlyphTheory]
    [InlineData("carbon")]
    [InlineData("paper")]
    public void BuiltIn_PageWithDock(string theme)
    {
        using var bitmap = Render(theme, dock: true);
        GoldenImage.Compare(bitmap, $"{theme}-dock");
    }

    [WindowsGlyphFact]
    public void Pop_PageWithDock_TintsTheSlotsByPosition()
    {
        using var bitmap = Render("pop", dock: true, draft: true);
        GoldenImage.Compare(bitmap, "pop-dock");
    }

    /// <summary>The drafts that draw outside their cards or have compact cards, with the dock: the dock's underlay stays inside its band.</summary>
    [WindowsGlyphTheory]
    [InlineData("prism")]
    [InlineData("rigline")]
    [InlineData("obsidian")]
    [InlineData("blueprint")]
    public void Draft_PageWithDock(string theme)
    {
        using var bitmap = Render(theme, dock: true, draft: true);
        GoldenImage.Compare(bitmap, $"{theme}-dock");
    }

    [Fact]
    public void TheSamplePage_RendersToTheSameBytesTwice()
    {
        using var first = Render("default-dark", dock: true);
        using var second = Render("default-dark", dock: true);

        Assert.True(first.Bytes.AsSpan().SequenceEqual(second.Bytes));
    }

    [Fact]
    public void TheSamplePage_LaysOutWithoutOverlapOrAnUnplacedWidget()
    {
        using var page = SamplePage.Build();
        var layout = Layout(page);

        Assert.Equal(page.Widgets.Count, layout.Count);
        Assert.All(layout, item => Assert.True(item.Placed));
        var screen = new System.Drawing.Rectangle(0, 0, Width, Height);
        for (int i = 0; i < layout.Count; i++)
        {
            var a = new System.Drawing.Rectangle(layout[i].Position, layout[i].Size);
            Assert.True(screen.Contains(a));
            for (int j = i + 1; j < layout.Count; j++)
                Assert.False(a.IntersectsWith(new System.Drawing.Rectangle(layout[j].Position, layout[j].Size)), $"widgets {i} and {j} overlap");
        }
    }

    [WindowsGlyphFact]
    public void ACardBorderOnePixelWider_FailsTheComparison()
    {
        using var bitmap = Render("default-dark");
        using (var page = SamplePage.Build())
        {
            var card = Layout(page)[^1];
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = false };
            canvas.DrawRect(SKRect.Create(card.Position.X + 0.5f, card.Position.Y + 0.5f, card.Size.Width - 1, card.Size.Height - 1), paint);
        }

        Assert.Throws<FailException>(() => GoldenImage.Compare(bitmap, "default-dark-page"));
    }
}
