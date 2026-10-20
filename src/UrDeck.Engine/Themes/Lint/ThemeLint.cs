// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SkiaSharp;
using UrDeck.Engine.Config;

namespace UrDeck.Engine.Themes;

/// <summary>One finding about a theme: the <see cref="ThemeFinding"/> fields, and the file it is in (relative to the theme).</summary>
public sealed record ThemeDiagnostic(string Code, ThemeSeverity Severity, string Message, string File, string? Key)
{
    public override string ToString() => $"{Code} {Message} ({File})";
}

/// <summary>What the lint knows about besides the theme: the folders that hold the user's backgrounds.</summary>
public sealed record ThemeLintOptions(IReadOnlyList<string> BackgroundDirectories)
{
    public static ThemeLintOptions Default { get; } = new([]);
}

/// <summary>
/// Checks a theme the way the deck loads it, and more. The findings of the deck's own checks come from the same code
/// (<see cref="ThemeLoader"/>, the resolver), with the same messages; the lint adds what needs a judgement: keys the deck does
/// not read, font files, backgrounds that do not exist, text contrast and the metadata.
/// </summary>
public static partial class ThemeLint
{
    /// <summary>The panel width, in pixels, that the lint resolves the theme at: four cells of 275 pixels.</summary>
    private const float PanelCellPx = 1100 / 4f;

    private const long MaxFontBytes = 3L * 1024 * 1024;
    private const long MaxPictureBytes = 8L * 1024 * 1024;

    private static readonly JsonDocumentOptions Document = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The findings about the theme in <paramref name="source"/>, in the order the checks ran.</summary>
    public static IReadOnlyList<ThemeDiagnostic> Check(IThemeSource source, ThemeLintOptions? options = null)
    {
        options ??= ThemeLintOptions.Default;
        var found = new List<ThemeDiagnostic>();
        string file = ThemeStore.SettingsFileName;

        // The file itself: nothing else can be checked without it.
        string? text;
        try
        {
            text = source.ReadSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [Error(ThemeCodes.FileUnusable, $"{file} could not be read: {ex.Message} Check that the file can be opened.", file, null)];
        }

        if (text == null)
        {
            return [Error(ThemeCodes.FileUnusable, $"{file} is missing; add a {file} to the theme's folder (urdeck-cli theme init makes a starter).", file, null)];
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text, documentOptions: Document);
        }
        catch (JsonException ex)
        {
            return [Error(ThemeCodes.FileUnusable, $"{file} is not valid JSON: {Reason(ex)}{Where(ex)}; fix the syntax there.", file, null)];
        }

        if (node is not JsonObject root)
            return [Error(ThemeCodes.FileUnusable, $"{file} holds no object; it must start with {{ and hold the theme's groups.", file, null)];

        foreach (var unknown in ThemeLintKeys.Find(root))
        {
            string advice = unknown.Suggestion == null
                ? "check the spelling against the schema (urdeck-cli theme schema)."
                : $"did you mean {unknown.Suggestion}?";
            found.Add(Warning(ThemeCodes.UnknownKey, $"{unknown.Path} is not a key the deck reads and it is ignored; {advice}", file, unknown.Path));
        }

        // A value of the wrong kind in a format 1 group fails the deck's whole read, so the theme is skipped.
        ThemeDefinition user;
        try
        {
            user = ThemeLenientJson.Read(text, new CollectedFindings(), UrDeckJson.Options, out _);
        }
        catch (JsonException ex)
        {
            string key = ex.Path is { } path ? path.TrimStart('$', '.') : "";
            found.Add(Error(ThemeCodes.WrongType,
                $"{(key.Length == 0 ? "a value" : key)} must be {Expected(ex)}; the deck skips the whole theme while a value of this key has the wrong kind.", file, key.Length == 0 ? null : key));
            return found;
        }

