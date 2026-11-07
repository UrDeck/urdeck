// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using UrDeck.Sdk.Settings;

namespace UrDeck.Cli;

internal sealed record CatalogExportOptions(string? Output, string? ThemeDir, string? BackgroundDir);

/// <summary>
/// <c>catalog export</c>: what the product knows about what it ships (themes, backgrounds, pairings, widgets, providers and
/// finding codes) as one JSON document for the website's build. Lists are sorted by name, object keys are sorted, lines end in
/// LF and the document ends with a newline, so the same input gives the same bytes.
/// </summary>
internal static class CatalogExportRunner
{
    public const int Version = 1;

    // As urdeck-config.json writes a widget: camelCase, nothing for null.
    private static readonly JsonSerializerOptions ConfigOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // The placement keys every widget has; a widget's default leaves them to whoever places it.
    private static readonly HashSet<string> PlacementKeys = ["typeId", "col", "row", "width", "height", "parameters", "isVisible"];

    public static int Run(CatalogExportOptions options, TextWriter output)
    {
        string? themes = Folder(options.ThemeDir, "--theme-dir");
        string? backgrounds = Folder(options.BackgroundDir, "--background-dir");
        string? outputPath = options.Output is { } o ? Path.GetFullPath(o) : null;
        if (outputPath != null && !Directory.Exists(Path.GetDirectoryName(outputPath)))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} cannot be written: its folder does not exist.");

        using var log = new LogCapture();
        // Without a folder of the user's, a folder that does not exist: only the built-in ones are listed.
        string none = Path.Combine(Path.GetTempPath(), "urdeck-cli-no-folder-" + Guid.NewGuid().ToString("N"));
        string text = Export(new ThemeStore(themes ?? none), new ShaderStore(backgrounds ?? none));

        if (outputPath == null)
        {
            output.Write(text);
            return ExitCodes.Ok;
        }

        try
        {
            File.WriteAllText(outputPath, text, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} cannot be written: {ex.Message}");
        }

