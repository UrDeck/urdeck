// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Components;

/// <summary>
/// The last few shaders and blur filters the gauges built. It lives on the theme's <see cref="GaugeTheme"/>, not in a static,
/// because widgets come from collectible load contexts and the theme is disposed when it changes; the theme is made per
/// surface size, so a key needs only what differs within one theme.
/// </summary>
internal sealed class GaugeCache : IDisposable
{
    internal const int Capacity = 24;

    private readonly Dictionary<object, IDisposable> _items = [];
    private readonly Queue<object> _order = new();

    /// <summary>How many objects were built (tests).</summary>
    internal int Builds { get; private set; }

    public T Get<T>(object key, Func<T> make) where T : class, IDisposable
    {
        lock (_items)
        {
            if (_items.TryGetValue(key, out IDisposable? item))
                return (T)item;
            if (_items.Count >= Capacity && _order.TryDequeue(out object? oldest) && _items.Remove(oldest, out IDisposable? old))
                old.Dispose();
            T made = make();
            Builds++;
            _items[key] = made;
            _order.Enqueue(key);
            return made;
        }
    }

    public void Dispose()
    {
        lock (_items)
        {
            foreach (var item in _items.Values)
                item.Dispose();
            _items.Clear();
            _order.Clear();
        }
    }
}
