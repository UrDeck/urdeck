// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Icons;

/// <summary>The answer to an icon request: the image, or none and whether it is still being loaded.</summary>
/// <param name="Image">
/// The icon, or null. It belongs to the host: draw it during the paint in which it was received, and neither dispose it
/// nor keep it for a later paint.
/// </param>
/// <param name="IsLoading">True when there is no image yet and the host will repaint the widget once it is known.</param>
public readonly record struct IconResult(SKImage? Image, bool IsLoading)
{
    /// <summary>No icon, and none is coming.</summary>
    public static IconResult None => default;

    /// <summary>No icon yet; the widget is repainted when it is ready or given up on.</summary>
    public static IconResult Loading => new(null, true);
}

/// <summary>
/// The host's icon service: the picture for an application, a file or a web address. Safe to call from <c>Render</c> on
/// every paint: it never blocks and does no I/O on the calling thread, and asking again for the same source is cheap.
/// </summary>
public interface IIconSource
{
    /// <summary>
    /// The icon of <paramref name="source"/>, a text classified like a launch target (see
    /// <see cref="Launch.LaunchTarget"/>): a local image file is that image, any other file or a <c>shell:</c> item gives
    /// the icon Windows shows for it, and a web address gives the site's own icon. <paramref name="pixelSize"/> is the
    /// size the widget will draw it at.
    /// </summary>
    IconResult GetIcon(string source, int pixelSize);
}

/// <summary>The icon source of a widget that was never attached, and of a host that offers none: there are no icons.</summary>
internal sealed class NullIconSource : IIconSource
{
    public static readonly NullIconSource Instance = new();

    public IconResult GetIcon(string source, int pixelSize) => IconResult.None;
}