        var baseline = ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!;
        var engine = new CollectedFindings();
        var loaded = ThemeLoader.Load(source, baseline, engine);
        foreach (var finding in engine.Found)
            found.Add(From(finding, file));
        if (loaded == null)
            return found;

        CheckFonts(source, user, found);
        CheckBackgrounds(source, loaded.Definition, options, found);
        CheckContrast(loaded.Definition, found);
        CheckResolved(loaded, found);
        CheckMeta(loaded.Definition, found);
        return found;
    }

    /// <summary>The findings of the resolver at the panel width: weights the font lacks and reach into the next card.</summary>
    private static void CheckResolved(LoadedTheme loaded, List<ThemeDiagnostic> found)
    {
        var resolved = new CollectedFindings();
        var probe = loaded with { Findings = resolved };
        using (ThemeResolver.Resolve(probe, PanelCellPx))
        using (ResolvedCardStyle.Resolve(probe, PanelCellPx))
        {
        }

        // A font that cannot be loaded is a file finding the lint makes itself, and one named by family depends on the
        // machine; neither may differ from one platform to the next.
        foreach (var finding in resolved.Found.Where(f => f.Code != ThemeCodes.FontFile))
            found.Add(From(finding, ThemeStore.SettingsFileName));
    }

    private static void CheckMeta(ThemeDefinition definition, List<ThemeDiagnostic> found)
    {
        string file = ThemeStore.SettingsFileName;
        if (definition.Meta is not { } meta)
        {
            found.Add(Warning(ThemeCodes.NoMetadata, "meta is missing; add meta with a name, a description and a mode (dark or light).", file, "meta"));
            return;
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(meta.Name))
            missing.Add("name");
        if (string.IsNullOrWhiteSpace(meta.Description))
            missing.Add("description");
        if (string.IsNullOrWhiteSpace(meta.Mode))
            missing.Add("mode");
        if (missing.Count > 0)
            found.Add(Warning(ThemeCodes.NoMetadata, $"meta has no {string.Join(", ", missing)}; add {(missing.Count == 1 ? "it" : "them")} so the editor can list the theme.", file, "meta"));
    }

    private static ThemeDiagnostic From(ThemeFinding finding, string file) =>
        new(finding.Code, finding.Severity, finding.Message, file, finding.Key);

    internal static ThemeDiagnostic Error(string code, string message, string file, string? key) => new(code, ThemeSeverity.Error, message, file, key);

    internal static ThemeDiagnostic Warning(string code, string message, string file, string? key) => new(code, ThemeSeverity.Warning, message, file, key);

    private static string Reason(JsonException ex)
    {
        string message = ex.Message;
        int cut = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        if (cut >= 0)
            message = message[..cut];
        return message.TrimEnd('.', ' ');
    }

    private static string Where(JsonException ex) =>
        ex.LineNumber is { } line ? $" (the reader stopped at line {line + 1}, position {(ex.BytePositionInLine ?? 0) + 1})" : "";

    /// <summary>What the value of a failed read should have been, from the type the serializer named.</summary>
    private static string Expected(JsonException ex) =>
        ConvertedTo().Match(ex.Message) is { Success: true } match
            ? match.Groups[1].Value switch
            {
                var t when t.Contains("Double", StringComparison.Ordinal) => "a number",
                var t when t.Contains("Int32", StringComparison.Ordinal) => "a whole number",
                var t when t.Contains("String", StringComparison.Ordinal) => "text",
                var t when t.Contains("Boolean", StringComparison.Ordinal) => "true or false",
                var t when t.Contains("List", StringComparison.Ordinal) => "a list",
                _ => "a value of another kind",
            }
            : "a value of another kind";

    [GeneratedRegex(@"converted to ([\w.`\[\]<>,]+)")]
    private static partial Regex ConvertedTo();

    /// <summary>Colours as the deck parses them; the sanitised definition holds only valid ones.</summary>
    internal static SKColor Parse(string color) => SKColor.Parse(color);
}
