// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>What <c>--snapshot</c> draws of the motion settings (motion): the configuration's level only, and the border still.</summary>
public sealed class MotionSnapshotTests : IDisposable
{
    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(null, null, MotionLevel.Full)]
    [InlineData("off", null, MotionLevel.Off)]
    [InlineData("reduced", "full", MotionLevel.Reduced)]
    [InlineData("full", "reduced", MotionLevel.Reduced)]
    public void ASnapshot_TreatsWindowsAsOn(string? config, string? theme, MotionLevel expected)
    {
        Assert.Equal(expected, MotionPolicy.ForSnapshot(config, theme).Effective);
    }

    [Fact]
    public void ASnapshot_WithMotionOff_DrawsAMovingShaderAsSpeedZeroDoes()
    {
        _fixture.WriteShader("drift", "uniform float iTime;\nhalf4 main(float2 p) { return half4(fract(iTime), 0.2, 0.4, 1); }");
        var theme = _fixture.WriteTheme("snap", """{ "format": 2 }""");
        var moving = BackgroundFixture.Parse("""{ "type": "shader", "name": "drift", "speed": 1 }""");
        var stopped = BackgroundFixture.Parse("""{ "type": "shader", "name": "drift", "speed": 0 }""");

        var off = MotionPolicy.ForSnapshot("off", null);
        using var withOff = BackgroundPlan.Resolve(moving, theme, off.User).Build(_fixture.Store, _fixture.Root)!;
        using var withZero = BackgroundPlan.Resolve(stopped, theme, MotionPolicy.ForSnapshot(null, null).User).Build(_fixture.Store, _fixture.Root)!;
        using var atFull = BackgroundPlan.Resolve(moving, theme, MotionPolicy.ForSnapshot(null, null).User).Build(_fixture.Store, _fixture.Root)!;

        using var a = Paint(withOff);
        using var b = Paint(withZero);
        using var c = Paint(atFull);
        Assert.True(a.Bytes.AsSpan().SequenceEqual(b.Bytes));
        Assert.False(a.Bytes.AsSpan().SequenceEqual(c.Bytes));
    }

    private static SKBitmap Paint(DeckBackground background)
    {
        var bitmap = new SKBitmap(64, 64);
        using var canvas = new SKCanvas(bitmap);
        background.Paint(canvas, 64, 64, 3.5);
        return bitmap;
    }

    [Fact]
    public void ASnapshotOfRigline_IsTheSameWithAndWithoutBorderAnimate()
    {
        using var animated = Render(removeAnimation: false);
        using var plain = Render(removeAnimation: true);

        Assert.True(animated.Bytes.AsSpan().SequenceEqual(plain.Bytes));
    }

    private static SKBitmap Render(bool removeAnimation)
    {
        using var page = SamplePage.Build();
        var loaded = new ThemeStore("").Load("rigline");
        Assert.NotNull(loaded.Definition.Card!.BorderAnimate);
        if (removeAnimation)
            loaded.Definition.Card.BorderAnimate = null;
        return ThemePreview.Render(loaded, page, 550, 960, dock: true);
    }
}
