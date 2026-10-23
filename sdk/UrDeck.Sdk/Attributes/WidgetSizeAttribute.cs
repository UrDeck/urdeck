// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class WidgetSizeAttribute : Attribute
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// Whether generated pictures of the widget (previews, the website, the tests of default configurations) include this
    /// size; default true. False leaves it out of them, for a size that only repeats what a smaller one shows. It does not
    /// change where the widget may be placed.
    /// </summary>
    public bool Preview { get; init; } = true;

    public WidgetSizeAttribute(int width, int height)
    {
        Width = width;
        Height = height;
    }
}
