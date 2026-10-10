// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using UrDeck.Sdk.Icons;
using UrDeck.Sdk.Launch;

namespace UrDeck.Sdk.Data;

/// <summary>
/// What a widget reads from. Every member is safe on the UI thread at any time, never blocks and never does I/O.
/// </summary>
public interface IReadingSource
{
    /// <summary>The reading with this id (<c>provider:path</c>). Never throws; an unknown id is unavailable.</summary>
    Reading Read(string id);

    /// <summary>The catalog entry for the id, or null when the provider or the reading is unknown (or not yet described).</summary>
    ReadingDescriptor? Describe(string id);

    /// <summary>Whether the user's Windows region shows temperatures in Fahrenheit.</summary>
    bool RegionUsesFahrenheit { get; }

    /// <summary>
    /// Whether the user's Windows region shows the time on a 24 hour clock. The default reads the current culture, so a
    /// source written before times existed keeps compiling.
    /// </summary>
    bool RegionUses24HourClock => !System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("tt", StringComparison.Ordinal);
}

/// <summary>The services the host offers a widget, handed over by <see cref="IWidget.Attach"/>.</summary>
public interface IWidgetHost
{
    IReadingSource Readings { get; }

    /// <summary>Writes one line to the host's log, for a fact worth reporting (a missing resource). The default drops it.</summary>
    void Log(string message)
    {
    }

    /// <summary>Opens files, applications and addresses. The default starts nothing.</summary>
    ILauncher Launcher => NullLauncher.Instance;

    /// <summary>The icons of applications, files and web addresses. The default has none.</summary>
    IIconSource Icons => NullIconSource.Instance;
}

/// <summary>The reading source of a widget that was never attached: everything is unavailable.</summary>
internal sealed class NullReadingSource : IReadingSource
{
    public static readonly NullReadingSource Instance = new();

    private static readonly Reading NotAttached = Reading.Unavailable("no data source");

    public Reading Read(string id) => NotAttached;

    public ReadingDescriptor? Describe(string id) => null;

    public bool RegionUsesFahrenheit => false;
}
