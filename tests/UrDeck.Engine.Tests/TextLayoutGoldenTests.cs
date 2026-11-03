// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The text-layout components drawn in a dark and a light built-in theme, compared with the pictures in <c>Goldens/</c>: an
/// ellipsis line beside the shrink line, a clipped line, a marquee at rest and mid-scroll, a wrapped block with and without a
/// line limit, and a list in follow mode. Windows only: FreeType and DirectWrite rasterise text differently. See
/// CONTRIBUTING.md, "Golden images".
/// </summary>
[Collection("TextLayoutCache")]
public sealed class TextLayoutGoldenTests
{
    private const string Title = "The Ballad of a Very Long Track Title That Goes On";
    private const string Paragraph = "The quick brown fox jumps over the lazy dog, then keeps running through the field and far away from the farm.";

    private static SKBitmap Render(string themeName)
    {
        var loaded = new ThemeStore("").Load(themeName);
        using var theme = ThemeResolver.Resolve(loaded, 275);
        var bitmap = new SKBitmap(640, 760);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(theme.Background);

        float lineH = theme.BodySize * 2;
        var column = new SKRect(20, 20, 320, 20 + lineH);
        var start = new DateTime(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);
        float overflow = TextLayoutCache.Width(theme, TextStep.Body, Title) - column.Width;
        double scroll = overflow / (theme.BodySize * Marquee.SizesPerSecond);
        double cycle = Marquee.PauseSeconds + scroll + Marquee.PauseSeconds + Marquee.ReturnSeconds;
        double seconds = start.Ticks / (double)TimeSpan.TicksPerSecond;
        var cycleStart = start.AddSeconds(-(seconds % cycle));
        var midScroll = cycleStart.AddSeconds(Marquee.PauseSeconds + scroll / 2);

        TextOverflow[] modes = [TextOverflow.Shrink, TextOverflow.Ellipsis, TextOverflow.Clip];
        foreach (var mode in modes)
        {
            TextLine.Draw(canvas, theme, column, Title, horizontal: HorizontalAlign.Left, overflow: mode);
            column.Offset(0, lineH);
        }

        TextLine.Draw(canvas, theme, column, Title, horizontal: HorizontalAlign.Left, overflow: TextOverflow.Marquee, time: cycleStart.AddSeconds(0.1));
        column.Offset(0, lineH);
        TextLine.Draw(canvas, theme, column, Title, horizontal: HorizontalAlign.Left, overflow: TextOverflow.Marquee, time: midScroll);

        var block = new SKRect(20, 310, 300, 310 + theme.BodySize * TextBlock.LineSpacing * 6);
        TextBlock.Draw(canvas, theme, block, Paragraph);
        var limited = new SKRect(20, 520, 300, 520 + theme.BodySize * TextBlock.LineSpacing * 6);
        TextBlock.Draw(canvas, theme, limited, Paragraph, maxLines: 2);

        var list = new TextList { Mode = TextListMode.Follow, Capacity = 50 };
        for (int i = 0; i < 12; i++)
        {
            list.Add(new TextListItem(
                i % 3 == 0 ? $"message {i}: a longer line that has to wrap in this narrow column" : $"message {i}",
                Mark: i % 2 == 0 ? "ana" : "bo",
                MarkColor: i % 2 == 0 ? theme.Accent : theme.Good));
        }

        list.Draw(canvas, theme, new SKRect(340, 20, 620, 740), start);
        return bitmap;
    }

    [WindowsGlyphTheory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    public void Components(string theme)
    {
        using var bitmap = Render(theme);
        GoldenImage.Compare(bitmap, $"text-layout-{theme}");
    }

    [Fact]
    public void TheComponents_RenderToTheSameBytesTwice()
    {
        using var first = Render("default-dark");
        using var second = Render("default-dark");

        Assert.True(first.Bytes.AsSpan().SequenceEqual(second.Bytes));
    }
}
