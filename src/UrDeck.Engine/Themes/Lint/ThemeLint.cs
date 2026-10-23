// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
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

        // A value of the wrong kind is removed with its own warning (THEME003) among the engine's findings below; a file the
        // reader still cannot use is skipped whole by the deck.
        ThemeDefinition user;
        try
        {
            user = ThemeLenientJson.Read(text, new CollectedFindings(), UrDeckJson.Options, out _);
        }
        catch (JsonException ex)
        {
            found.Add(Error(ThemeCodes.FileUnusable, $"{file} could not be read: {Reason(ex)}{Where(ex)}; fix the value there.", file, null));
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
        // machine; neither may differ from one platform to the next. The icon sets are files the lint reads itself, thoroughly.
        foreach (var finding in resolved.Found.Where(f => f.Code is not (ThemeCodes.FontFile or ThemeCodes.UnknownIconSet or ThemeCodes.IconManifest or ThemeCodes.IconEntry or ThemeCodes.IconFile)))
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
        new(finding.Code, finding.Severity, WithTheFix(finding), file, finding.Key);

    /// <summary>
    /// The deck's log line, which says what is wrong and what the deck does; where the line gives no fix, the tool says what to
    /// change. The log text itself is not touched, and a finding in the deck's own words that has no subject gets one.
    /// </summary>
    private static string WithTheFix(ThemeFinding finding)
    {
        string message = finding.Form == ThemeLogForm.Space ? "the theme " + finding.Message : finding.Message;
        string? fix = finding.Code switch
        {
            ThemeCodes.FontFile when message.Contains("leaves the theme folder", StringComparison.Ordinal) =>
                "Put the font file in the theme's folder and name it without a path.",
            ThemeCodes.BorderWithoutWidth => "Give card.borderWidth a value above 0, or remove the style.",
            ThemeCodes.SpinWithoutConic => "Set card.borderGradient to kind conic, or remove the animation.",
            ThemeCodes.AnimatedDashedBorder => "Use a solid card.borderStyle, or remove the animation.",
            ThemeCodes.PatternDense => "Raise pattern.spacing, or lower pattern.thickness.",
            ThemeCodes.CornerMarksReach => "Move the marks in (a smaller negative inset) or widen card.gap.",
            ThemeCodes.EffectReach => "Reduce the blur, offset or spread, or widen card.gap.",
            ThemeCodes.Hairline => "Raise the value so it is at least one pixel at the panel width.",
            ThemeCodes.CompactCapsule => "Use a card that is square, or another shape, if the capsule is not wanted.",
            ThemeCodes.Animations => "Turn one of them off to save frames.",
            _ => null,
        };
        return fix == null ? message : $"{message} {fix}";
    }

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

    /// <summary>Colours as the deck parses them; the sanitised definition holds only valid ones.</summary>
    internal static SKColor Parse(string color) => SKColor.Parse(color);
}
