// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;

namespace UrDeck.Engine.Backgrounds;

public enum ParameterKind
{
    Number,
    Color,
}

/// <summary>What the manifest says about one parameter. <see cref="Kind"/> is null when the manifest gives none.</summary>
public sealed record ManifestParameter(
    string Name, string? Label, string? Description, ParameterKind? Kind, double? Min, double? Max, double? Step, string? Unit, bool Hidden);

/// <summary>
/// A sample file of a background. A <c>video</c> type is accepted and never played. <see cref="Settings"/> is how the sample
/// was made, as written (a clone that outlives the document); the host never uses it.
/// </summary>
public sealed record ManifestSample(string Type, string? File, int? Order, string? Caption, JsonElement? Settings = null);

/// <summary>The version 1 manifest as far as it could be read; every field but <see cref="Version"/> may be missing.</summary>
public sealed record BackgroundManifest(
    int Version,
    string? Title,
    string? Description,
    string? Entry,
    string? Author,
    string? License,
    string? PackageVersion,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, ManifestParameter> Parameters,
    IReadOnlyList<ManifestSample> Samples)
{
    public const string FileName = "manifest.json";
    public const int SupportedVersion = 1;
}

/// <summary>The manifest (null when it fails the background) and every diagnostic found while reading it.</summary>
public sealed record ManifestReadResult(BackgroundManifest? Manifest, IReadOnlyList<ShaderDiagnostic> Diagnostics);

/// <summary>
/// Reads <c>manifest.json</c> by hand so that a wrong type or an unknown key is a diagnostic and the rest is still used.
/// Only two things fail: not JSON with an object on top, and a <c>manifest</c> that is not the integer 1.
/// </summary>
public static class BackgroundManifestReader
{
    internal static readonly string[] TopKeys =
        ["$schema", "manifest", "title", "description", "entry", "author", "license", "version", "tags", "parameters", "samples"];

    internal static readonly string[] ParameterKeys = ["label", "description", "kind", "min", "max", "step", "unit", "hidden"];
    internal static readonly string[] SampleKeys = ["type", "file", "order", "caption", "settings"];
    internal static readonly string[] Kinds = ["number", "color"];
    internal static readonly string[] SampleTypes = ["image", "video"];

