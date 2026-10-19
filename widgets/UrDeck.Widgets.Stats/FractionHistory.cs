// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Widgets.Stats;

/// <summary>
/// The recent fractions of one gauge position: at most one value for each second (the latest in it), at most
/// <c>capacity</c> of them, oldest dropped first. A ring of floats, so a position costs at most 4 bytes per second kept.
/// </summary>
internal sealed class FractionHistory(int capacity)
{
    private readonly float[] _values = new float[capacity];
    private int _start;
    private long _lastSecond = long.MinValue;

    public int Capacity => _values.Length;

    public int Count { get; private set; }

    /// <summary>Records <paramref name="fraction"/> as the value of the second <paramref name="time"/> falls in.</summary>
    public void Add(DateTime time, float fraction)
    {
        long second = time.Ticks / TimeSpan.TicksPerSecond;
        if (second == _lastSecond && Count > 0)
        {
            _values[(_start + Count - 1) % _values.Length] = fraction;
            return;
        }

        _lastSecond = second;
        if (Count < _values.Length)
        {
            _values[(_start + Count) % _values.Length] = fraction;
            Count++;
            return;
        }

        _values[_start] = fraction;
        _start = (_start + 1) % _values.Length;
    }

    /// <summary>The values, oldest first.</summary>
    public float[] ToArray()
    {
        float[] result = new float[Count];
        for (int i = 0; i < Count; i++)
            result[i] = _values[(_start + i) % _values.Length];
        return result;
    }
}
