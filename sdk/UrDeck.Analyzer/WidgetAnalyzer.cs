// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UrDeck.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetAnalyzer : DiagnosticAnalyzer
{
    public const string IdWidget = "URDECK001";
    public const string IdWidgetSize = "URDECK002";
    public const string IdNoRefresh = "URDECK003";
    public const string IdMultipleRefresh = "URDECK004";
    public const string IdInvalidSize = "URDECK005";

    private const string WidgetBaseMetadataName = "UrDeck.Sdk.Widget`1";
    private const string AttrNs = "UrDeck.Sdk.";

    private static readonly DiagnosticDescriptor RuleWidget = new DiagnosticDescriptor(
        IdWidget, "Missing [Widget]", "Widget class '{0}' must have a [Widget] attribute", "Usage", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor RuleWidgetSize = new DiagnosticDescriptor(
        IdWidgetSize, "Missing [WidgetSize]", "Widget class '{0}' must have at least one [WidgetSize] attribute", "Usage", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor RuleNoRefresh = new DiagnosticDescriptor(
        IdNoRefresh, "Missing refresh strategy", "Widget class '{0}' must have a refresh strategy attribute ([RefreshOnTick], [RefreshAdaptive] or [RefreshOnData])", "Usage", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor RuleMultipleRefresh = new DiagnosticDescriptor(
        IdMultipleRefresh, "Conflicting refresh strategies", "Widget class '{0}' must have only one refresh strategy attribute", "Usage", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor RuleInvalidSize = new DiagnosticDescriptor(
        IdInvalidSize, "Invalid widget size", "Width must be between 1 and 4 and height must be at least 1", "Usage", DiagnosticSeverity.Error, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        RuleWidget, RuleWidgetSize, RuleNoRefresh, RuleMultipleRefresh, RuleInvalidSize);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var widgetBase = start.Compilation.GetTypeByMetadataName(WidgetBaseMetadataName);
            if (widgetBase == null)
                return;

            var widgetAttr = start.Compilation.GetTypeByMetadataName(AttrNs + "WidgetAttribute");
            var sizeAttr = start.Compilation.GetTypeByMetadataName(AttrNs + "WidgetSizeAttribute");
            var tick = start.Compilation.GetTypeByMetadataName(AttrNs + "RefreshOnTickAttribute");
            var adaptive = start.Compilation.GetTypeByMetadataName(AttrNs + "RefreshAdaptiveAttribute");
            var onData = start.Compilation.GetTypeByMetadataName(AttrNs + "RefreshOnDataAttribute");

            start.RegisterSymbolAction(
                ctx => AnalyzeType(ctx, widgetBase, widgetAttr, sizeAttr, tick, adaptive, onData),
                SymbolKind.NamedType);
        });
    }

    private static bool DerivesFromWidget(INamedTypeSymbol type, INamedTypeSymbol widgetBase)
    {
        for (var b = type.BaseType; b != null; b = b.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(b.OriginalDefinition, widgetBase))
                return true;
        }
        return false;
    }

    private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol widgetBase,
        INamedTypeSymbol? widgetAttr, INamedTypeSymbol? sizeAttr,
        INamedTypeSymbol? tick, INamedTypeSymbol? adaptive, INamedTypeSymbol? onData)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
            return;
        if (!DerivesFromWidget(type, widgetBase))
            return;

        var location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
        bool hasWidget = false;
        int sizeCount = 0;
        int refreshCount = 0;

        foreach (var attr in type.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls == null)
                continue;

            if (Is(cls, widgetAttr))
                hasWidget = true;
            else if (Is(cls, tick) || Is(cls, adaptive) || Is(cls, onData))
                refreshCount++;
            else if (Is(cls, sizeAttr))
            {
                sizeCount++;
                var args = attr.ConstructorArguments;
                if (args.Length >= 2 && args[0].Value is int w && args[1].Value is int h
                    && (w < 1 || w > 4 || h < 1))
                {
                    var loc = attr.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? location;
                    context.ReportDiagnostic(Diagnostic.Create(RuleInvalidSize, loc));
                }
            }
        }

        if (!hasWidget)
            context.ReportDiagnostic(Diagnostic.Create(RuleWidget, location, type.Name));
        if (sizeCount == 0)
            context.ReportDiagnostic(Diagnostic.Create(RuleWidgetSize, location, type.Name));
        if (refreshCount == 0)
            context.ReportDiagnostic(Diagnostic.Create(RuleNoRefresh, location, type.Name));
        if (refreshCount > 1)
            context.ReportDiagnostic(Diagnostic.Create(RuleMultipleRefresh, location, type.Name));
    }

    private static bool Is(INamedTypeSymbol cls, INamedTypeSymbol? expected) =>
        expected != null && SymbolEqualityComparer.Default.Equals(cls, expected);
}
