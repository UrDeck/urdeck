// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;

namespace UrDeck.Providers.Weather;

/// <summary>
/// The place a reading id names: the first path segment. Free text is a search, <c>@latitude,longitude</c> is a point. A
/// <c>/</c> inside the text is written <c>%2F</c> and a <c>%</c> is written <c>%25</c>. Keys compare without regard to
/// case and surrounding white space, so ids that differ only in those name one place.
/// </summary>
internal sealed class LocationKey : IEquatable<LocationKey>
{
    private LocationKey(string text, double? latitude, double? longitude)
    {
        Text = text;
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>The text to search for, unescaped and trimmed; for a point, the point written as it was typed.</summary>
    public string Text { get; }

    public double? Latitude { get; }

    public double? Longitude { get; }

    public bool IsPoint => Latitude.HasValue;

    /// <summary>Splits a reading path into its location segment (as written) and the rest; false when either is empty.</summary>
    public static bool TrySplit(string path, out string segment, out string reading)
    {
        segment = "";
        reading = "";
        int slash = path.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0 || slash == path.Length - 1)
            return false;
        segment = path[..slash];
        reading = path[(slash + 1)..];
        return true;
    }

    /// <summary>Reads a location segment as written in an id; false when it is empty.</summary>
    public static bool TryParse(string segment, out LocationKey? key)
    {
        key = null;
        string text = Unescape(segment).Trim();
        if (text.Length == 0)
            return false;

        if (TryParsePoint(text, out double latitude, out double longitude))
            key = new LocationKey(latitude.ToString(CultureInfo.InvariantCulture) + ", " + longitude.ToString(CultureInfo.InvariantCulture), latitude, longitude);
        else
            key = new LocationKey(text, null, null);
        return true;
    }

    /// <summary>Writes text as a location segment: <c>%</c> becomes <c>%25</c> and <c>/</c> becomes <c>%2F</c>.</summary>
    public static string Escape(string text) =>
        text.Replace("%", "%25", StringComparison.Ordinal).Replace("/", "%2F", StringComparison.Ordinal);

    /// <summary>Reads a segment back; an <c>%</c> that is not part of <c>%2F</c> or <c>%25</c> stays as it is.</summary>
    public static string Unescape(string segment)
    {
        if (!segment.Contains('%', StringComparison.Ordinal))
            return segment;

        var result = new System.Text.StringBuilder(segment.Length);
        for (int i = 0; i < segment.Length; i++)
        {
            if (segment[i] == '%' && i + 2 < segment.Length && TryEscape(segment, i, out char decoded))
            {
                result.Append(decoded);
                i += 2;
            }
            else
            {
                result.Append(segment[i]);
            }
        }

        return result.ToString();
    }

    private static bool TryEscape(string segment, int at, out char decoded)
    {
        decoded = '\0';
        ReadOnlySpan<char> code = segment.AsSpan(at + 1, 2);
        if (code.Equals("2F", StringComparison.OrdinalIgnoreCase))
            decoded = '/';
        else if (code.Equals("25", StringComparison.Ordinal))
            decoded = '%';
        else
            return false;
        return true;
    }

    private static bool TryParsePoint(string text, out double latitude, out double longitude)
    {
        latitude = longitude = 0;
        if (text[0] != '@')
            return false;
        string[] parts = text[1..].Split(',');
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
            && latitude is >= -90 and <= 90
            && longitude is >= -180 and <= 180;
    }

    public bool Equals(LocationKey? other) =>
        other != null && (IsPoint
            ? other.IsPoint && Latitude == other.Latitude && Longitude == other.Longitude
            : !other.IsPoint && string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase));

    public override bool Equals(object? obj) => obj is LocationKey other && Equals(other);

    public override int GetHashCode() => IsPoint
        ? HashCode.Combine(Latitude, Longitude)
        : StringComparer.OrdinalIgnoreCase.GetHashCode(Text);

    public override string ToString() => IsPoint ? "@" + Text.Replace(" ", "", StringComparison.Ordinal) : Text;
}
