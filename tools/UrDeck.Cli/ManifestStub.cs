// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json.Nodes;
using UrDeck.Engine.Backgrounds;

namespace UrDeck.Cli;

/// <summary>The manifest <c>shader init</c> writes: what the shader declares, with the text left for the author to fill in.</summary>
internal static class ManifestStub
{
    /// <summary>The <c>$schema</c> value of every manifest the project ships.</summary>
    public const string Schema = "https://urdeck.app/schemas/v1/shader-manifest.json";

    private static readonly string[] ColorWords = ["color", "colour", "tint"];

    /// <summary>The stub for <paramref name="shaderText"/> of the background <paramref name="name"/>, and the uniforms that got no entry.</summary>
    public static (JsonObject Manifest, IReadOnlyList<string> Skipped) Build(string name, string shaderText)
    {
        var declarations = ShaderDeclarations.Parse(shaderText, name);
        var parameters = new JsonObject();
        foreach (var parameter in BackgroundParameter.Join(declarations, null))
        {
            var entry = new JsonObject
            {
                ["label"] = Words(parameter.Name),
                ["description"] = "",
            };
            if (SuggestsColor(parameter, declarations))
                entry["kind"] = "color";
            parameters[parameter.Name] = entry;
        }

        var manifest = new JsonObject
        {
            ["$schema"] = Schema,
            ["manifest"] = BackgroundManifest.SupportedVersion,
            ["title"] = Words(name),
            ["description"] = "",
            ["parameters"] = parameters,
        };
        return (manifest, declarations.Unsupported);
    }

    /// <summary>A guess, and only where <c>color</c> is allowed: three or four components and a sign (see design decision 3).</summary>
    private static bool SuggestsColor(BackgroundParameter parameter, ShaderDeclarations declarations)
    {
        if (parameter.Components is not (3 or 4))
            return false;

        var uniform = declarations.Uniforms.First(u => u.Name == parameter.Name);
        return parameter.Kind == ParameterKind.Color
            || uniform.DefaultIsColor
            || ColorWords.Any(word => parameter.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A name as words: split on <c>_</c>, <c>-</c> and each step from a lower-case to an upper-case letter, joined with spaces, every
    /// letter but the first in lower case (<c>bandWidth</c> and <c>band_width</c> both give <c>Band width</c>).
    /// </summary>
    public static string Words(string name)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        char previous = ' ';
        foreach (char c in name)
        {
            if (c is '_' or '-')
            {
                Flush();
            }
            else
            {
                if (char.IsUpper(c) && (char.IsLower(previous) || char.IsDigit(previous)))
                    Flush();
                current.Append(c);
            }

            previous = c;
        }

        Flush();
        string text = string.Join(' ', words.Select(w => w.ToLowerInvariant()));
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

        void Flush()
        {
            if (current.Length > 0)
                words.Add(current.ToString());
            current.Clear();
        }
    }
}
