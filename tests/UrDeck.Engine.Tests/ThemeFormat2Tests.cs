// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json.Nodes;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// Format 2 of the theme file: the values are read, checked, merged and kept. Most of the checks are driven by the published
/// schema, so every key it lists is tried valid, out of range and of the wrong kind.
/// </summary>
public sealed class ThemeFormat2Tests : IDisposable
{
    private const string Name = "probe";

    private static readonly JsonObject Schema = LoadSchema();

    // Format 1 keys fail the whole theme when they are of the wrong kind, as they always did: only format 2 keys are swept.
    private static readonly HashSet<string> Format1Paths =
    [
        "colors.background", "colors.cardFill", "colors.cardBorder", "colors.text", "colors.textMuted", "colors.accent",
        "colors.accentDim", "colors.good", "colors.warning", "colors.critical", "colors.backgroundPalette",
        "card.radius", "card.borderWidth", "card.gap", "card.padding",
        "typography.font", "typography.weights", "typography.labelSize", "typography.bodySize", "typography.titleSize",
        "typography.unitRatio", "stroke", "indicator", "press", "dock", "gauge.style",
    ];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-theme2-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public ThemeFormat2Tests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static JsonObject LoadSchema()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root)!;
        return JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "schemas", "v2", "theme.json")))!.AsObject();
    }

    private static string[] ThemeLines =>
        File.Exists(UrDeckLog.LogPath)
            ? File.ReadAllLines(UrDeckLog.LogPath).Where(l => l.Contains($"Theme '{Name}'", StringComparison.Ordinal)).ToArray()
            : [];

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private static void ClearLog()
    {
        if (File.Exists(UrDeckLog.LogPath))
            File.Delete(UrDeckLog.LogPath);
    }

    private LoadedTheme Load(string json, string name = Name)
    {
        string dir = Path.Combine(_root, "themes", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), json);
        return new ThemeStore(Path.Combine(_root, "themes")).Load(name);
    }

    /// <summary>The value at a dotted path of the loaded definition, or null where a group on the way is absent.</summary>
    private static object? Dig(object? from, string path)
    {
        foreach (string segment in path.Split('.'))
        {
            if (from == null)
                return null;
            var property = from.GetType().GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                           ?? throw new InvalidOperationException($"The model has no '{segment}' on {from.GetType().Name} (path {path}).");
            from = property.GetValue(from);
        }
        return from;
    }

    // ---------------------------------------------------------------- the schema, walked

    private static JsonObject Resolve(JsonObject node) =>
        node["$ref"] is JsonValue reference
            ? Merge((JsonObject)Schema["$defs"]![reference.GetValue<string>().Split('/').Last()]!.DeepClone(), node)
            : node;

    private static JsonObject Merge(JsonObject baseNode, JsonObject over)
    {
        foreach (var (key, value) in over)
            if (key != "$ref")
                baseNode[key] = value?.DeepClone();
        return baseNode;
    }

    private static JsonObject NodeAt(string path)
    {
        var node = Schema;
        foreach (string segment in path.Split('.'))
            node = Resolve(node["properties"]![segment]!.AsObject());
        return node;
    }

    private static IEnumerable<string> Paths(JsonObject node, string prefix, bool groups)
    {
        foreach (var (key, child0) in node["properties"]!.AsObject())
        {
            if (key == "$schema")
                continue;
            var child = Resolve(child0!.AsObject());
            string path = prefix.Length == 0 ? key : prefix + "." + key;
            bool isGroup = child["type"]?.GetValue<string>() == "object" && child["properties"] != null;
            if (isGroup)
            {
                if (groups)
                    yield return path;
                foreach (string inner in Paths(child, path, groups))
                    yield return inner;
            }
            else
            {
                yield return path;
            }
        }
    }

    private static bool IsFormat2(string path) =>
        !Format1Paths.Contains(path) && !Format1Paths.Any(f => path.StartsWith(f + ".", StringComparison.Ordinal));

    public static TheoryData<string> Leaves()
    {
        var data = new TheoryData<string>();
        foreach (string path in Paths(Schema, "", groups: false).Where(p => p != "format"))
            data.Add(path);
        return data;
    }

    public static TheoryData<string> Groups()
    {
        var data = new TheoryData<string>();
        foreach (string path in Paths(Schema, "", groups: true).Where(p => NodeAt(p)["properties"] != null && IsFormat2(p) && p is not ("colors" or "card" or "typography" or "gauge")))
            data.Add(path);
        return data;
    }

    private static JsonNode Sample(JsonObject node0)
    {
        var node = Resolve(node0);
        if (node["oneOf"] != null)
            return "ramp";
        if (node["enum"] is JsonArray names)
            return names[0]!.DeepClone();
        double Low() => node["minimum"]!.GetValue<double>() + 0.1 * (node["maximum"]!.GetValue<double>() - node["minimum"]!.GetValue<double>());
        switch (node["type"]!.GetValue<string>())
        {
            case "number":
                return Math.Round(Low(), 4);
            case "integer":
                return node["minimum"]!.GetValue<int>();
            case "boolean":
                return true;
            case "string":
                return node["pattern"] != null ? "#ff8800" : "accent";
            case "array":
                var item = Resolve(node["items"]!.AsObject());
                var list = new JsonArray();
                int count = node["minItems"]?.GetValue<int>() ?? 1;
                for (int i = 0; i < count; i++)
                    list.Add(item["type"]?.GetValue<string>() == "number" ? 0.5 : item["type"]?.GetValue<string>() == "object" ? new JsonObject() : item["enum"] != null ? item["enum"]![i % item["enum"]!.AsArray().Count]!.DeepClone() : item["pattern"] != null ? "#ff8800" : "x");
                return list;
            default:
                var obj = new JsonObject();
                foreach (var required in node["required"]?.AsArray() ?? [])
                    obj[required!.GetValue<string>()] = Sample(node["properties"]![required.GetValue<string>()]!.AsObject());
                return obj;
        }
    }

    /// <summary>A theme that sets the one path to <paramref name="value"/> (and what its groups cannot do without).</summary>
    private static string ThemeWith(string path, JsonNode? value)
    {
        // A border and a gap wide enough that no rule between values is in the way of a valid value.
        var root = new JsonObject { ["card"] = new JsonObject { ["borderWidth"] = 0.02, ["gap"] = 0.2 } };
        var at = root;
        var schemaNode = Schema;
        string[] parts = path.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            var child = Resolve(schemaNode["properties"]![parts[i]]!.AsObject());
            if (i == parts.Length - 1)
            {
                at[parts[i]] = value;
                break;
            }
            at = at[parts[i]] as JsonObject ?? (JsonObject)(at[parts[i]] = new JsonObject());
            foreach (var required in child["required"]?.AsArray() ?? [])
                if (required!.GetValue<string>() != parts[i + 1] && at[required.GetValue<string>()] == null)
                    at[required.GetValue<string>()] = Sample(child["properties"]![required.GetValue<string>()]!.AsObject());
            schemaNode = child;
        }
        return root.ToJsonString();
    }

    /// <summary>Values the key refuses: out of range, wrong name, wrong kind.</summary>
    private static IEnumerable<JsonNode?> Invalid(JsonObject node0)
    {
        var node = Resolve(node0);
        if (node["oneOf"] != null)
        {
            yield return "rainbow";
            yield return 5;
            yield break;
        }
        if (node["enum"] != null)
        {
            yield return "bogus";
            yield return 5;
            yield break;
        }
        switch (node["type"]!.GetValue<string>())
        {
            case "number":
                yield return node["maximum"]!.GetValue<double>() + 1;
                yield return "text";
                break;
            case "integer":
                yield return node["maximum"]!.GetValue<int>() + 1;
                yield return 1.5;
                yield return "text";
                break;
            case "boolean":
                yield return "yes";
                break;
            case "string":
                if (node["pattern"] != null)
                    yield return "red";
                yield return 5;
                break;
            case "array":
                var item = Resolve(node["items"]!.AsObject());
                yield return "text";
                yield return new JsonArray(5);
                if (item["pattern"] != null)
                {
                    yield return new JsonArray("#ff8800", "red");
                    yield return new JsonArray();
                }
                break;
            default:
                yield return 5;
                yield return "text";
                break;
        }
    }

    [Theory]
    [MemberData(nameof(Leaves))]
    public void EveryKey_AcceptsAValidValue_AndKeepsIt(string path)
    {
        if (!IsFormat2(path) && path != "gauge.style")
            return;

        var loaded = Load(ThemeWith(path, path == "gauge.style" ? "tach" : Sample(NodeAt(path))));

        Assert.Empty(ThemeLines);
        Assert.NotNull(Dig(loaded.Definition, path));
    }

    [Theory]
    [MemberData(nameof(Leaves))]
    public void EveryKey_RefusesAnInvalidValue_WithOneWarning_AndTakesTheDefault(string path)
    {
        if (!IsFormat2(path) && path != "gauge.style")
            return;

        var node = NodeAt(path);
        foreach (var bad in path == "gauge.style" ? ["plain"] : Invalid(node))
        {
            ClearLog();
            var loaded = Load(ThemeWith(path, bad));

            string[] lines = ThemeLines;
            Assert.True(lines.Length == 1, $"{path} = {bad?.ToJsonString()} gave {lines.Length} warnings: {string.Join(" | ", lines)}");
            Assert.Contains(path, lines[0], StringComparison.Ordinal);
            Assert.Equal(Dig(Load("{}").Definition, path), Dig(loaded.Definition, path));
        }
    }

    [Theory]
    [MemberData(nameof(Groups))]
    public void EveryGroup_RefusesAValueThatIsNotAnObject(string path)
    {
        var loaded = Load(ThemeWith(path, 5));

        string[] lines = ThemeLines;
        Assert.Single(lines);
        Assert.Contains(path, lines[0], StringComparison.Ordinal);
        Assert.Null(Dig(loaded.Definition, path));
    }

    [Fact]
    public void ATheme_WithEveryFormat2ValueInvalid_LoadsTheDefaultsAndWarnsOncePerKey()
    {
        var root = new JsonObject();
        var leaves = Paths(Schema, "", groups: false).Where(p => p != "format" && IsFormat2(p)).ToList();
        foreach (string path in leaves)
        {
            var bad = Invalid(NodeAt(path)).First();
            var at = root;
            string[] parts = path.Split('.');
            foreach (string part in parts[..^1])
                at = at[part] as JsonObject ?? (JsonObject)(at[part] = new JsonObject());
            at[parts[^1]] = bad;
        }

        var loaded = Load(root.ToJsonString());
        var dark = new ThemeStore(Path.Combine(_root, "themes")).Load("default-dark");

        Assert.Equal(leaves.Count, ThemeLines.Length);
        foreach (string path in leaves)
            Assert.Null(Dig(loaded.Definition, path));
        Assert.Equal(dark.Definition.Colors!.Accent, loaded.Definition.Colors!.Accent);
        Assert.Equal(dark.Definition.Card!.Radius, loaded.Definition.Card!.Radius);
    }

    // ---------------------------------------------------------------- format

    [Fact]
    public void Format_IsOptional_AndAFormat2ThemeLoadsWithoutIt()
    {
        var plain = Load("""{ "card": { "shadow": { "blur": 0.1 } } }""");
        var declared = Load("""{ "format": 2 }""");

        Assert.Null(plain.Definition.Format);
        Assert.Equal(0.1, plain.Definition.Card!.Shadow!.Blur);
        Assert.Equal(2, declared.Definition.Format);
        Assert.Empty(ThemeLines);
    }

    [Fact]
    public void ANewerFormat_LoadsWithOneWarning()
    {
        var loaded = Load("""{ "format": 3, "colors": { "accent": "#ff00ff" }, "futureGroup": { "x": 1 } }""");

        string line = Assert.Single(ThemeLines);
        Assert.Contains("format 3", line, StringComparison.Ordinal);
        Assert.Contains("supports 2", line, StringComparison.Ordinal);
        Assert.Equal("#ff00ff", loaded.Definition.Colors!.Accent);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2.5")]
    [InlineData("\"two\"")]
    public void AFormatThatIsNotAWholeNumberOfAtLeastOne_IsDroppedWithAWarning(string value)
    {
        var loaded = Load("{ \"format\": " + value + " }");

        Assert.Single(ThemeLines);
        Assert.Null(loaded.Definition.Format);
    }

    [Fact]
    public void AKeyTheHostDoesNotKnow_IsIgnoredWithoutAWarning()
    {
        var loaded = Load("""{ "card": { "blur": 0.2, "shadow": { "blur": 0.1, "spread2": 3 } }, "typography": { "fonts": { "body": "x.ttf" } }, "somethingNew": [1] }""");

        Assert.Empty(ThemeLines);
        Assert.Equal(0.1, loaded.Definition.Card!.Shadow!.Blur);
    }

    // ---------------------------------------------------------------- merge

    /// <summary><paramref name="allowMeta"/>: the built-in themes carry <c>meta</c> (a catalog lists them by it) and nothing else of format 2.</summary>
    private static void AssertNoFormat2Groups(ThemeDefinition d, bool allowMeta = false)
    {
        Assert.Null(d.Format);
        if (!allowMeta)
            Assert.Null(d.Meta);
        Assert.Null(d.Pattern);
        Assert.Null(d.Divider);
        Assert.Null(d.Chip);
        Assert.Null(d.Well);
        Assert.Null(d.Icons);
        Assert.Null(d.Background);
        Assert.Null(d.Motion);
        foreach (string path in Paths(Schema, "", groups: false).Where(p => p != "format" && IsFormat2(p) && !(allowMeta && p.StartsWith("meta.", StringComparison.Ordinal))))
            Assert.Null(Dig(d, path));
    }

    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    public void TheBuiltInThemes_LoadCleanWithOnlyMetaOfFormat2(string name)
    {
        var theme = new ThemeStore(Path.Combine(_root, "themes")).Load(name);

        Assert.Equal(name, theme.Name);
        AssertNoFormat2Groups(theme.Definition, allowMeta: true);
        Assert.False(string.IsNullOrWhiteSpace(theme.Definition.Meta?.Name));
        Assert.False(string.IsNullOrWhiteSpace(theme.Definition.Meta?.Description));
        Assert.NotNull(theme.Definition.Meta?.Mode);
        Assert.Empty(Log);
    }

    [Fact]
    public void AFormat1Theme_MergedOverTheDefault_HasNoFormat2Group()
    {
        var loaded = Load("""{ "colors": { "accent": "#ff00ff" }, "card": { "radius": 0.2, "borderWidth": 0.01 }, "gauge": { "style": "bar" } }""");

        AssertNoFormat2Groups(loaded.Definition);
        Assert.Empty(ThemeLines);
        Assert.Equal("", Log);
    }

    private static ThemeDefinition BaselineWithEverything() => new()
    {
        Colors = new ThemeColorsDefinition { Accent2 = "#111111", Ink = "#222222", Ramp = ["#000000", "#ffffff"], CardTints = ["#333333"] },
        Card = new ThemeCardDefinition
        {
            Shape = "chamfer",
            Shadow = new ThemeCardShadowDefinition { Blur = 0.5 },
            Compact = new ThemeCardCompactDefinition { Radius = 0.1 },
        },
        Typography = new ThemeTypographyDefinition
        {
            ValueScale = 1.2,
            Fonts = new ThemeTypographyFontsDefinition { Value = "a.ttf" },
            ValueFill = new ThemeTypographyValueFillDefinition { Colors = ["#000000", "#ffffff"] },
        },
        Gauge = new ThemeGaugeDefinition { Sweep = new ThemeGaugeSweepDefinition { Span = 90 }, ColorBy = "ramp" },
        Pattern = new ThemePatternDefinition { Kind = "grid" },
        Divider = new ThemeDividerDefinition { Style = "gradient", Color = "#444444" },
        Motion = new ThemeMotionDefinition { Level = "reduced" },
        Icons = new ThemeIconsDefinition { Set = "outline" },
        Background = new ThemeBackgroundDefinition { Default = new ThemeBackgroundDefaultDefinition { Name = "nebula" } },
        Meta = new ThemeMetaDefinition { Name = "Other" },
    };

    [Fact]
    public void Merge_NeverTakesTheListedGroupsFromTheBaseline_ButInheritsTheOthers()
    {
        var merged = new ThemeDefinition { Divider = new ThemeDividerDefinition { Thickness = 0.02 } }.MergeOver(BaselineWithEverything());

        // Not inherited: nothing of these appears.
        Assert.Null(merged.Colors!.Ramp);
        Assert.Null(merged.Colors.CardTints);
        Assert.Null(merged.Card!.Shape);
        Assert.Null(merged.Card.Shadow);
        Assert.Null(merged.Card.Compact);
        Assert.Null(merged.Typography!.ValueFill);
        Assert.Null(merged.Gauge!.Sweep);
        Assert.Null(merged.Gauge.ColorBy);
        Assert.Null(merged.Pattern);
        Assert.Null(merged.Icons);
        Assert.Null(merged.Background);
        Assert.Null(merged.Meta);

        // Inherited, member by member.
        Assert.Equal("#111111", merged.Colors.Accent2);
        Assert.Equal("#222222", merged.Colors.Ink);
        Assert.Equal(1.2, merged.Typography.ValueScale);
        Assert.Equal("a.ttf", merged.Typography.Fonts!.Value);
        Assert.Equal("reduced", merged.Motion!.Level);
        Assert.Equal("gradient", merged.Divider!.Style);
        Assert.Equal(0.02, merged.Divider.Thickness);
        Assert.Equal("#444444", merged.Divider.Color);
    }

    [Fact]
    public void Merge_ANestedGroupInheritsMemberByMember()
    {
        var baseline = new ThemeDefinition { Divider = new ThemeDividerDefinition { Color = "#444444", Thickness = 0.01 } };

        var merged = new ThemeDefinition { Divider = new ThemeDividerDefinition { Thickness = 0.03 } }.MergeOver(baseline);

        Assert.Equal("#444444", merged.Divider!.Color);
        Assert.Equal(0.03, merged.Divider.Thickness);
    }

    [Fact]
    public void APartialGroup_KeepsOnlyWhatTheFileSays()
    {
        var loaded = Load("""{ "card": { "shadow": { "blur": 0.2, "color": "#80000000" }, "compact": { "shape": "circle" } } }""");

        var shadow = loaded.Definition.Card!.Shadow!;
        Assert.Equal(0.2, shadow.Blur);
        Assert.Equal("#80000000", shadow.Color);
        Assert.Null(shadow.X);
        Assert.Null(shadow.Spread);
        Assert.Equal("circle", loaded.Definition.Card.Compact!.Shape);
        Assert.Null(loaded.Definition.Card.Compact.Radius);
        Assert.Null(loaded.Definition.Card.Glow);
        Assert.Null(loaded.Definition.Pattern);
        Assert.Empty(ThemeLines);
    }

    // ---------------------------------------------------------------- reading

    [Fact]
    public void ANameIsReadInAnyCase()
    {
        var loaded = Load("""{ "card": { "borderStyle": "Dashed", "borderWidth": 0.01, "cornerMarks": { "style": "BRACKET" } } }""");

        Assert.Empty(ThemeLines);
        Assert.Equal("dashed", loaded.Definition.Card!.BorderStyle);
        Assert.Equal("bracket", loaded.Definition.Card.CornerMarks!.Style);
    }

    [Theory]
    [InlineData("""{ "typography": { "opticalSize": "auto" } }""", "auto")]
    [InlineData("""{ "typography": { "opticalSize": "off" } }""", "off")]
    [InlineData("""{ "typography": { "opticalSize": "Auto" } }""", "auto")]
    [InlineData("""{ "typography": { } }""", null)]
    public void OpticalSize_IsReadInAnyCase_AndAbsentIsNull(string json, string? expected)
    {
        var loaded = Load(json);

        Assert.Empty(ThemeLines);
        Assert.Equal(expected, loaded.Definition.Typography!.OpticalSize);
    }

    [Theory]
    [InlineData("\"large\"", "large")]
    [InlineData("5", "5")]
    public void OpticalSize_OfAnUnknownValueOrKind_IsClearedWithOneWarning(string value, string shown)
    {
        var loaded = Load($$"""{ "typography": { "opticalSize": {{value}} } }""");

        string line = Assert.Single(ThemeLines);
        Assert.Contains("typography.opticalSize", line, StringComparison.Ordinal);
        Assert.Contains(shown, line, StringComparison.Ordinal);
        Assert.Null(loaded.Definition.Typography!.OpticalSize);
    }

    [Fact]
    public void ADeferredEnumerationValue_IsRefused()
    {
        var loaded = Load("""{ "card": { "borderStyle": "dotted", "borderWidth": 0.01, "cornerMarks": { "style": "cross" } }, "pattern": { "kind": "lines" }, "chip": { "shape": "rect" } }""");

        Assert.Equal(4, ThemeLines.Length);
        Assert.Null(loaded.Definition.Card!.BorderStyle);
        Assert.Null(loaded.Definition.Pattern!.Kind);
    }

    [Fact]
    public void OneBadColourInAnArray_DropsTheArrayWhole()
    {
        var loaded = Load("""{ "colors": { "cardTints": ["#ff0000", "nope", "#00ff00"], "accent2": "#abcdef" } }""");

        Assert.Single(ThemeLines);
        Assert.Null(loaded.Definition.Colors!.CardTints);
        Assert.Equal("#abcdef", loaded.Definition.Colors.Accent2);
    }

    [Fact]
    public void Corners_DropUnknownNamesAndDuplicates()
    {
        var loaded = Load("""{ "card": { "cutCorners": ["TR", "tr", "middle", "bl"] } }""");

        Assert.Single(ThemeLines);
        Assert.Equal(["tr", "bl"], loaded.Definition.Card!.CutCorners);
    }

    [Fact]
    public void NoCornersAtAll_IsASquareCard()
    {
        var loaded = Load("""{ "card": { "shape": "chamfer", "cutCorners": [] } }""");

        Assert.Empty(ThemeLines);
        Assert.Empty(loaded.Definition.Card!.CutCorners!);
    }

    [Fact]
    public void AGradientWithoutColours_IsDroppedWithOneWarning()
    {
        var loaded = Load("""{ "card": { "borderWidth": 0.02, "borderGradient": { "kind": "conic" } } }""");

        string line = Assert.Single(ThemeLines);
        Assert.Contains("card.borderGradient", line, StringComparison.Ordinal);
        Assert.Null(loaded.Definition.Card!.BorderGradient);
    }

    [Fact]
    public void TheGaugeGradient_IsRampOrStops()
    {
        var ramp = Load("""{ "gauge": { "gradient": "ramp" } }""");
        var stops = Load("""{ "gauge": { "gradient": { "colors": ["#ff0000", "#00ff00"], "angle": 30 } } }""");

        Assert.True(ramp.Definition.Gauge!.Gradient!.Ramp);
        Assert.Equal(["#ff0000", "#00ff00"], stops.Definition.Gauge!.Gradient!.Stops!.Colors);
        Assert.Empty(ThemeLines);
    }

    [Theory]
    [InlineData("accent")]
    [InlineData("TEXT")]
    [InlineData("none")]
    [InlineData("#ff8800")]
    public void AnIconTint_IsANameOrAColour(string tint)
    {
        var loaded = Load("{ \"icons\": { \"tint\": \"" + tint + "\" } }");

        Assert.Empty(ThemeLines);
        Assert.NotNull(loaded.Definition.Icons!.Tint);
    }

    [Fact]
    public void Metadata_IsKept_AndTheThemeReportsItsName()
    {
        var described = Load("""{ "meta": { "name": "Carbon", "mode": "dark", "tags": ["a"], "pairsWith": { "backgrounds": ["nebula", "nothing-here"] }, "samples": [{ "file": "a.png", "caption": "A" }] } }""");
        var bare = Load("{}", "mine");

        Assert.Empty(ThemeLines);
        Assert.Equal("Carbon", described.DisplayName);
        Assert.Equal("dark", described.Mode);
        Assert.Equal(["nebula", "nothing-here"], described.Definition.Meta!.PairsWith!.Backgrounds);
        Assert.Equal("a.png", described.Definition.Meta.Samples![0].File);
        Assert.Equal("mine", bare.DisplayName);
        Assert.Null(bare.Mode);
        Assert.Null(bare.Definition.Meta);
    }

    [Theory]
    [InlineData("meter")]
    [InlineData("tach")]
    [InlineData("marker")]
    [InlineData("thickbar")]
    [InlineData("THICKBAR")]
    public void AFormat2GaugeStyle_IsKept_AndTheResolverGivesIt(string style)
    {
        var loaded = Load("{ \"gauge\": { \"style\": \"" + style + "\" } }");
        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Empty(ThemeLines);
        Assert.Equal(style, loaded.Definition.Gauge!.Style);
        Assert.Equal(Enum.Parse<GaugeStyle>(style, ignoreCase: true), theme.GaugeStyle);
    }

    [Fact]
    public void AFormat1Value_OfTheWrongKind_StillFailsTheWholeTheme()
    {
        var loaded = Load("""{ "card": { "radius": "big" }, "colors": { "accent": "#ff00ff" } }""");

        Assert.Contains("could not be read", Log, StringComparison.Ordinal);
        Assert.NotEqual("#ff00ff", loaded.Definition.Colors!.Accent);
    }

    // ---------------------------------------------------------------- rules between values

    [Theory]
    [InlineData("\"borderStyle\": \"dashed\"")]
    [InlineData("\"borderGradient\": { \"colors\": [\"#ff0000\", \"#00ff00\"] }")]
    [InlineData("\"borderAnimate\": { \"mode\": \"pulse\" }")]
    public void ABorderStyleGradientOrAnimation_WithNoBorder_WarnsOnce(string card)
    {
        var loaded = Load("{ \"card\": { " + card + " } }");

        string line = Assert.Single(ThemeLines);
        Assert.Contains("borderWidth 0", line, StringComparison.Ordinal);
        Assert.NotNull(loaded.Definition.Card);
    }

    [Fact]
    public void SeveralBorderValues_WithNoBorder_AreOneWarning()
    {
        Load("""{ "card": { "borderStyle": "dashed", "borderGradient": { "colors": ["#ff0000", "#00ff00"] } } }""");

        Assert.Single(ThemeLines);
    }

    [Fact]
    public void ABorderStyleNone_NeedsNoBorder()
    {
        Load("""{ "card": { "borderStyle": "none" } }""");

        Assert.Empty(ThemeLines);
    }

    [Fact]
    public void ASpinningBorder_WithoutAConicGradient_IsSwitchedOff()
    {
        var linear = Load("""{ "card": { "borderWidth": 0.01, "borderGradient": { "colors": ["#ff0000", "#00ff00"], "kind": "linear" }, "borderAnimate": { "mode": "spin" } } }""");

        Assert.Single(ThemeLines);
        Assert.Null(linear.Definition.Card!.BorderAnimate);
        Assert.NotNull(linear.Definition.Card.BorderGradient);

        ClearLog();
        var none = Load("""{ "card": { "borderWidth": 0.01, "borderAnimate": { "mode": "spin", "period": 5 } } }""");

        Assert.Single(ThemeLines);
        Assert.Null(none.Definition.Card!.BorderAnimate);
    }

    [Fact]
    public void ASpinningBorder_WithAConicGradient_IsKept()
    {
        var loaded = Load("""{ "card": { "borderWidth": 0.01, "borderGradient": { "colors": ["#ff0000", "#00ff00"], "kind": "conic" }, "borderAnimate": { "mode": "spin", "period": 5 } } }""");

        Assert.Empty(ThemeLines);
        Assert.Equal("spin", loaded.Definition.Card!.BorderAnimate!.Mode);
    }

    [Fact]
    public void APatternWhoseMarksRunTogether_WarnsOnce()
    {
        Load("""{ "pattern": { "kind": "grid", "spacing": 0.02, "thickness": 0.01 } }""");
        Assert.Single(ThemeLines);

        ClearLog();
        Load("""{ "pattern": { "kind": "grid", "spacing": 0.13, "thickness": 0.01 } }""");
        Assert.Empty(ThemeLines);

        ClearLog();
        Load("""{ "pattern": { "kind": "none", "spacing": 0.02, "thickness": 0.01 } }""");
        Assert.Empty(ThemeLines);
    }

    [Fact]
    public void CornerMarksOutsideTheCard_WarnWhenTheGapIsTooSmall()
    {
        Load("""{ "card": { "gap": 0.02, "cornerMarks": { "style": "bracket", "inset": -0.05 } } }""");
        Assert.Single(ThemeLines);

        ClearLog();
        Load("""{ "card": { "gap": 0.09, "cornerMarks": { "style": "bracket", "inset": -0.05 } } }""");
        Assert.Empty(ThemeLines);

        ClearLog();
        Load("""{ "card": { "gap": 0.02, "cornerMarks": { "style": "bracket", "inset": 0.02 } } }""");
        Assert.Empty(ThemeLines);
    }

    [Fact]
    public void AChamferWithACircleOnCompactCards_IsAllowed()
    {
        var loaded = Load("""{ "card": { "shape": "chamfer", "compact": { "shape": "circle" } } }""");

        Assert.Empty(ThemeLines);
        Assert.Equal("chamfer", loaded.Definition.Card!.Shape);
        Assert.Equal("circle", loaded.Definition.Card.Compact!.Shape);
    }

    [Fact]
    public void MoreThanOneContinuousAnimation_IsAnInformationLine_AndOneOrNoneIsNothing()
    {
        var check = new ThemeValueCheck(Name);

        ThemeDefinition.NoteAnimations(check, 1);
        Assert.Empty(ThemeLines);

        ThemeDefinition.NoteAnimations(check, 2);
        string line = Assert.Single(ThemeLines);
        Assert.Contains("INFO", line, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- the model and the schema

    [Fact]
    public void TheModel_AndTheSchema_DescribeTheSameKeys()
    {
        var missingFromModel = new List<string>();
        var missingFromSchema = new List<string>();
        Compare(typeof(ThemeDefinition), Schema, "", missingFromModel, missingFromSchema);

        Assert.True(missingFromModel.Count == 0, "the schema has keys the model lacks: " + string.Join(", ", missingFromModel));
        Assert.True(missingFromSchema.Count == 0, "the model has keys the schema lacks: " + string.Join(", ", missingFromSchema));
    }

    private static void Compare(Type type, JsonObject schemaNode, string path, List<string> missingFromModel, List<string> missingFromSchema)
    {
        var properties = schemaNode["properties"]?.AsObject() ?? [];
        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType == type)
            .ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]);
        foreach (var (key, child0) in properties)
        {
            if (key == "$schema")
                continue;
            string full = path.Length == 0 ? key : path + "." + key;
            if (!members.TryGetValue(key, out var member))
            {
                missingFromModel.Add(full);
                continue;
            }
            var child = Resolve(child0!.AsObject());
            var memberType = Nullable.GetUnderlyingType(member.PropertyType) ?? member.PropertyType;
            if (child["type"]?.GetValue<string>() == "object" && child["properties"] != null)
                Compare(memberType, child, full, missingFromModel, missingFromSchema);
            else if (child["type"]?.GetValue<string>() == "array" && Resolve(child["items"]!.AsObject()) is { } item && item["type"]?.GetValue<string>() == "object")
                Compare(memberType.GetGenericArguments()[0], item, full + "[]", missingFromModel, missingFromSchema);
            else if (child["oneOf"] != null)
                Assert.Equal(typeof(ThemeGaugeGradientDefinition), memberType);
            else
                Assert.True(IsSimple(memberType), $"{full} is {memberType.Name}, which is not a plain value, but the schema says it is");
        }
        foreach (string name in members.Keys.Where(k => !properties.ContainsKey(k)))
            missingFromSchema.Add(path.Length == 0 ? name : path + "." + name);
    }

    private static bool IsSimple(Type type) =>
        type == typeof(string) || type == typeof(double) || type == typeof(int) || type == typeof(bool)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && IsSimple(type.GetGenericArguments()[0]));
}
