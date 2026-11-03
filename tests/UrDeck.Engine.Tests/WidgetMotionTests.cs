// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;
using EngineMotion = UrDeck.Engine.Motion.MotionLevel;
using SdkMotion = UrDeck.Sdk.MotionLevel;

namespace UrDeck.Engine.Tests;

public sealed class WidgetMotionTests : IDisposable
{
    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

    public void Dispose() => _theme.Dispose();

    private sealed class SeenConfig : WidgetConfig;

    private sealed class Probe : Widget<SeenConfig>
    {
        public SdkMotion? Seen;

        public override void Render(WidgetRenderContext context) => Seen = context.Motion;
    }

    private SdkMotion? Paint(Probe probe, SdkMotion? motion)
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        var size = new System.Drawing.Size(100, 100);
        if (motion is { } m)
            WidgetPainter.Paint(probe, canvas, size, _theme, null, DateTime.Now, CancellationToken.None, m);
        else
            WidgetPainter.Paint(probe, canvas, size, _theme, DateTime.Now, CancellationToken.None);
        return probe.Seen;
    }

    [Fact]
    public void Context_defaults_to_full_motion() => Assert.Equal(SdkMotion.Full, Paint(new Probe(), null));

    [Fact]
    public void A_level_change_reaches_the_next_paint()
    {
        var probe = new Probe();
        Assert.Equal(SdkMotion.Full, Paint(probe, SdkMotion.Full));
        Assert.Equal(SdkMotion.Reduced, Paint(probe, SdkMotion.Reduced));
        Assert.Equal(SdkMotion.Off, Paint(probe, SdkMotion.Off));
    }

    [Theory]
    [InlineData(EngineMotion.Full, SdkMotion.Full)]
    [InlineData(EngineMotion.Reduced, SdkMotion.Reduced)]
    [InlineData(EngineMotion.Off, SdkMotion.Off)]
    public void Engine_level_maps_to_the_sdk_level(EngineMotion engine, SdkMotion expected) =>
        Assert.Equal(expected, UrDeck.Engine.Motion.MotionPolicy.ToWidget(engine));
}
