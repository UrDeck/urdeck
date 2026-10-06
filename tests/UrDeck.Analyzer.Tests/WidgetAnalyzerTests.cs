// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace UrDeck.Analyzer.Tests;

public class WidgetAnalyzerTests
{
    private const string Usings = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using UrDeck.Sdk;
public class Cfg : WidgetConfig { }
";

    private const string Body = @"
{
    public override void Render(WidgetRenderContext context) { }
}";

    private const string Widget = "[Widget(\"a\", \"b\")]";
    private const string Size = "[WidgetSize(2, 1)]";
    private const string Tick = "[RefreshOnTick(1, TimeUnit.Seconds)]";

    private static string[] Run(string attrs, string decl = "public class W : Widget<Cfg>", string? extra = null)
    {
        string source = Usings + (extra ?? "") + attrs + "\n" + decl + Body;
        var tree = CSharpSyntaxTree.ParseText(source);

        var refs = new List<MetadataReference>();
        string tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (string p in tpa.Split(Path.PathSeparator))
            refs.Add(MetadataReference.CreateFromFile(p));
        refs.Add(MetadataReference.CreateFromFile(typeof(UrDeck.Sdk.WidgetAttribute).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(SkiaSharp.SKCanvas).Assembly.Location));

        var compilation = CSharpCompilation.Create("t", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Sanity: no compile errors in the test source itself.
        string[] compileErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
        Assert.Empty(compileErrors);

        var diags = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new WidgetAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        Assert.All(diags, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        return diags.Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void ValidWidget_NoDiagnostics() =>
        Assert.Empty(Run(Widget + Size + Tick));

    [Fact]
    public void MissingWidget_UrDeck001() =>
        Assert.Equal(new[] { "URDECK001" }, Run(Size + Tick));

    [Fact]
    public void MissingSize_UrDeck002() =>
        Assert.Equal(new[] { "URDECK002" }, Run(Widget + Tick));

    [Fact]
    public void RefreshOnData_CountsAsTheRefreshStrategy() =>
        Assert.Empty(Run(Widget + Size + "[RefreshOnData]"));

    [Fact]
    public void MissingRefresh_UrDeck003() =>
        Assert.Equal(new[] { "URDECK003" }, Run(Widget + Size));

    [Fact]
    public void MultipleRefresh_UrDeck004() =>
        Assert.Equal(new[] { "URDECK004" }, Run(Widget + Size + Tick + "[RefreshOnData]"));

    [Theory]
    [InlineData("[WidgetSize(5, 1)]")]
    [InlineData("[WidgetSize(0, 1)]")]
    [InlineData("[WidgetSize(2, 0)]")]
    public void InvalidSize_UrDeck005(string size) =>
        Assert.Equal(new[] { "URDECK005" }, Run(Widget + size + Tick));

    [Fact]
    public void MultipleValidSizes_NoDiagnostics() =>
        Assert.Empty(Run(Widget + Size + "[WidgetSize(4, 3)]" + "[RefreshAdaptive(100, 1000)]"));

    [Fact]
    public void AbstractWidget_NotFlagged() =>
        Assert.Empty(Run("", "public abstract class W : Widget<Cfg>"));

    [Fact]
    public void IndirectSubclass_IsAnalyzed() =>
        Assert.Equal(new[] { "URDECK001", "URDECK002", "URDECK003" },
            Run("", "public class W : Base", "public abstract class Base : Widget<Cfg>" + Body + "\n"));

    [Fact]
    public void NonWidgetClass_NotFlagged() =>
        Assert.Empty(Run(Widget + Size + Tick, "public class W : Widget<Cfg>", "public class Plain { }"));
}
