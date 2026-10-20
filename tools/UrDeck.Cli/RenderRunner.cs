// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Themes;

namespace UrDeck.Cli;

/// <summary><c>shader render</c>: one frame of a background folder on the CPU, written as a PNG.</summary>
internal static partial class RenderRunner
{
    private const int MaxSide = 8192;

    public static int Run(RenderOptions options, TextWriter output, TextWriter error)
    {
        var (width, height) = ParseSize(options.Size);
        string themeName = options.Theme ?? ThemeStore.DefaultName;
        var theme = ThemeStore.LoadBuiltIn(themeName)
            ?? throw new ToolFailureException($"unknown theme '{themeName}'; the built-in themes are {string.Join(", ", ThemeStore.BuiltInNames)}.");

        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Folder));
        if (!Directory.Exists(folder))
            throw new ToolFailureException($"the folder {PathDisplay.Of(options.Folder)} does not exist.");

        string name = Path.GetFileName(folder);
        if (!BackgroundSettings.IsValidName(name))
        {
            throw new ToolFailureException(
                $"{ShaderCodes.FolderName} the folder name '{name}' is not a valid background name; a name may only contain letters, digits, '-' and '_'.");
        }

        string outputPath = Path.GetFullPath(options.Output);
        if (Directory.Exists(outputPath))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} is a folder, not a file.");
        if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} cannot be written: its folder does not exist.");

        SampleUpdate? update = null;
        if (options.UpdateManifest)
        {
            string file = SampleFile(folder, outputPath);
            update = SampleUpdate.Plan(folder, file, RecordSettings(options, width, height, themeName));
        }

        using var log = new LogCapture();
        var settings = BuildSettings(name, options);

        var load = ShaderStore.TryRead(folder);
        if (load.Package == null)
        {
            WriteWarnings(log, error);
            error.Write($"error: {load.Failure}\n");
            return ExitCodes.Findings;
        }

        using var result = BackgroundRender.Render(load.Package, settings, theme, width, height, options.Time);
        WriteWarnings(log, error);
        if (result.Bitmap == null)
        {
            error.Write($"error: {result.Failure}\n");
            return ExitCodes.Findings;
        }

        try
        {
            using var image = SKImage.FromBitmap(result.Bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(outputPath, data.ToArray());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} cannot be written: {ex.Message}");
        }

        output.Write($"wrote {PathDisplay.Of(outputPath)} ({width}x{height})\n");
        if (update != null)
        {
            bool added = update.Apply();
            output.Write($"{(added ? "added" : "updated")} {SampleFile(folder, outputPath)} in the samples of {PathDisplay.Of(update.ManifestPath)}\n");
        }

        var blank = result.Diagnostics.Where(d => d.Code == ShaderCodes.BlankRender).ToList();
        foreach (var diagnostic in blank)
            output.Write(ReportWriters.TextLine(PathDisplay.Of(folder), Diagnostic.From(diagnostic)) + "\n");

        return blank.Count > 0 && options.FailOnWarnings ? ExitCodes.Findings : ExitCodes.Ok;
    }

    private static void WriteWarnings(LogCapture log, TextWriter error)
    {
        foreach (string warning in log.Warnings)
            error.Write($"warning: {warning}\n");
    }

    private static (int Width, int Height) ParseSize(string size)
    {
        var match = SizePattern().Match(size);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int width)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int height)
            || width is < 1 or > MaxSide || height is < 1 or > MaxSide)
        {
            throw new ToolFailureException($"--size '{size}' must be WIDTHxHEIGHT in pixels, each from 1 to {MaxSide} (for example 270x480).");
        }

        return (width, height);
    }

    /// <summary>The picture's path relative to the folder with '/' separators; the picture must be inside the folder and not the manifest.</summary>
    private static string SampleFile(string folder, string outputPath)
    {
        string relative = Path.GetRelativePath(folder, outputPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new ToolFailureException(
                $"--update-manifest needs the output inside the background folder; {PathDisplay.Of(outputPath)} is outside {PathDisplay.Of(folder)}. Nothing was rendered.");
        }

        relative = relative.Replace(Path.DirectorySeparatorChar, '/');
        if (string.Equals(relative, BackgroundManifest.FileName, StringComparison.OrdinalIgnoreCase))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} is the manifest itself. Nothing was rendered.");

        return relative;
    }

    /// <summary>How the picture is made, as a sample's <c>settings</c>: size, time and theme always, colours and parameters when given.</summary>
    private static JsonObject RecordSettings(RenderOptions options, int width, int height, string themeName)
    {
        var settings = new JsonObject
        {
            ["size"] = $"{width}x{height}",
            ["time"] = options.Time,
            ["theme"] = themeName,
        };

        var colors = ColorList(options);
        if (colors.Count > 0)
        {
            var array = new JsonArray();
            foreach (string color in colors)
                array.Add(color);
            settings["colors"] = array;
        }

        var parameters = ParameterList(options);
        if (parameters.Count > 0)
        {
            var values = new JsonObject();
            foreach (var (key, value) in parameters)
                values[key] = value;
            settings["parameters"] = values;
        }

        return settings;
    }

    private static List<string> ColorList(RenderOptions options) =>
        options.Colors?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];

    /// <summary>The <c>--param</c> values as parsed, in the order given; a repeated name keeps the last, as the configuration does.</summary>
    private static List<(string Key, JsonNode? Value)> ParameterList(RenderOptions options)
    {
        var result = new List<(string Key, JsonNode? Value)>();
        foreach (string parameter in options.Param)
        {
            int equals = parameter.IndexOf('=', StringComparison.Ordinal);
            string key = equals > 0 ? parameter[..equals].Trim() : "";
            if (key.Length == 0 || !BackgroundSettings.IsValidName(key))
                throw new ToolFailureException($"--param '{parameter}' must be name=value, with a name of letters, digits, '-' and '_'.");
            if (HostKeys.IsHostKey(key))
                throw new ToolFailureException($"--param '{key}' is set by the product and cannot be a parameter of a shader.");

            result.RemoveAll(entry => entry.Key == key);
            result.Add((key, ParseValue(parameter[(equals + 1)..])));
        }

        return result;
    }

    /// <summary>The options as the configuration's <c>background</c> object, so the engine reads (and refuses) them as it does there.</summary>
    private static BackgroundSettings BuildSettings(string name, RenderOptions options)
    {
        var background = new JsonObject
        {
            ["type"] = "shader",
            ["name"] = name,
            ["scale"] = 1,
        };

        if (options.Colors != null)
        {
            var colors = new JsonArray();
            foreach (string color in ColorList(options))
                colors.Add(color);
            background["colors"] = colors;
        }

        foreach (var (key, value) in ParameterList(options))
            background[key] = value;

        using var document = JsonDocument.Parse(background.ToJsonString());
        return BackgroundSettings.Parse(document.RootElement.Clone());
    }

    /// <summary>A number or an array when the text parses as one, otherwise the text itself.</summary>
    private static JsonNode? ParseValue(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind is JsonValueKind.Number or JsonValueKind.Array)
                return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
        }

        return JsonValue.Create(text);
    }

    [GeneratedRegex(@"^(\d+)[xX](\d+)$")]
    private static partial Regex SizePattern();
}
