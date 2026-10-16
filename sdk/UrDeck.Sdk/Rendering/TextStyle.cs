// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Components;

namespace UrDeck.Sdk;

/// <summary>The letter case a theme asks for.</summary>
public enum TextCase
{
    /// <summary>The text is drawn as given.</summary>
    None,
    Upper,
    Lower,
}

/// <summary>A gradient that fills a readout's value: colours evenly spaced across the value's box, at an angle.</summary>
public sealed class TextFill
{
    /// <summary>Two to six colours, from the start of the gradient to its end.</summary>
    public required IReadOnlyList<SKColor> Colors { get; init; }

    /// <summary>The gradient's direction in degrees: 0 runs left to right, 90 top to bottom.</summary>
    public float AngleDegrees { get; init; }
}

/// <summary>A soft blurred copy drawn behind a readout's value.</summary>
public sealed class TextGlow
{
    /// <summary>The blur radius in pixels for the surface being drawn.</summary>
    public float BlurPx { get; init; }

    /// <summary>The glow's colour; null for the value's own colour (or its gradient).</summary>
    public SKColor? Color { get; init; }

    /// <summary>Strength, from 0 to 1.</summary>
    public float Alpha { get; init; } = 0.6f;
}

/// <summary>
/// How the theme wants text drawn beyond typeface and size, already in pixels for the surface. The <see cref="Readout"/> and
/// <see cref="TextLine"/> components apply it, so a widget that draws with them needs no code of its own; a widget that draws
/// text itself may read it. A new instance changes nothing.
/// </summary>
public sealed class TextStyle : IDisposable
{
    /// <summary>The case of a readout's label and of a text line at the label step.</summary>
    public TextCase LabelCase { get; init; }

    /// <summary>The case of a text line at the title step.</summary>
    public TextCase TitleCase { get; init; }

    /// <summary>Letter spacing of a readout's value, in em of its size, added after each character but the last.</summary>
    public float ValueTracking { get; init; }

    /// <summary>Letter spacing of a readout's label and of a text line at the label step, in em.</summary>
    public float LabelTracking { get; init; }

    /// <summary>Letter spacing of a text line at the title step, in em.</summary>
    public float TitleTracking { get; init; }

    /// <summary>The lean of a readout's value to the right, in degrees (0 to 20), about its baseline.</summary>
    public float ValueSlantDegrees { get; init; }

    /// <summary>
    /// Multiplies the size a widget asks of a readout; the result is never larger than the size that fills the rectangle.
    /// </summary>
    public float ValueScale { get; init; } = 1f;

    /// <summary>The gradient a readout's value is filled with, or null for the text colour.</summary>
    public TextFill? ValueFill { get; init; }

    /// <summary>The glow behind a readout's value, or null for none.</summary>
    public TextGlow? ValueGlow { get; init; }

    /// <summary>Blurred copies of values, kept while the theme lives so a repaint with the same value does not blur again.</summary>
    internal GlowCache Glows { get; } = new();

    internal static TextCase CaseFor(TextStyle style, TextStep step) => step switch
    {
        TextStep.Label => style.LabelCase,
        TextStep.Title => style.TitleCase,
        _ => TextCase.None,
    };

    internal static float TrackingFor(TextStyle style, TextStep step) => step switch
    {
        TextStep.Label => style.LabelTracking,
        TextStep.Title => style.TitleTracking,
        _ => 0f,
    };

    internal static string Apply(TextCase textCase, string text) => textCase switch
    {
        TextCase.Upper => text.ToUpperInvariant(),
        TextCase.Lower => text.ToLowerInvariant(),
        _ => text,
    };

    public void Dispose() => Glows.Dispose();
}
