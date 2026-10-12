// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Backgrounds;

/// <summary>A uniform of a shader: its name, how many floats it has (1 to 4) and its declared default, if any.</summary>
public sealed record ShaderUniform(string Name, int Components, float[]? Default);

/// <summary>
/// What a shader's text declares. SkiaSharp names a compiled effect's uniforms but not their types, so the declarations
/// are read from the source: <c>uniform &lt;type&gt; &lt;name&gt;;   // default: &lt;value&gt;</c>, one per line.
/// </summary>
public sealed partial class ShaderDeclarations
{
    private ShaderDeclarations(List<ShaderUniform> uniforms, List<string> unsupported, bool hasChildShader)
    {
        Uniforms = uniforms;
        Unsupported = unsupported;
        HasChildShader = hasChildShader;
    }

    /// <summary>The uniforms of a supported type, in declaration order.</summary>
    public IReadOnlyList<ShaderUniform> Uniforms { get; }

    /// <summary>Names of uniforms of a type the engine cannot set (<c>int</c>, matrices, ...); they stay zero.</summary>
    public IReadOnlyList<string> Unsupported { get; }

    /// <summary>True when the file declares a <c>uniform shader</c>: the host has nothing to give it.</summary>
    public bool HasChildShader { get; }

    /// <summary>Reads the declarations of <paramref name="text"/>; problems are logged as warnings naming <paramref name="shader"/>.</summary>
    public static ShaderDeclarations Parse(string text, string shader)
    {
        var uniforms = new List<ShaderUniform>();
        var unsupported = new List<string>();
        bool child = false;

        foreach (Match match in DeclarationPattern().Matches(text))
        {
            string type = match.Groups["type"].Value;
            string name = match.Groups["name"].Value;
            string? comment = match.Groups["default"].Success ? match.Groups["default"].Value.Trim() : null;

            if (type == "shader")
            {
                child = true;
                continue;
            }

            int? components = Components(type);
            if (components == null)
            {
                UrDeckLog.Warn($"Background '{shader}': uniform '{name}' has type '{type}', which is not supported (float, float2, float3, float4 and their aliases are); it stays 0.");
                unsupported.Add(name);
                continue;
            }

            float[]? value = null;
            if (comment != null)
            {
                value = ParseDefault(comment, components.Value);
                if (value == null)
                    UrDeckLog.Warn($"Background '{shader}': the default '{comment}' of '{name}' is not {components} number(s) or a colour; using 0.");
            }

            uniforms.Add(new ShaderUniform(name, components.Value, value));
        }

        return new ShaderDeclarations(uniforms, unsupported, child);
    }

    /// <summary>The float count of a supported type name, or null.</summary>
    internal static int? Components(string type) => type switch
    {
        "float" or "half" => 1,
        "float2" or "vec2" or "half2" => 2,
        "float3" or "vec3" or "half3" => 3,
        "float4" or "vec4" or "half4" => 4,
        _ => null,
    };

    /// <summary>A comma-separated list of exactly <paramref name="components"/> numbers, or a colour for three or four.</summary>
    internal static float[]? ParseDefault(string text, int components)
    {
        if (text.StartsWith('#'))
            return components is 3 or 4 && BackgroundSettings.TryParseColor(text, out var color) ? ColorToFloats(color, components) : null;

        string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != components)
            return null;

        float[] result = new float[components];
        for (int i = 0; i < components; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]) || !float.IsFinite(result[i]))
                return null;
        }

        return result;
    }

    /// <summary>Straight (not premultiplied) components from 0 to 1; the alpha is dropped for three.</summary>
    internal static float[] ColorToFloats(SKColor color, int components) => components == 3
        ? [color.Red / 255f, color.Green / 255f, color.Blue / 255f]
        : [color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f];

    [GeneratedRegex(@"^[ \t]*uniform[ \t]+(?<type>\w+)[ \t]+(?<name>\w+)[ \t]*;[ \t]*(?://[ \t]*default:(?<default>[^\r\n]*))?", RegexOptions.Multiline)]
    private static partial Regex DeclarationPattern();
}
