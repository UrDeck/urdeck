// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>How a <see cref="Chip"/> frames its text.</summary>
public enum ChipShape
{
    /// <summary>The text alone.</summary>
    None,
    /// <summary>A rounded outline and fill round the text.</summary>
    Pill,
}

/// <summary>
/// The theme's chip values. A null colour is not drawn (fill, border) or is the text colour (<see cref="TextColor"/>).
/// With <see cref="ColorByStatus"/> the level a widget gives replaces the colours.
/// </summary>
public sealed record ChipTheme(ChipShape Shape = ChipShape.None, SKColor? Fill = null, SKColor? Border = null, SKColor? TextColor = null, bool ColorByStatus = false);

/// <summary>The look of a <see cref="Divider"/>.</summary>
public enum DividerStyle
{
    None,
    Line,
    Gradient,
    /// <summary>A rule with a tick at each end and room for a centred label.</summary>
    Dimension,
}

/// <summary>The theme's divider; the thickness in pixels. A null <see cref="GradientTo"/> fades to the colour's transparent self.</summary>
public sealed record DividerTheme(DividerStyle Style, SKColor Color, float Thickness, SKColor? GradientTo = null);

/// <summary>The theme's well: a fill (null for none), the corner radius in pixels and a border (null for none).</summary>
public sealed record WellTheme(SKColor? Fill, float Radius, SKColor? Border);

/// <summary>
/// The sparkline the theme asks of a gauge: its height and stroke width in pixels, the seconds of history, and the least
/// height in pixels a gauge's rectangle must have to show it (0.9 of a grid cell).
/// </summary>
public sealed record GaugeSparkline(float Height, float StrokeWidth, int HistorySeconds, float MinRectHeight);

/// <summary>The status the theme asks of a gauge: three labels (good, warning, critical) and the fractions where the second and third begin.</summary>
public sealed record GaugeStatusStyle(IReadOnlyList<string> Labels, float WarmAt, float HotAt)
{
    /// <summary>The label and level for <paramref name="fraction"/>.</summary>
    public (string Text, GaugeLevel Level) For(float fraction) =>
        fraction < WarmAt ? (Labels[0], GaugeLevel.Normal)
        : fraction < HotAt ? (Labels[1], GaugeLevel.Warning)
        : (Labels[2], GaugeLevel.Critical);
}

/// <summary>A status a widget gives a gauge: the text and the level it is tinted by.</summary>
public sealed record GaugeStatusText(string Text, GaugeLevel Level);
