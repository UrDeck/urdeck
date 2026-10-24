// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UrDeck.Engine.Themes;

namespace UrDeck.Cli;

internal sealed record ThemeInitOptions(string Name, string? Output, string? From);

/// <summary>
/// <c>theme init</c>: makes a theme folder that loads and checks without errors, either a small starter or a self-contained
/// copy of a built-in theme with the fonts and licences it names. Never overwrites.
/// </summary>
internal static partial class ThemeInitRunner
{
    private const string SchemaUri = "https://urdeck.app/schemas/v1/theme.json";

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^(?<stem>.+)-OFL\.txt$|^LICENSE.*$|^.+-LICENSE.*$", RegexOptions.IgnoreCase)]
    private static partial Regex LicenceName();

    public static int Run(ThemeInitOptions options, TextWriter output)
    {
        if (!NamePattern().IsMatch(options.Name))
        {
            throw new ToolFailureException(
                $"'{options.Name}' is not a valid theme name; a name is a single folder name of letters, digits, '-' and '_'.");
        }

        string parent = options.Output == null ? Directory.GetCurrentDirectory() : Path.GetFullPath(options.Output);
        if (!Directory.Exists(parent))
            throw new ToolFailureException($"the directory {PathDisplay.Of(options.Output ?? parent)} does not exist.");

        string folder = Path.Combine(parent, options.Name);
        if (Directory.Exists(folder) || File.Exists(folder))
            throw new ToolFailureException($"{PathDisplay.Of(folder)} already exists and is left as it is.");

        var files = options.From == null ? Starter(options.Name) : CopyOf(options.From, options.Name);

        try
        {
            Directory.CreateDirectory(folder);
            foreach (var (name, bytes) in files)
                File.WriteAllBytes(Path.Combine(folder, name), bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the folder {PathDisplay.Of(folder)} could not be written: {ex.Message}");
        }

        output.Write($"made {PathDisplay.Of(folder)}\n");
        foreach (var (name, _) in files)
            output.Write($"  wrote {name}\n");
        output.Write($"next: urdeck-cli theme validate {PathDisplay.Of(folder)}\n");
        return ExitCodes.Ok;
    }

    /// <summary>A starter: the schema line, the metadata to fill in, and the default dark theme's colours.</summary>
    private static List<(string Name, byte[] Bytes)> Starter(string name)
    {
        var dark = JsonNode.Parse(new EmbeddedThemeSource(ThemeStore.DefaultName).ReadSettings()!)!.AsObject();
        var root = new JsonObject
        {
            ["$schema"] = SchemaUri,
            ["meta"] = new JsonObject { ["name"] = name, ["description"] = "", ["mode"] = "dark" },
            ["colors"] = dark["colors"]!.DeepClone(),
        };
        return [(ThemeStore.SettingsFileName, Encoding.UTF8.GetBytes(root.ToJsonString(Pretty) + "\n"))];
    }

    /// <summary>A built-in theme's settings as shipped (with the new name) and the font and licence files it names.</summary>
    private static List<(string Name, byte[] Bytes)> CopyOf(string from, string name)
    {
        if (!ThemeStore.BuiltInNames.Contains(from))
            throw new ToolFailureException($"unknown built-in theme '{from}'; the built-in themes are {string.Join(", ", ThemeStore.BuiltInNames)}.");

        var source = new EmbeddedThemeSource(from);
        var root = JsonNode.Parse(source.ReadSettings()!)!.AsObject();
        if (root["meta"] is not JsonObject meta)
            root["meta"] = meta = new JsonObject();
        meta["name"] = name;

        var files = new List<(string Name, byte[] Bytes)> { (ThemeStore.SettingsFileName, Encoding.UTF8.GetBytes(root.ToJsonString(Pretty) + "\n")) };
        var fonts = new List<string>();
        if (root["typography"] is JsonObject typography)
        {
            if (typography["font"]?.GetValue<string>() is { } font)
                fonts.Add(font);
            if (typography["fonts"] is JsonObject roles)
                fonts.AddRange(roles.Select(r => r.Value?.GetValue<string>()).Where(f => f != null)!);
        }

        foreach (string font in fonts.Where(IsFontFile).Distinct(StringComparer.Ordinal))
        {
            files.Add((font, Read(source, font)));
            string stem = Path.GetFileNameWithoutExtension(font);
            foreach (string licence in source.Files().Where(f => BelongsTo(f, stem)))
                if (files.All(f => f.Name != licence))
                    files.Add((licence, Read(source, licence)));
        }

        return files;
    }

    private static bool IsFontFile(string spec) => spec.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
        || spec.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) || spec.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase);

    /// <summary>The licence of a font: <c>JetBrainsMono-OFL.txt</c> for <c>JetBrainsMono-Variable.ttf</c>, or a plain <c>LICENSE</c> file.</summary>
    private static bool BelongsTo(string file, string fontStem)
    {
        var match = LicenceName().Match(file);
        if (!match.Success)
            return false;
        return !match.Groups["stem"].Success || fontStem.StartsWith(match.Groups["stem"].Value, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Read(EmbeddedThemeSource source, string file)
    {
        using var stream = source.Open(file) ?? throw new ToolFailureException($"the built-in file {file} is not shipped.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