        return ExitCodes.Ok;
    }

    private static string? Folder(string? path, string option)
    {
        if (path == null)
            return null;
        string full = Path.GetFullPath(path);
        return Directory.Exists(full) ? full : throw new ToolFailureException($"{option} {PathDisplay.Of(path)} does not exist.");
    }

    /// <summary>The document's text.</summary>
    internal static string Export(ThemeStore themeStore, ShaderStore shaderStore)
    {
        var themes = ThemeCatalog.List(themeStore).OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        var backgrounds = BackgroundCatalog.List(shaderStore).OrderBy(b => b.Name, StringComparer.Ordinal).ToList();
        var pairing = new PairingIndex(themes, backgrounds);

        var document = new JsonObject
        {
            ["version"] = Version,
            ["themes"] = Array(themes.Select(t => (JsonNode)new JsonObject
            {
                ["name"] = t.Name,
                ["displayName"] = t.DisplayName,
                ["description"] = t.Description,
                ["author"] = t.Author,
                ["mode"] = t.Mode,
                ["tags"] = Strings(t.Tags),
                ["swatch"] = new JsonObject
                {
                    ["background"] = t.Swatch.Background,
                    ["cardFill"] = t.Swatch.CardFill,
                    ["accent"] = t.Swatch.Accent,
                    ["text"] = t.Swatch.Text,
                },
                ["defaultBackground"] = t.DefaultBackground,
                ["pairsWith"] = Strings(t.PairsWithBackgrounds),
                ["builtIn"] = t.IsBuiltIn,
            })),
            ["backgrounds"] = Array(backgrounds.Select(b => (JsonNode)new JsonObject
            {
                ["name"] = b.Name,
                ["title"] = b.Title,
                ["description"] = b.Description,
                ["author"] = b.Author,
                ["tags"] = Strings(b.Tags),
                ["parameters"] = Parameters(b.Parameters),
                ["pairsWith"] = Strings(b.PairsWithThemes),
                ["builtIn"] = b.IsBuiltIn,
            })),
            ["pairings"] = Array(themes.Select(t => (JsonNode)new JsonObject
            {
                ["theme"] = t.Name,
                ["backgrounds"] = Array(pairing.BackgroundsFor(t.Name)
                    .Select(p => (JsonNode)new JsonObject { ["name"] = p.Name, ["mutual"] = p.Mutual })),
            })),
            ["widgets"] = Array(PreviewWidgets.Descriptors.Select(Widget)),
            ["providers"] = Array(PreviewProviders.Descriptors.Select(p => (JsonNode)new JsonObject
            {
                ["id"] = p.Id,
                ["name"] = p.Name,
                ["sample"] = PreviewProviders.HasSample(p.Id),
                ["settings"] = Array(ProviderSettingsOf(p)),
            })),
            ["findings"] = Array(FindingCatalog.All.OrderBy(f => f.Code, StringComparer.Ordinal).Select(f => (JsonNode)new JsonObject
            {
                ["code"] = f.Code,
                ["severity"] = f.Severity,
                ["summary"] = f.Summary,
            })),
        };

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            Sorted(document)!.WriteTo(writer);
        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>
    /// A provider's declared settings beyond the base ones, written like a widget's (<see cref="SettingsOf"/>), plus for a secret the
    /// environment variable that sets it. Never a value.
    /// </summary>
    internal static IEnumerable<JsonNode> ProviderSettingsOf(UrDeck.Engine.Data.ProviderDescriptor provider) =>
        provider.Schema.Settings.Select(s =>
        {
            var node = SettingNode(s);
            node["environmentVariable"] = s.IsSecret ? UrDeck.Engine.Secrets.SecretStore.EnvironmentName(provider.Id, s.Key) : null;
            return (JsonNode)node;
        });

    private static JsonNode Widget(WidgetDescriptor descriptor)
    {
        var defaults = JsonSerializer.SerializeToNode(PreviewWidgets.DefaultConfig(descriptor), descriptor.ConfigType, ConfigOptions)!.AsObject();
        foreach (string key in PlacementKeys)
        {
            if (key != "typeId")
                defaults.Remove(key);
        }

        var sizes = descriptor.SupportedSizes.Select(s => (JsonNode)new JsonObject
        {
            ["width"] = s.Width,
            ["height"] = s.Height,
            ["preview"] = descriptor.PreviewSizes.Contains(s),
        });

        return new JsonObject
        {
            ["typeId"] = descriptor.Id,
            ["name"] = descriptor.Name,
            ["description"] = descriptor.Description,
            ["category"] = descriptor.Category,
            ["sizes"] = Array(sizes),
            ["defaultConfig"] = defaults,
            ["settings"] = Array(SettingsOf(descriptor.Settings)),
        };
    }

    /// <summary>
    /// The declared settings of a schema (a widget's configuration, a provider's settings or a list item), in display order: its
    /// name in <c>urdeck-config.json</c>, kind, label, help, group, sizes, options, limits, fallback, default and, for a list of
    /// positions, the positions per size and the item's settings. The one writer for widgets and providers.
    /// </summary>
    internal static IEnumerable<JsonNode> SettingsOf(SettingSchema schema) => schema.Settings.Select(s => (JsonNode)SettingNode(s));

    private static JsonObject SettingNode(SettingDescriptor s)
    {
        var node = new JsonObject
        {
            ["name"] = s.Key,
            ["kind"] = s.Kind,
            ["type"] = LegacyType(s),
            ["label"] = s.Label,
            ["help"] = s.Help,
            ["default"] = s.IsSecret ? null : JsonSerializer.SerializeToNode(s.Default, s.Default?.GetType() ?? typeof(object), ConfigOptions),
            ["required"] = s.Required,
            ["secret"] = s.IsSecret,
        };
        if (s.Group != null)
            node["group"] = s.Group;
        if (s.Sizes is { Count: > 0 })
            node["sizes"] = Strings(s.Sizes);
        if (s.When != null)
            node["shownWhen"] = new JsonObject { ["key"] = s.When.Key, ["values"] = Strings(s.When.Values) };
        if (s.Fallback.Text != null || s.Fallback.From != FallbackSource.None)
        {
            node["fallback"] = new JsonObject
            {
                ["text"] = s.Fallback.Text,
                ["from"] = JsonNamingPolicy.CamelCase.ConvertName(s.Fallback.From.ToString()),
                ["reading"] = s.Fallback.ReadingSetting,
            };
        }

        if (s.AllowsNone)
            node["allowsNone"] = true;
        if (s.Options.Count > 0)
        {
            node["values"] = Strings(s.Options.Select(o => o.Value));
            node["options"] = Array(s.Options.Select(o => (JsonNode)new JsonObject { ["value"] = o.Value, ["label"] = o.Label }));
        }

        if (s.Number != null)
        {
            node["min"] = s.Number.Min;
            node["max"] = s.Number.Max;
            if (s.Number.Step > 0)
                node["step"] = s.Number.Step;
            if (s.Number.Unit != null)
                node["unit"] = s.Number.Unit;
            if (s.Number.UnitFromReading != null)
                node["unitFromReading"] = s.Number.UnitFromReading;
        }

        if (s.Reading != null)
        {
            node["reading"] = new JsonObject
            {
                ["filter"] = s.Reading.Filter.ToString(),
                ["provider"] = s.Reading.Provider,
            };
        }

        if (s.OwnerReading != null)
            node["readingSetting"] = s.OwnerReading;
        if (s.Place != null)
            node["place"] = new JsonObject { ["source"] = s.Place.Source, ["latitude"] = s.Place.LatitudeSetting, ["longitude"] = s.Place.LongitudeSetting };
        if (s.Launch != null)
            node["launch"] = s.Launch.Value.ToString();
        if (s.DisabledInRoles.Count > 0)
        {
            node["disabledInRoles"] = Strings(s.DisabledInRoles);
            node["disabledHelp"] = s.DisabledHelp;
        }

        if (s.Positions != null)
        {
            node["positions"] = Array(s.Positions.Sizes.Select(p => (JsonNode)new JsonObject { ["size"] = p.Size, ["roles"] = Strings(p.Roles) }));
            node["item"] = Array(SettingsOf(s.Item!));
        }

        return node;
    }

    /// <summary>The older description of a setting's type: text, number, wholeNumber, onOff, values (the accepted values follow), list, secret or the kind.</summary>
    private static string LegacyType(SettingDescriptor s) => s.Kind switch
    {
        SettingKinds.Toggle => "onOff",
        SettingKinds.Choice => "values",
        SettingKinds.Text => "text",
        SettingKinds.Number or SettingKinds.Range => s.Number is { WholeNumber: true } ? "wholeNumber" : "number",
        SettingKinds.Secret => "secret",
        SettingKinds.Positions => "list",
        var other => other,
    };

    private static JsonObject Parameters(IReadOnlyDictionary<string, ManifestParameter> parameters)
    {
        var result = new JsonObject();
        foreach (var (name, p) in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            result[name] = new JsonObject
            {
                ["label"] = p.Label,
                ["description"] = p.Description,
                ["kind"] = p.Kind is { } kind ? JsonNamingPolicy.CamelCase.ConvertName(kind.ToString()) : null,
                ["min"] = p.Min,
                ["max"] = p.Max,
                ["step"] = p.Step,
                ["unit"] = p.Unit,
                ["hidden"] = p.Hidden,
            };
        }

        return result;
    }

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(v => JsonValue.Create(v))]);

    private static JsonArray Array(IEnumerable<JsonNode> nodes) => new([.. nodes]);

    /// <summary>A copy with every object's keys in ordinal order, so the bytes do not depend on the order of the code.</summary>
    private static JsonNode? Sorted(JsonNode? node) => node switch
    {
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Sorted(p.Value)))),
        JsonArray a => new JsonArray([.. a.Select(Sorted)]),
        _ => node?.DeepClone(),
    };
}
