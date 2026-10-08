// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Components;

namespace UrDeck.Sdk;

/// <summary>
/// The active theme as a widget sees it: colours, and every size already in pixels for the surface being drawn.
/// Widgets never see the cell size, the display resolution or scaling. The host creates one per surface size and
/// disposes it when the theme or the display changes; widgets must not keep a reference past <c>Render</c>.
/// </summary>
public sealed class Theme : IDisposable
{
    private IReadOnlyDictionary<TextRole, SKTypeface> _typefaces = new Dictionary<TextRole, SKTypeface>();

    public SKColor Background { get; init; } = SKColor.Parse("#0f0f1a");
    public SKColor CardFill { get; init; } = SKColor.Parse("#1a1a2e");
    public SKColor CardBorder { get; init; } = SKColors.Transparent;
    public SKColor Text { get; init; } = SKColors.White;
    public SKColor TextMuted { get; init; } = SKColors.White.WithAlpha(0xB3);
    public SKColor Accent { get; init; } = SKColor.Parse("#4a9eff");
    public SKColor AccentDim { get; init; } = SKColor.Parse("#4a9eff").WithAlpha(0x66);
    public SKColor Good { get; init; } = SKColor.Parse("#3ddc84");
    public SKColor Warning { get; init; } = SKColor.Parse("#ffb020");
    public SKColor Critical { get; init; } = SKColor.Parse("#ff5a4d");

    /// <summary>Corner radius of every card, in pixels.</summary>
    public float CardRadius { get; init; } = 12f;
    public float CardBorderWidth { get; init; }
    /// <summary>Space between neighbouring cards, in pixels.</summary>
    public float Gap { get; init; } = 8f;
    /// <summary>Space between a card's edge and its content rectangle, in pixels.</summary>
    public float Padding { get; init; } = 16f;

    public float LabelSize { get; init; } = 14f;
    public float BodySize { get; init; } = 18f;
    public float TitleSize { get; init; } = 24f;
    /// <summary>A unit's size as a fraction of its value's size.</summary>
    public float UnitRatio { get; init; } = 0.4f;

    /// <summary>Line thickness as a fraction of the drawn element's size.</summary>
    public float StrokeRatio { get; init; } = 0.08f;
    public SKStrokeCap StrokeCap { get; init; } = SKStrokeCap.Round;

    /// <summary>The shape a widget draws for a gauge it was asked to draw "as the theme says"; never <see cref="GaugeStyle.Plain"/>.</summary>
    public GaugeStyle GaugeStyle { get; init; } = GaugeStyle.Ring;

    /// <summary>One typeface per role; the theme owns them. Missing roles use the system default.</summary>
    public IReadOnlyDictionary<TextRole, SKTypeface> Typefaces
    {
        get => _typefaces;
        init => _typefaces = value;
    }

    public SKTypeface GetTypeface(TextRole role) =>
        _typefaces.TryGetValue(role, out var typeface) ? typeface : SKTypeface.Default;

    /// <summary>The pixel size of a small-text step.</summary>
    public float GetTextSize(TextStep step) => step switch
    {
        TextStep.Label => LabelSize,
        TextStep.Body => BodySize,
        _ => TitleSize,
    };

    public void Dispose()
    {
        foreach (var typeface in _typefaces.Values.Distinct())
            typeface.Dispose();
        _typefaces = new Dictionary<TextRole, SKTypeface>();
    }
}
