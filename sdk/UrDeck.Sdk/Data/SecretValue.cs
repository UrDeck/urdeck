// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrDeck.Sdk.Data;

/// <summary>
/// The value of a secret setting. It prints as a fixed mask and gives its text only through <see cref="Reveal"/>, so a provider
/// that logs its settings object cannot leak a token by accident. It never serializes: reading skips the JSON value and writing
/// writes null.
/// </summary>
[JsonConverter(typeof(SecretValueJsonConverter))]
public sealed class SecretValue
{
    /// <summary>What <see cref="ToString"/> returns.</summary>
    public const string Mask = "•••";

    private readonly string _text;

    public SecretValue(string text)
    {
        _text = text ?? "";
    }

    /// <summary>Whether there is no text.</summary>
    public bool IsEmpty => _text.Length == 0;

    /// <summary>The secret's text. Pass it to the service it belongs to and nowhere else.</summary>
    public string Reveal() => _text;

    /// <summary>Always the mask, whatever the text (not its length either).</summary>
    public override string ToString() => Mask;

    /// <summary>Whether <paramref name="other"/> holds the same text.</summary>
    public bool SameAs(SecretValue? other) => other != null && string.Equals(_text, other._text, StringComparison.Ordinal);
}

internal sealed class SecretValueJsonConverter : JsonConverter<SecretValue?>
{
    public override bool HandleNull => true;

    public override SecretValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, SecretValue? value, JsonSerializerOptions options) => writer.WriteNullValue();
}
