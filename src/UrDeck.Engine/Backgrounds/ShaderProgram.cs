// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Backgrounds;

/// <summary>
/// A compiled background shader with its uniforms resolved: the host's inputs (<c>iTime</c>, <c>iResolution</c>,
/// <c>iColor0</c> to <c>iColor3</c>) are set per frame, every other uniform is a parameter from its default or the
/// configuration. Owns the compiled effect.
/// </summary>
internal sealed class ShaderProgram : IDisposable
{
    private const string TimeName = "iTime";
    private const string ResolutionName = "iResolution";
    private static readonly string[] ColorNames = ["iColor0", "iColor1", "iColor2", "iColor3"];

    private readonly SKRuntimeEffect _effect;
    private readonly SKRuntimeEffectUniforms _uniforms;
    private readonly float[][] _colors;
    private readonly bool _hasResolution;
    private readonly bool[] _hasColor = new bool[ColorNames.Length];

    private ShaderProgram(SKRuntimeEffect effect, SKRuntimeEffectUniforms uniforms, bool declaresTime, bool hasResolution,
        bool[] hasColor, SKColor[] palette, IReadOnlyDictionary<string, float[]> parameters)
    {
        _effect = effect;
        _uniforms = uniforms;
        DeclaresTime = declaresTime;
        _hasResolution = hasResolution;
        _hasColor = hasColor;
        Parameters = parameters;
        _colors = palette.Select(c => ShaderDeclarations.ColorToFloats(c, 4)).ToArray();
    }

    /// <summary>True when the shader declares <c>iTime</c>: only then can the background move.</summary>
    public bool DeclaresTime { get; }

    /// <summary>The parameters as the shader receives them (defaults overridden by the configuration), by uniform name.</summary>
    public IReadOnlyDictionary<string, float[]> Parameters { get; }

    /// <summary>
    /// Compiles <paramref name="shader"/> and resolves its uniforms against <paramref name="settings"/>; null (after one
    /// warning that names the cause) when it cannot be used. Warnings about the configuration's keys are logged here.
    /// </summary>
    public static ShaderProgram? Create(ShaderText shader, BackgroundSettings settings, SKColor[] palette)
    {
        string label = shader.Name;
        var declarations = ShaderDeclarations.Parse(shader.Text, label);
        if (declarations.HasChildShader)
        {
            UrDeckLog.Warn($"Background '{label}': a 'uniform shader' (a child shader) cannot be used; there is nothing to give it.");
            return null;
        }

        SKRuntimeEffect? effect;
        string errors;
        try
        {
            effect = SKRuntimeEffect.CreateShader(shader.Text, out errors);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            UrDeckLog.Warn($"Background '{label}': the shader could not be compiled: {ex.Message}");
            return null;
        }

        if (effect == null)
        {
            UrDeckLog.Warn($"Background '{label}': the shader does not compile: {errors.Trim()}");
            return null;
        }

        if (effect.Children.Count > 0)
        {
            UrDeckLog.Warn($"Background '{label}': a child shader cannot be used; there is nothing to give it.");
            effect.Dispose();
            return null;
        }

        var known = new HashSet<string>(declarations.Uniforms.Select(u => u.Name).Concat(declarations.Unsupported), StringComparer.Ordinal);
        foreach (string name in effect.Uniforms)
            if (!known.Contains(name))
                UrDeckLog.Warn($"Background '{label}': uniform '{name}' could not be read from the source (one declaration per line, as 'uniform float name;'); it stays 0.");

        var uniforms = new SKRuntimeEffectUniforms(effect);
        var compiled = new HashSet<string>(effect.Uniforms, StringComparer.Ordinal);
        bool declaresTime = false;
        bool hasResolution = false;
        bool[] hasColor = new bool[ColorNames.Length];
        var parameters = new Dictionary<string, float[]>(StringComparer.Ordinal);

        foreach (var uniform in declarations.Uniforms)
        {
            if (!compiled.Contains(uniform.Name))
                continue;

            if (IsHostInput(uniform.Name))
            {
                bool right = uniform.Name switch
                {
                    TimeName => uniform.Components == 1,
                    ResolutionName => uniform.Components == 2,
                    _ => uniform.Components == 4,
                };
                if (!right)
                {
                    UrDeckLog.Warn($"Background '{label}': '{uniform.Name}' is provided by the host as {HostType(uniform.Name)}, not with {uniform.Components} component(s); it stays 0.");
                    continue;
                }

                if (uniform.Name == TimeName)
                    declaresTime = true;
                else if (uniform.Name == ResolutionName)
                    hasResolution = true;
                else
                    hasColor[Array.IndexOf(ColorNames, uniform.Name)] = true;
                continue;
            }

            float[] value = uniform.Default ?? new float[uniform.Components];
            if (settings.Parameters.TryGetValue(uniform.Name, out var given))
            {
                if (TryReadParameter(given, uniform.Components, out float[]? parsed))
                    value = parsed;
                else
                    UrDeckLog.Warn($"Background '{label}': '{uniform.Name}' {given.GetRawText()} must be {Describe(uniform.Components)}; using {Format(value)}.");
            }

            parameters[uniform.Name] = value;
            Set(uniforms, uniform.Name, value);
        }

        foreach (string key in settings.Parameters.Keys)
        {
            if (IsHostInput(key))
                UrDeckLog.Warn($"Background '{label}': '{key}' is an input the host sets and cannot be set in the configuration.");
            else if (!parameters.ContainsKey(key))
                UrDeckLog.Warn($"Background '{label}': '{key}' is not a parameter of the shader.");
        }

        return new ShaderProgram(effect, uniforms, declaresTime, hasResolution, hasColor, palette, parameters);
    }

