// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using UrDeck.Sdk;

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
    /// A provider's declared settings beyond the base ones: key, type (text, number, wholeNumber, onOff or secret), label, help, default,
    /// whether it is required, and for a secret the environment variable that sets it. Never a value.
    /// </summary>
    internal static IEnumerable<JsonNode> ProviderSettingsOf(UrDeck.Engine.Data.ProviderDescriptor provider) =>
        provider.Schema.Settings.Select(s => (JsonNode)new JsonObject
        {
            ["key"] = s.Key,
            ["type"] = s.Type switch
            {
                UrDeck.Engine.Data.SettingType.Text => "text",
                UrDeck.Engine.Data.SettingType.WholeNumber => "wholeNumber",
                UrDeck.Engine.Data.SettingType.Number => "number",
                UrDeck.Engine.Data.SettingType.Toggle => "onOff",
                _ => "secret",
            },
            ["label"] = s.Label,
            ["help"] = s.Help,
            ["default"] = s.IsSecret ? null : JsonSerializer.SerializeToNode(s.Default, s.Default?.GetType() ?? typeof(object), ConfigOptions),
            ["required"] = s.Required,
            ["secret"] = s.IsSecret,
            ["environmentVariable"] = s.IsSecret ? UrDeck.Engine.Secrets.SecretStore.EnvironmentName(provider.Id, s.Key) : null,
        }).OrderBy(s => (string)s["key"]!, StringComparer.Ordinal);

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
            ["settings"] = Array(Settings(descriptor.ConfigType)),
        };
    }

    /// <summary>
    /// The settings of a configuration type beyond <see cref="WidgetConfig"/>'s own: its name in <c>urdeck-config.json</c>, its
    /// type (or the values it accepts) and its default for a widget written without it (<c>new TConfig()</c>).
    /// </summary>
    internal static IEnumerable<JsonNode> Settings(Type configType)
    {
        object fresh = Activator.CreateInstance(configType)!;
        return configType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType != typeof(WidgetConfig) && p.CanWrite && p.CanRead && p.GetIndexParameters().Length == 0
                && p.GetCustomAttribute<JsonExtensionDataAttribute>() == null && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .Select(p =>
            {
                var setting = new JsonObject
                {
                    ["name"] = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(p.Name),
                    ["type"] = TypeOf(p, out var values),
                    ["default"] = JsonSerializer.SerializeToNode(p.GetValue(fresh), p.PropertyType, ConfigOptions),
                };
                if (values != null)
                    setting["values"] = values;
                if (p.GetCustomAttribute<RangeAttribute>() is { } range)
                {
                    setting["min"] = JsonValue.Create(Convert.ToDouble(range.Minimum, System.Globalization.CultureInfo.InvariantCulture));
                    setting["max"] = JsonValue.Create(Convert.ToDouble(range.Maximum, System.Globalization.CultureInfo.InvariantCulture));
                }

                return (JsonNode)setting;
            })
            .OrderBy(s => (string)s["name"]!, StringComparer.Ordinal);
    }

    /// <summary>text, number, wholeNumber, onOff, list, object, values (the accepted values follow) or any.</summary>
    private static string TypeOf(PropertyInfo property, out JsonArray? values)
    {
        values = null;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (property.GetCustomAttribute<AllowedValuesAttribute>() is { } allowed)
        {
            if (allowed.Values.All(v => v is bool))
                return "onOff";
            values = new JsonArray([.. allowed.Values.Select(v => JsonValue.Create(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)))]);
            return "values";
        }

        if (property.GetCustomAttribute<RangeAttribute>() is { Minimum: int, Maximum: int })
            return "wholeNumber";
        if (type == typeof(string))
            return "text";
        if (type == typeof(bool))
            return "onOff";
        if (type == typeof(int) || type == typeof(long))
            return "wholeNumber";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return "number";
        if (type == typeof(JsonElement))
            return "any";
        if (typeof(IEnumerable).IsAssignableFrom(type) && !typeof(IDictionary).IsAssignableFrom(type))
            return "list";
        return "object";
    }

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
