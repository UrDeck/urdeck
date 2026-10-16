// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>What a blurred copy of a value depends on, within one theme (whose typeface, spacing and blur are fixed).</summary>
internal readonly record struct GlowKey(string Text, float Size, uint Color, bool Filled);

/// <summary>
/// The last few blurred copies of readout values. It lives on the theme's <see cref="TextStyle"/>, not in a static, because
/// widgets come from collectible load contexts and the theme is disposed when it changes.
/// </summary>
internal sealed class GlowCache : IDisposable
{
    internal const int Capacity = 16;

    private readonly Dictionary<GlowKey, SKImage> _images = [];
    private readonly Queue<GlowKey> _order = new();

    /// <summary>How many copies were blurred (tests).</summary>
    internal int Renders { get; private set; }

    public SKImage Get(GlowKey key, Func<SKImage> render)
    {
        lock (_images)
        {
            if (_images.TryGetValue(key, out var image))
                return image;
            if (_images.Count >= Capacity && _order.TryDequeue(out var oldest) && _images.Remove(oldest, out var old))
                old.Dispose();
            image = render();
            Renders++;
            _images[key] = image;
            _order.Enqueue(key);
            return image;
        }
    }

    public void Dispose()
    {
        lock (_images)
        {
            foreach (var image in _images.Values)
                image.Dispose();
            _images.Clear();
            _order.Clear();
        }
    }
}