    /// <summary>The shader for one frame of <paramref name="width"/> x <paramref name="height"/> pixels at <paramref name="time"/> seconds. The caller disposes it.</summary>
    public SKShader CreateShader(int width, int height, float time)
    {
        if (DeclaresTime)
            _uniforms[TimeName] = time;
        if (_hasResolution)
            _uniforms[ResolutionName] = new float[] { width, height };
        for (int i = 0; i < ColorNames.Length; i++)
            if (_hasColor[i])
                _uniforms[ColorNames[i]] = _colors[i];
        return _effect.ToShader(_uniforms);
    }

    public void Dispose()
    {
        _uniforms.Dispose();
        _effect.Dispose();
    }

    private static bool IsHostInput(string name) => name is TimeName or ResolutionName || Array.IndexOf(ColorNames, name) >= 0;

    private static string HostType(string name) => name switch
    {
        TimeName => "float",
        ResolutionName => "float2",
        _ => "float4",
    };

    private static void Set(SKRuntimeEffectUniforms uniforms, string name, float[] value)
    {
        if (value.Length == 1)
            uniforms[name] = value[0];
        else
            uniforms[name] = value;
    }

    private static bool TryReadParameter(JsonElement element, int components, out float[] value)
    {
        value = new float[components];
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when components == 1:
                return element.TryGetSingle(out value[0]) && float.IsFinite(value[0]);

            case JsonValueKind.Array when components > 1:
                if (element.GetArrayLength() != components)
                    return false;
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out value[index]) || !float.IsFinite(value[index]))
                        return false;
                    index++;
                }
                return true;

            case JsonValueKind.String when components is 3 or 4:
                if (!BackgroundSettings.TryParseColor(element.GetString(), out var color))
                    return false;
                value = ShaderDeclarations.ColorToFloats(color, components);
                return true;

            default:
                return false;
        }
    }

    private static string Describe(int components) => components switch
    {
        1 => "a number",
        2 => "an array of two numbers",
        _ => $"an array of {components} numbers or a #RRGGBB colour",
    };

    private static string Format(float[] value) =>
        string.Join(", ", value.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));
}
