// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrDeck.Engine.Themes;

/// <summary>
/// <c>gauge.gradient</c>: the text <c>"ramp"</c> (the gauge follows <c>colors.ramp</c>) or a gradient of its own colours.
/// </summary>
[JsonConverter(typeof(ThemeGaugeGradientConverter))]
public sealed class ThemeGaugeGradientDefinition
{
    /// <summary>True for <c>"ramp"</c>.</summary>
    public bool Ramp { get; set; }

    /// <summary>The colours of a gradient written out, or null for <c>"ramp"</c>.</summary>
    public ThemeGradientDefinition? Stops { get; set; }

    /// <summary>What the file said when it was neither <c>"ramp"</c> nor a gradient; <see cref="Sanitize"/> reports it.</summary>
    internal string? Invalid { get; set; }

    internal bool IsEmpty => !Ramp && Stops == null;

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        if (Invalid != null)
        {
            check.WrongKind(path, Invalid, "'ramp' or a gradient with colours");
            Invalid = null;
        }
        if (Stops is { } stops)
        {
            bool had = stops.Colors != null;
            stops.Sanitize(check, path);
            if (stops.Colors == null)
            {
                if (!had)
                    check.Missing(path, "colors");
                Stops = null;
            }
        }
    }
}

internal sealed class ThemeGaugeGradientConverter : JsonConverter<ThemeGaugeGradientDefinition>
{
    public override ThemeGaugeGradientDefinition? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                string text = reader.GetString() ?? "";
                return text.Trim().Equals("ramp", StringComparison.OrdinalIgnoreCase)
                    ? new ThemeGaugeGradientDefinition { Ramp = true }
                    : new ThemeGaugeGradientDefinition { Invalid = $"\"{text}\"" };
            case JsonTokenType.StartObject:
                return new ThemeGaugeGradientDefinition { Stops = JsonSerializer.Deserialize<ThemeGradientDefinition>(ref reader, options) };
            default:
                using (var doc = JsonDocument.ParseValue(ref reader))
                    return new ThemeGaugeGradientDefinition { Invalid = doc.RootElement.GetRawText() };
        }
    }

    public override void Write(Utf8JsonWriter writer, ThemeGaugeGradientDefinition value, JsonSerializerOptions options)
    {
        if (value.Ramp)
            writer.WriteStringValue("ramp");
        else
            JsonSerializer.Serialize(writer, value.Stops, options);
    }
}
