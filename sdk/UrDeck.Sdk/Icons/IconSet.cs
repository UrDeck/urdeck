// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Components;

namespace UrDeck.Sdk;

/// <summary>One outline of a glyph, in the 24 by 24 space of an icon file. A filled shape is also filled with the icon's colour.</summary>
public sealed record IconShape(SKPath Path, bool Filled);

/// <summary>A parsed icon: its shapes in the 24 by 24 space. Immutable; the set that made it owns the paths.</summary>
public sealed class IconGlyph(IReadOnlyList<IconShape> shapes)
{
    /// <summary>The side of the space the shapes are in.</summary>
    public const float Size = 24f;

    public IReadOnlyList<IconShape> Shapes { get; } = shapes;
}

/// <summary>The icons of a theme, asked for by key (<c>weather/rain</c>). Answering does no I/O.</summary>
public interface IIconSet
{
    /// <summary>True, with the glyph, when the set has an icon for <paramref name="key"/>.</summary>
    bool TryGet(string key, out IconGlyph glyph);
}

/// <summary>What <see cref="IconTheme.Tint"/> colours an icon with.</summary>
public enum IconTint
{
    Accent,
    Accent2,
    Text,
    /// <summary>The colour of the theme's ramp at the position the widget gives.</summary>
    Ramp,
    /// <summary>The set's own colours are not available; the icon is drawn in the text colour.</summary>
    None,
    /// <summary>The fixed colour in <see cref="IconTheme.TintColor"/>.</summary>
    Fixed,
}

/// <summary>The glow behind an icon: the blur in pixels, a colour (null for the tint) and an opacity.</summary>
public sealed record IconGlow(float BlurPx, SKColor? Color, float Opacity);

/// <summary>
/// The theme's icon values. The set is shared by every <see cref="Theme"/> of a theme and is not owned here; the cache of
/// blurred glows is, and is disposed with the theme.
/// </summary>
public sealed class IconTheme : IDisposable
{
    /// <summary>The stroke when a theme sets none: 1.6 in 24.</summary>
    public const float DefaultStroke = 1.6f / 24f;

    private static readonly IIconSet Empty = new EmptyIconSet();

    /// <summary>The theme's icons, with the built-in fallback behind them; an empty set when there is none (a default theme).</summary>
    public IIconSet Set { get; init; } = Empty;

    /// <summary>True when the theme names a set that was found; a widget with icons of its own for the other case checks it.</summary>
    public bool NamesSet { get; init; }

    /// <summary>The stroke width as a fraction of the icon's size.</summary>
    public float Stroke { get; init; } = DefaultStroke;

    public IconTint Tint { get; init; } = IconTint.Accent;

    /// <summary>The colour for <see cref="IconTint.Fixed"/>.</summary>
    public SKColor TintColor { get; init; } = SKColors.White;

    public IconGlow? Glow { get; init; }

    internal GlowCache GlowImages { get; } = new();

    public void Dispose() => GlowImages.Dispose();

    private sealed class EmptyIconSet : IIconSet
    {
        public bool TryGet(string key, out IconGlyph glyph)
        {
            glyph = null!;
            return false;
        }
    }
}
