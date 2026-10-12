// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Backgrounds;

/// <summary>
/// A parameter as a reader of the background sees it: what the shader declares (type, default) joined with what the
/// manifest annotates (label, range, hidden). The shader is the source of truth for which parameters exist.
/// </summary>
public sealed record BackgroundParameter(
    string Name,
    int Components,
    float[]? Default,
    ParameterKind Kind,
    string? Label,
    string? Description,
    double? Min,
    double? Max,
    double? Step,
    string? Unit,
    bool Hidden)
{
    /// <summary>
    /// The parameters of <paramref name="declarations"/> in declaration order, annotated by <paramref name="manifest"/> (which may
    /// be null). The kind is the manifest's when it gives one, else <c>color</c> for a <c>layout(color)</c>, else a number.
    /// The host's inputs and the setting's own keys are not parameters.
    /// </summary>
    public static IReadOnlyList<BackgroundParameter> Join(ShaderDeclarations declarations, BackgroundManifest? manifest)
    {
        var result = new List<BackgroundParameter>();
        foreach (var uniform in declarations.Uniforms)
        {
            if (HostKeys.IsHostKey(uniform.Name))
                continue;

            ManifestParameter? note = null;
            manifest?.Parameters.TryGetValue(uniform.Name, out note);
            var kind = note?.Kind ?? (uniform.IsColor ? ParameterKind.Color : ParameterKind.Number);
            result.Add(new BackgroundParameter(
                uniform.Name, uniform.Components, uniform.Default, kind,
                note?.Label, note?.Description, note?.Min, note?.Max, note?.Step, note?.Unit, note?.Hidden ?? false));
        }

        return result;
    }

    /// <summary>True when the value is inside the range the manifest gives, per component. No range, always true.</summary>
    public bool InRange(float[] value)
    {
        foreach (float component in value)
        {
            if ((Min is { } min && component < min) || (Max is { } max && component > max))
                return false;
        }

        return true;
    }

    /// <summary>The range as written in a message: <c>0 to 3</c>, <c>at least 0</c> or <c>at most 3</c>.</summary>
    public string DescribeRange() => (Min, Max) switch
    {
        ({ } low, { } high) => $"{Format(low)} to {Format(high)}",
        ({ } low, null) => $"at least {Format(low)}",
        (null, { } high) => $"at most {Format(high)}",
        _ => "any",
    };

    private static string Format(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>The names the product owns: the setting's own keys and the inputs the host sets. They are never parameters.</summary>
public static class HostKeys
{
    private static readonly string[] Inputs = ["iTime", "iResolution", "iColor0", "iColor1", "iColor2", "iColor3"];

    /// <summary>True for a name a background cannot use as a parameter: one of the setting's own keys or a host input.</summary>
    public static bool IsHostKey(string name) => IsHostInput(name) || BackgroundSettings.IsOwnKey(name);

    public static bool IsHostInput(string name) => Array.IndexOf(Inputs, name) >= 0;
}
