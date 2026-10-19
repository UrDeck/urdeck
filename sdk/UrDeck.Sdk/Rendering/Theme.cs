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
    private SKColor? _accent2;
    private SKColor? _ink;
    private IReadOnlyList<SKColor>? _ramp;
    private DividerTheme? _divider;

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

    /// <summary>The second accent colour; the accent when the theme has none.</summary>
    public SKColor Accent2
    {
        get => _accent2 ?? Accent;
        init => _accent2 = value;
    }

    /// <summary>The colour for text on an accent fill; the text or the background colour, whichever contrasts more with the accent, when the theme has none.</summary>
    public SKColor Ink
    {
        get => _ink ?? ColorMath.MoreContrasting(Accent, Text, Background);
        init => _ink = value;
    }

    /// <summary>Two to five colours a value can be coloured along; the good, warning and critical colours when the theme has none.</summary>
    public IReadOnlyList<SKColor> Ramp
    {
        get => _ramp ?? [Good, Warning, Critical];
        init => _ramp = value;
    }

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

    /// <summary>The gauge values of the theme (sweep, segments, ticks, redline, marker and the rest), in pixels; a theme that sets none changes nothing.</summary>
    public GaugeTheme Gauge { get; init; } = new();

    /// <summary>The chip values of the theme; a theme that sets none gives plain text.</summary>
    public ChipTheme Chip { get; init; } = new();

    /// <summary>The divider; a theme that sets none gives the rule the Weather card always drew (muted text at about a third of its opacity, at least a pixel thick).</summary>
    public DividerTheme Divider
    {
        get => _divider ?? new DividerTheme(DividerStyle.Line, TextMuted.WithAlpha(0x55), Math.Max(1f, Gap * 0.15f));
        init => _divider = value;
    }

    /// <summary>The well drawn behind grouped content, or null when the theme sets none.</summary>
    public WellTheme? Well { get; init; }

    /// <summary>Letter case, spacing, slant, size, fill and glow of text, in pixels; a theme that sets none changes nothing.</summary>
    public TextStyle TextStyle { get; init; } = new();

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
        TextStyle.Dispose();
        Gauge.Dispose();
        foreach (var typeface in _typefaces.Values.Distinct())
            typeface.Dispose();
        _typefaces = new Dictionary<TextRole, SKTypeface>();
    }
}
