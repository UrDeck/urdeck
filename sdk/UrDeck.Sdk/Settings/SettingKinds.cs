// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Settings;

/// <summary>Which readings a reading setting offers.</summary>
[Flags]
public enum ReadingFilter
{
    /// <summary>Every reading.</summary>
    None = 0,

    /// <summary>Only readings with a range (a minimum and a maximum).</summary>
    HasRange = 1,

    /// <summary>Only readings that can be acted on.</summary>
    HasAction = 2,
}

/// <summary>What a launch target setting accepts.</summary>
[Flags]
public enum LaunchKinds
{
    /// <summary>An application (an executable, a shortcut, a <c>shell:</c> item).</summary>
    App = 1,

    /// <summary>A file, opened with its default application.</summary>
    File = 2,

    /// <summary>A folder.</summary>
    Folder = 4,

    /// <summary>A web address.</summary>
    Address = 8,

    /// <summary>All of them.</summary>
    All = App | File | Folder | Address,
}

/// <summary>
/// The base of the attributes that name a setting's kind where its type does not. The editor chooses the control from
/// <see cref="Kind"/>; a kind it does not know is shown as text. A plugin may derive its own.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public abstract class SettingKindAttribute : Attribute
{
    /// <summary>The kind's name as the catalog and the editor use it (<c>"number"</c>, <c>"range"</c>, ...).</summary>
    public abstract string Kind { get; }
}

/// <summary>
/// An on/off setting held in a property whose type is not <see cref="bool"/> (a <see cref="System.Text.Json.JsonElement"/> that
/// keeps whatever the file had). A <see cref="bool"/> property needs no kind.
/// </summary>
public sealed class ToggleAttribute : SettingKindAttribute
{
    /// <inheritdoc />
    public override string Kind => "toggle";
}

/// <summary>A number with optional limits and a unit. A whole-number property (<see cref="int"/>) accepts whole numbers only.</summary>
public sealed class NumberAttribute : SettingKindAttribute
{
    /// <inheritdoc />
    public override string Kind => "number";

    /// <summary>The smallest value; <see cref="double.NaN"/> for none.</summary>
    public double Min { get; set; } = double.NaN;

    /// <summary>The largest value; <see cref="double.NaN"/> for none.</summary>
    public double Max { get; set; } = double.NaN;

    /// <summary>The step of the arrows; 0 for the editor's choice.</summary>
    public double Step { get; set; }

    /// <summary>A fixed unit shown after the box (<c>"s"</c>).</summary>
    public string? Unit { get; set; }

    /// <summary>
    /// The key of a reading setting: the value is in that reading's canonical unit, and the editor shows and accepts it in the
    /// display unit the card uses (see <see cref="Data.DisplayUnits"/>).
    /// </summary>
    public string? UnitFromReading { get; set; }
}

/// <summary>A bounded number chosen by sliding.</summary>
public sealed class SliderAttribute : SettingKindAttribute
{
    /// <summary>Declares the slider.</summary>
    /// <param name="min">The smallest value.</param>
    /// <param name="max">The largest value.</param>
    /// <param name="step">The distance between positions.</param>
    public SliderAttribute(double min, double max, double step = 1)
    {
        Min = min;
        Max = max;
        Step = step;
    }

    /// <inheritdoc />
    public override string Kind => "range";

    /// <summary>The smallest value.</summary>
    public double Min { get; }

    /// <summary>The largest value.</summary>
    public double Max { get; }

    /// <summary>The distance between positions.</summary>
    public double Step { get; }

    /// <summary>A unit shown after the value.</summary>
    public string? Unit { get; set; }
}

/// <summary>A reading id, chosen from the readings the providers offer.</summary>
public sealed class ReadingAttribute : SettingKindAttribute
{
    /// <inheritdoc />
    public override string Kind => "reading";

    /// <summary>Which readings are offered.</summary>
    public ReadingFilter Filter { get; set; }

    /// <summary>Only the readings of this provider; null for all.</summary>
    public string? Provider { get; set; }
}

/// <summary>An action of the reading chosen in another setting.</summary>
public sealed class ActionAttribute : SettingKindAttribute
{
    /// <summary>Declares the setting.</summary>
    /// <param name="readingSetting">The key of the reading setting whose actions are offered.</param>
    public ActionAttribute(string readingSetting)
    {
        ReadingSetting = readingSetting;
    }

    /// <inheritdoc />
    public override string Kind => "action";

    /// <summary>The key of the reading setting whose actions are offered.</summary>
    public string ReadingSetting { get; }
}

/// <summary>A display unit (Celsius or Fahrenheit) of the reading chosen in another setting.</summary>
public sealed class UnitAttribute : SettingKindAttribute
{
    /// <summary>Declares the setting.</summary>
    /// <param name="readingSetting">The key of the reading setting whose display units are offered.</param>
    public UnitAttribute(string readingSetting)
    {
        ReadingSetting = readingSetting;
    }

    /// <inheritdoc />
    public override string Kind => "unit";

    /// <summary>The key of the reading setting whose display units are offered.</summary>
    public string ReadingSetting { get; }
}

/// <summary>A place found by a provider's search (see <see cref="ISuggestionSource"/>), stored as its name and coordinates.</summary>
public sealed class PlaceAttribute : SettingKindAttribute
{
    /// <summary>Declares the setting.</summary>
    /// <param name="source">The id of the provider that answers the search.</param>
    public PlaceAttribute(string source)
    {
        Source = source;
    }

    /// <inheritdoc />
    public override string Kind => "place";

    /// <summary>The id of the provider that answers the search.</summary>
    public string Source { get; }

    /// <summary>The key of the property that receives the latitude.</summary>
    public string? LatitudeSetting { get; set; }

    /// <summary>The key of the property that receives the longitude.</summary>
    public string? LongitudeSetting { get; set; }
}

/// <summary>An application, file, folder or address to open.</summary>
public sealed class LaunchTargetAttribute : SettingKindAttribute
{
    /// <inheritdoc />
    public override string Kind => "launchTarget";

    /// <summary>What the setting accepts.</summary>
    public LaunchKinds Allowed { get; set; } = LaunchKinds.All;
}

/// <summary>An icon: automatic, a glyph of the theme's icon set, or a file.</summary>
public sealed class IconAttribute : SettingKindAttribute
{
    /// <inheritdoc />
    public override string Kind => "icon";

    /// <summary>Whether an image file may be chosen.</summary>
    public bool AllowFile { get; set; } = true;
}
