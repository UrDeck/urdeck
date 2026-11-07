// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Collections.Immutable;
using System.Linq;
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
    public const string IdUndeclaredSetting = "URDECK006";

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

    private static readonly DiagnosticDescriptor RuleUndeclaredSetting = new DiagnosticDescriptor(
        IdUndeclaredSetting, "Undeclared setting",
        "'{0}' is not a declared setting: the editor cannot show it. Add [Setting], or [JsonIgnore] if it is not user-facing.",
        "Usage", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor RuleKindlessSetting = new DiagnosticDescriptor(
        IdUndeclaredSetting, "Setting without a kind",
        "'{0}' is a declared setting whose kind cannot be taken from its type: add a kind attribute ([Slider], [Toggle], [Number], [Positions] ...) or [Option]s",
        "Usage", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        RuleWidget, RuleWidgetSize, RuleNoRefresh, RuleMultipleRefresh, RuleInvalidSize, RuleUndeclaredSetting, RuleKindlessSetting);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSettings, SymbolKind.NamedType);
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

    private static bool Is(INamedTypeSymbol? cls, INamedTypeSymbol? expected) =>
        cls != null && expected != null && SymbolEqualityComparer.Default.Equals(cls, expected);

    private static void AnalyzeSettings(SymbolAnalysisContext ctx)
    {
        var type = (INamedTypeSymbol)ctx.Symbol;
        if (type.TypeKind != TypeKind.Class)
            return;

        var compilation = ctx.Compilation;
        var configBase = compilation.GetTypeByMetadataName(AttrNs + "WidgetConfig");
        var settingsBase = compilation.GetTypeByMetadataName(AttrNs + "Data.ProviderSettings");
        if (!Derives(type, configBase) && !Derives(type, settingsBase))
            return;

        var names = new SettingNames(
            compilation.GetTypeByMetadataName(AttrNs + "Settings.SettingAttribute"),
            compilation.GetTypeByMetadataName(AttrNs + "Settings.SettingPartAttribute"),
            compilation.GetTypeByMetadataName(AttrNs + "Settings.SettingKindAttribute"),
            compilation.GetTypeByMetadataName(AttrNs + "Settings.OptionAttribute"),
            compilation.GetTypeByMetadataName(AttrNs + "Settings.PositionsAttribute"),
            compilation.GetTypeByMetadataName(AttrNs + "Settings.SecretAttribute"),
            compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonIgnoreAttribute"),
            compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonExtensionDataAttribute"),
            compilation.GetTypeByMetadataName("System.Text.Json.JsonElement"));
        if (names.Setting == null)
            return;

        CheckProperties(ctx, type, names);
        foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (names.Positions != null && member.GetAttributes().Any(a => Is(a.AttributeClass, names.Positions))
                && member.Type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } list
                && list.TypeArguments[0] is INamedTypeSymbol item && item.Locations.Any(l => l.IsInSource))
            {
                CheckProperties(ctx, item, names);
            }
        }
    }

    private static bool Derives(INamedTypeSymbol type, INamedTypeSymbol? baseType)
    {
        if (baseType == null)
            return false;
        for (var b = type.BaseType; b != null; b = b.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(b, baseType))
                return true;
        }
        return false;
    }

    private static void CheckProperties(SymbolAnalysisContext context, INamedTypeSymbol type, SettingNames names)
    {
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public
                || property.SetMethod == null || property.SetMethod.DeclaredAccessibility != Accessibility.Public
                || property.Locations.Length == 0 || !property.Locations[0].IsInSource)
                continue;

            var attributes = property.GetAttributes();
            if (attributes.Any(a => Is(a.AttributeClass, names.SettingPart) || Is(a.AttributeClass, names.JsonIgnore) || Is(a.AttributeClass, names.JsonExtensionData)))
                continue;

            if (!attributes.Any(a => Is(a.AttributeClass, names.Setting)))
            {
                context.ReportDiagnostic(Diagnostic.Create(RuleUndeclaredSetting, property.Locations[0], property.Name));
                continue;
            }

            bool hasKind = attributes.Any(a => DerivesFromAttribute(a.AttributeClass, names.Kind)
                || Is(a.AttributeClass, names.Option) || Is(a.AttributeClass, names.Positions) || Is(a.AttributeClass, names.Secret));
            if (hasKind)
                continue;

            var propertyType = property.Type;
            if (propertyType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                && nullable.TypeArguments.Length == 1)
                propertyType = nullable.TypeArguments[0];

            bool isJsonElement = names.JsonElement != null && SymbolEqualityComparer.Default.Equals(propertyType, names.JsonElement);
            bool isList = propertyType is INamedTypeSymbol { IsGenericType: true } generic
                && generic.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>";
            if (isJsonElement || isList)
                context.ReportDiagnostic(Diagnostic.Create(RuleKindlessSetting, property.Locations[0], property.Name));
        }
    }

    private static bool DerivesFromAttribute(INamedTypeSymbol? attribute, INamedTypeSymbol? baseType)
    {
        if (baseType == null)
            return false;
        for (var b = attribute; b != null; b = b.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(b, baseType))
                return true;
        }
        return false;
    }

    private sealed class SettingNames
    {
        public SettingNames(INamedTypeSymbol? setting, INamedTypeSymbol? settingPart, INamedTypeSymbol? kind, INamedTypeSymbol? option,
            INamedTypeSymbol? positions, INamedTypeSymbol? secret, INamedTypeSymbol? jsonIgnore, INamedTypeSymbol? jsonExtensionData,
            INamedTypeSymbol? jsonElement)
        {
            Setting = setting;
            SettingPart = settingPart;
            Kind = kind;
            Option = option;
            Positions = positions;
            Secret = secret;
            JsonIgnore = jsonIgnore;
            JsonExtensionData = jsonExtensionData;
            JsonElement = jsonElement;
        }

        public INamedTypeSymbol? Setting { get; }

        public INamedTypeSymbol? SettingPart { get; }

        public INamedTypeSymbol? Kind { get; }

        public INamedTypeSymbol? Option { get; }

        public INamedTypeSymbol? Positions { get; }

        public INamedTypeSymbol? Secret { get; }

        public INamedTypeSymbol? JsonIgnore { get; }

        public INamedTypeSymbol? JsonExtensionData { get; }

        public INamedTypeSymbol? JsonElement { get; }
    }
}
