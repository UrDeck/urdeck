// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// A bounded, least-recently-used cache of laid-out text, so that a widget that repaints with the same text, step and width
/// measures nothing. The key is everything that changes the result: the text, the step, the width, the theme's typeface and
/// size for the step, and its case and spacing. It holds strings and numbers only (the typeface by its id), so it keeps no
/// theme, widget or plugin type alive.
/// </summary>
internal static class TextLayoutCache
{
    private const int Capacity = 512;

    private readonly record struct Key(string Text, TextStep Step, int Width, uint Typeface, float Size, TextCase Case, float Tracking);

    private sealed record Entry(IReadOnlyList<string> Lines, float Width);

    private static readonly object Gate = new();
    private static readonly Dictionary<Key, LinkedListNode<(Key Key, Entry Entry)>> Map = [];
    private static readonly LinkedList<(Key Key, Entry Entry)> Order = new();
    private static int LayoutCount;

    /// <summary>How many layouts have been computed (cache misses) since the process started. For tests.</summary>
    public static int Layouts => Volatile.Read(ref LayoutCount);

    /// <summary>Forgets everything cached. For tests.</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            Map.Clear();
            Order.Clear();
        }
    }

    /// <summary>The number of layouts held.</summary>
    public static int Count
    {
        get
        {
            lock (Gate)
                return Map.Count;
        }
    }

    /// <summary><paramref name="text"/>, with the theme's case applied, broken into lines no wider than <paramref name="width"/>.</summary>
    public static IReadOnlyList<string> Lines(Theme theme, TextStep step, string text, float width) =>
        Get(theme, step, text, (int)MathF.Floor(width)).Lines;

    /// <summary>The width of <paramref name="text"/> on one line at the step's size, with the theme's case and spacing.</summary>
    public static float Width(Theme theme, TextStep step, string text) => Get(theme, step, text, -1).Width;

    private static Entry Get(Theme theme, TextStep step, string text, int width)
    {
        var typeface = theme.GetTypeface(RoleOf(step));
        float size = theme.GetTextSize(step);
        var textCase = TextStyle.CaseFor(theme.TextStyle, step);
        float tracking = TextStyle.TrackingFor(theme.TextStyle, step);
        var key = new Key(text, step, width, IdOf(typeface), size, textCase, tracking);
        lock (Gate)
        {
            if (Map.TryGetValue(key, out var node))
            {
                Order.Remove(node);
                Order.AddFirst(node);
                return node.Value.Entry;
            }
        }

        Interlocked.Increment(ref LayoutCount);
        string cased = TextStyle.Apply(textCase, text);
        using var font = TextMetrics.CreateFont(typeface, size);
        var entry = width < 0
            ? new Entry([cased], TextMetrics.Measure(font, cased, tracking))
            : new Entry(TextMetrics.Break(font, cased, tracking, width), 0);
        lock (Gate)
        {
            if (!Map.ContainsKey(key))
            {
                Map[key] = Order.AddFirst((key, entry));
                while (Map.Count > Capacity)
                {
                    Map.Remove(Order.Last!.Value.Key);
                    Order.RemoveLast();
                }
            }
        }

        return entry;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SKTypeface, object> Ids = new();
    private static uint NextId;

    /// <summary>A number for a typeface that lasts as long as the typeface does, without keeping it alive.</summary>
    private static uint IdOf(SKTypeface typeface) =>
        (uint)Ids.GetValue(typeface, _ => (object)(uint)Interlocked.Increment(ref NextId));

    internal static TextRole RoleOf(TextStep step) => step switch
    {
        TextStep.Label => TextRole.Label,
        TextStep.Body => TextRole.Body,
        _ => TextRole.Title,
    };
}
