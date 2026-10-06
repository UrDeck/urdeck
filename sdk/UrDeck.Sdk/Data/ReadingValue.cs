// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Globalization;

namespace UrDeck.Sdk.Data;

/// <summary>Which of its three shapes a <see cref="ReadingValue"/> has.</summary>
public enum ReadingValueType
{
    Number,
    Text,
    OnOff,
}

/// <summary>
/// A reading's value: a number, a text or an on/off state. It is an SDK type so that a widget can read a value from any
/// provider without referencing that provider's assembly.
/// </summary>
public readonly struct ReadingValue : IEquatable<ReadingValue>
{
    private readonly double _number;
    private readonly string? _text;

    private ReadingValue(ReadingValueType type, double number, string? text)
    {
        Type = type;
        _number = number;
        _text = text;
    }

    public ReadingValueType Type { get; }

    /// <summary>The number; 0 unless <see cref="Type"/> is <see cref="ReadingValueType.Number"/>.</summary>
    public double Number => _number;

    /// <summary>The text; empty unless <see cref="Type"/> is <see cref="ReadingValueType.Text"/>.</summary>
    public string Text => _text ?? "";

    /// <summary>The state; false unless <see cref="Type"/> is <see cref="ReadingValueType.OnOff"/>.</summary>
    public bool IsOn => Type == ReadingValueType.OnOff && _number != 0;

    public static ReadingValue FromNumber(double number) => new(ReadingValueType.Number, number, null);

    public static ReadingValue FromText(string text) => new(ReadingValueType.Text, 0, text ?? "");

    public static ReadingValue FromOnOff(bool on) => new(ReadingValueType.OnOff, on ? 1 : 0, null);

    public static implicit operator ReadingValue(double number) => FromNumber(number);

    public static implicit operator ReadingValue(string text) => FromText(text);

    public static implicit operator ReadingValue(bool on) => FromOnOff(on);

    public bool Equals(ReadingValue other) =>
        Type == other.Type && _number.Equals(other._number) && string.Equals(_text, other._text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ReadingValue other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Type, _number, _text);

    public static bool operator ==(ReadingValue left, ReadingValue right) => left.Equals(right);

    public static bool operator !=(ReadingValue left, ReadingValue right) => !left.Equals(right);

    public override string ToString() => Type switch
    {
        ReadingValueType.Text => Text,
        ReadingValueType.OnOff => IsOn ? "On" : "Off",
        _ => _number.ToString(CultureInfo.InvariantCulture),
    };
}