    // The same leniency as the configuration: comments and trailing commas.
    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static ManifestReadResult Read(string text)
    {
        var found = new List<ShaderDiagnostic>();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, Options);
        }
        catch (JsonException ex)
        {
            found.Add(Error(ShaderCodes.ManifestNotJson, $"{BackgroundManifest.FileName} is not valid JSON: {ex.Message}"));
            return new ManifestReadResult(null, found);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                found.Add(Error(ShaderCodes.ManifestNotJson, $"{BackgroundManifest.FileName} must hold a JSON object, not {root.ValueKind}."));
                return new ManifestReadResult(null, found);
            }

            if (!root.TryGetProperty("manifest", out var version))
            {
                found.Add(Error(ShaderCodes.ManifestVersion,
                    $"'manifest' is missing; it must be the integer {BackgroundManifest.SupportedVersion}, the version of the format."));
                return new ManifestReadResult(null, found);
            }

            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number))
            {
                found.Add(Error(ShaderCodes.ManifestVersion,
                    $"'manifest' must be the integer {BackgroundManifest.SupportedVersion}, the version of the format (it is {version.GetRawText()})."));
                return new ManifestReadResult(null, found);
            }

            if (number != BackgroundManifest.SupportedVersion)
            {
                found.Add(Error(ShaderCodes.ManifestVersion,
                    $"'manifest' is {number}; this version of UrDeck reads version {BackgroundManifest.SupportedVersion}."));
                return new ManifestReadResult(null, found);
            }

            return new ManifestReadResult(ReadObject(root, found), found);
        }
    }

    private static BackgroundManifest ReadObject(JsonElement root, List<ShaderDiagnostic> found)
    {
        CheckKeys(root, TopKeys, "the manifest", found);

        var tags = new List<string>();
        if (root.TryGetProperty("tags", out var tagsElement))
        {
            if (tagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsElement.EnumerateArray())
                {
                    if (tag.ValueKind == JsonValueKind.String)
                        tags.Add(tag.GetString()!);
                    else
                        found.Add(WrongType("tags", "a string", tag));
                }
            }
            else
            {
                found.Add(WrongType("tags", "an array of strings", tagsElement));
            }
        }

        var parameters = new Dictionary<string, ManifestParameter>(StringComparer.Ordinal);
        if (root.TryGetProperty("parameters", out var parametersElement))
        {
            if (parametersElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in parametersElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Object)
                        parameters[property.Name] = ReadParameter(property.Name, property.Value, found);
                    else
                        found.Add(WrongType($"parameters.{property.Name}", "an object", property.Value));
                }
            }
            else
            {
                found.Add(WrongType("parameters", "an object", parametersElement));
            }
        }

        var samples = new List<ManifestSample>();
        if (root.TryGetProperty("samples", out var samplesElement))
        {
            if (samplesElement.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in samplesElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                        samples.Add(ReadSample(index, item, found));
                    else
                        found.Add(WrongType($"samples[{index}]", "an object", item));
                    index++;
                }
            }
            else
            {
                found.Add(WrongType("samples", "an array", samplesElement));
            }
        }

        return new BackgroundManifest(
            BackgroundManifest.SupportedVersion,
            Text(root, "title", "title", found),
            Text(root, "description", "description", found),
            Text(root, "entry", "entry", found),
            Text(root, "author", "author", found),
            Text(root, "license", "license", found),
            Text(root, "version", "version", found),
            tags,
            parameters,
            samples);
    }

    private static ManifestParameter ReadParameter(string name, JsonElement element, List<ShaderDiagnostic> found)
    {
        string where = $"parameters.{name}";
        CheckKeys(element, ParameterKeys, where, found);

        ParameterKind? kind = null;
        string? kindText = Text(element, "kind", $"{where}.kind", found);
        if (kindText != null)
        {
            switch (kindText)
            {
                case "number":
                    kind = ParameterKind.Number;
                    break;
                case "color":
                    kind = ParameterKind.Color;
                    break;
                default:
                    found.Add(Warning(ShaderCodes.UnknownKind, $"{where}.kind '{kindText}' is not defined (number, color); it is ignored."));
                    break;
            }
        }

        return new ManifestParameter(
            name,
            Text(element, "label", $"{where}.label", found),
            Text(element, "description", $"{where}.description", found),
            kind,
            Number(element, "min", $"{where}.min", found),
            Number(element, "max", $"{where}.max", found),
            Number(element, "step", $"{where}.step", found),
            Text(element, "unit", $"{where}.unit", found),
            Flag(element, "hidden", $"{where}.hidden", found));
    }

    private static ManifestSample ReadSample(int index, JsonElement element, List<ShaderDiagnostic> found)
    {
        string where = $"samples[{index}]";
        CheckKeys(element, SampleKeys, where, found);

        string type = Text(element, "type", $"{where}.type", found) ?? "image";
        if (Array.IndexOf(SampleTypes, type) < 0)
        {
            found.Add(Warning(ShaderCodes.UnknownSampleType, $"{where}.type '{type}' is not defined (image, video)."));
            type = "image";
        }

        int? order = null;
        if (element.TryGetProperty("order", out var orderElement))
        {
            if (orderElement.ValueKind == JsonValueKind.Number && orderElement.TryGetInt32(out int value))
                order = value;
            else
                found.Add(WrongType($"{where}.order", "an integer", orderElement));
        }

        JsonElement? kept = null;
        if (element.TryGetProperty("settings", out var settings))
        {
            if (settings.ValueKind == JsonValueKind.Object)
                kept = settings.Clone();
            else
                found.Add(WrongType($"{where}.settings", "an object", settings));
        }

        return new ManifestSample(type, Text(element, "file", $"{where}.file", found), order, Text(element, "caption", $"{where}.caption", found), kept);
    }

    private static void CheckKeys(JsonElement element, string[] known, string where, List<ShaderDiagnostic> found)
    {
        foreach (var property in element.EnumerateObject())
            if (Array.IndexOf(known, property.Name) < 0)
                found.Add(Warning(ShaderCodes.UnknownKey, $"'{property.Name}' is not a key of {where}; it is ignored."));
    }

    private static string? Text(JsonElement element, string key, string where, List<ShaderDiagnostic> found)
    {
        if (!element.TryGetProperty(key, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();
        found.Add(WrongType(where, "a string", value));
        return null;
    }

    private static double? Number(JsonElement element, string key, string where, List<ShaderDiagnostic> found)
    {
        if (!element.TryGetProperty(key, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number))
            return number;
        found.Add(WrongType(where, "a number", value));
        return null;
    }

    private static bool Flag(JsonElement element, string key, string where, List<ShaderDiagnostic> found)
    {
        if (!element.TryGetProperty(key, out var value))
            return false;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();
        found.Add(WrongType(where, "true or false", value));
        return false;
    }

    private static ShaderDiagnostic WrongType(string where, string expected, JsonElement actual) =>
        Warning(ShaderCodes.WrongType, $"'{where}' must be {expected}, not {actual.ValueKind}; it is ignored.");

    private static ShaderDiagnostic Warning(string code, string message) => new(code, ShaderSeverity.Warning, message, BackgroundManifest.FileName);

    private static ShaderDiagnostic Error(string code, string message) => new(code, ShaderSeverity.Error, message, BackgroundManifest.FileName);
}
