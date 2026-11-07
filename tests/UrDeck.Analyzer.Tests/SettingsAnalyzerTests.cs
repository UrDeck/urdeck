// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace UrDeck.Analyzer.Tests;

/// <summary>URDECK006: every setting of a widget configuration, provider settings or list item type is declared.</summary>
public class SettingsAnalyzerTests
{
    private const string Usings = @"
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;
";

    private static string[] Run(string declarations)
    {
        var tree = CSharpSyntaxTree.ParseText(Usings + declarations);

        var refs = new List<MetadataReference>();
        string tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (string p in tpa.Split(Path.PathSeparator))
            refs.Add(MetadataReference.CreateFromFile(p));
        refs.Add(MetadataReference.CreateFromFile(typeof(UrDeck.Sdk.WidgetAttribute).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(SkiaSharp.SKCanvas).Assembly.Location));

        var compilation = CSharpCompilation.Create("t", [tree], refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var diagnostics = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new WidgetAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        return [.. diagnostics.Select(d => d.Id + ":" + d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).OrderBy(x => x, StringComparer.Ordinal)];
    }

    [Fact]
    public void An_undeclared_public_property_warns_and_names_it() =>
        Assert.Collection(
            Run("public class C : WidgetConfig { public int Speed { get; set; } }"),
            d => { Assert.StartsWith("URDECK006:", d); Assert.Contains("'Speed'", d); });

    [Fact]
    public void A_declared_setting_a_part_and_excluded_properties_do_not_warn() =>
        Assert.Empty(Run(@"
public class C : WidgetConfig
{
    [Setting(""Speed"")] public int Speed { get; set; }
    [SettingPart(""speed"")] public double? Part { get; set; }
    [JsonIgnore] public int Hidden { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    public int ReadOnly { get; }
    public int Private { get; private set; }
    public static int Shared { get; set; }
}"));

    [Fact]
    public void Properties_inherited_from_the_sdk_base_do_not_warn() =>
        Assert.Empty(Run("public class C : WidgetConfig { }"));

    [Fact]
    public void A_provider_settings_type_is_checked_too() =>
        Assert.Single(Run("public class S : ProviderSettings { public string? Url { get; set; } }"));

    [Fact]
    public void A_list_item_type_is_checked() =>
        Assert.Collection(
            Run(@"
public class Slot { public string Reading { get; set; } = """"; [Setting(""Label"")] public string? Label { get; set; } }
public class C : WidgetConfig { [Setting(""Slots"")] [Positions(""2x2"", ""a"")] public List<Slot> Slots { get; set; } = []; }"),
            d => Assert.Contains("'Reading'", d));

    [Fact]
    public void A_json_element_setting_without_a_kind_warns() =>
        Assert.Collection(
            Run("public class C : WidgetConfig { [Setting(\"Days\")] public JsonElement? Days { get; set; } }"),
            d => { Assert.StartsWith("URDECK006:", d); Assert.Contains("'Days'", d); });

    [Fact]
    public void A_json_element_setting_with_a_kind_or_options_does_not_warn() =>
        Assert.Empty(Run(@"
public class C : WidgetConfig
{
    [Setting(""Days"")] [Slider(1, 7)] public JsonElement? Days { get; set; }
    [Setting(""Animate"")] [Toggle] public JsonElement? Animate { get; set; }
    [Setting(""Mode"")] [Option(""a"", ""A"")] public JsonElement? Mode { get; set; }
}"));

    [Fact]
    public void A_list_setting_without_positions_warns() =>
        Assert.Single(Run("public class C : WidgetConfig { [Setting(\"Items\")] public List<string> Items { get; set; } = []; }"));

    [Fact]
    public void A_class_that_is_not_a_settings_type_is_left_alone() =>
        Assert.Empty(Run("public class Plain { public int Speed { get; set; } }"));
}
