// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Settings;
using UrDeck.Sdk.Settings;

namespace UrDeck.Engine.Backgrounds;

/// <summary>
/// A shader's parameters as settings an editor form can show: one descriptor per visible single-number parameter, from the
/// shader's declaration (the default) and its manifest (label, help, range, step, unit). Colour parameters and vectors have
/// no control yet and are left out; the parameters are keys of the <c>background</c> object.
/// </summary>
public static class ManifestSettings
{
    /// <summary>The settings of <paramref name="package"/>, in declaration order. Empty when the shader cannot be read.</summary>
    public static IReadOnlyList<SettingDescriptor> From(BackgroundPackage package)
    {
        var declarations = ShaderDeclarations.Parse(package.EntryText, package.Name);
        var settings = new List<SettingDescriptor>();
        int order = 0;
        foreach (var parameter in BackgroundParameter.Join(declarations, package.Manifest))
        {
            order++;
            if (parameter.Hidden || parameter.Kind != ParameterKind.Number || parameter.Components != 1)
                continue;

            settings.Add(Describe(parameter, order));
        }

        return settings;
    }

    /// <summary>The settings of the background named <paramref name="name"/>; empty when it is not found.</summary>
    public static IReadOnlyList<SettingDescriptor> From(ShaderStore store, string name) =>
        store.TryLoad(name).Package is { } package ? From(package) : [];

    private static SettingDescriptor Describe(BackgroundParameter parameter, int order)
    {
        double? fallback = parameter.Default is { Length: > 0 } floats ? Math.Round(floats[0], 4) : null;
        bool ranged = parameter.Min is not null && parameter.Max is not null && parameter.Max > parameter.Min;
        double step = parameter.Step is > 0 ? parameter.Step.Value
            : ranged ? Math.Round((parameter.Max!.Value - parameter.Min!.Value) / 100, 4) : 0;
        var number = new NumberSpec(parameter.Min, parameter.Max, step, parameter.Unit, null, false);

        return new SettingDescriptor(
            parameter.Name,
            ranged ? SettingKinds.Range : SettingKinds.Number,
            string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label,
            parameter.Description ?? "",
            null,
            order,
            false,
            null,
            null,
            new Fallback(null, FallbackSource.None, null),
            false,
            [],
            number,
            null,
            null,
            null,
            null,
            true,
            null,
            null,
            null,
            fallback);
    }
}
