// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;

namespace UrDeck.Cli;

internal sealed record ThemeSchemaOptions(string? Output);

/// <summary>
/// <c>theme schema</c>: prints the JSON Schema of the theme format, the file the product publishes as
/// <c>docs/schemas/v1/theme.json</c>, which the tool carries as a linked resource.
/// </summary>
internal static class ThemeSchemaRunner
{
    private const string ResourceName = "UrDeck.Cli.theme.schema.json";

    public static string Text()
    {
        using var stream = typeof(ThemeSchemaRunner).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new ToolFailureException("the tool was built without the theme schema.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        return reader.ReadToEnd();
    }

    public static int Run(ThemeSchemaOptions options, TextWriter output)
    {
        string text = Text();
        if (options.Output == null)
        {
            output.Write(text);
            return ExitCodes.Ok;
        }

        string path = Path.GetFullPath(options.Output);
        if (Directory.Exists(path))
            throw new ToolFailureException($"the output {PathDisplay.Of(path)} is a folder, not a file.");
        if (!Directory.Exists(Path.GetDirectoryName(path)))
            throw new ToolFailureException($"the output {PathDisplay.Of(path)} cannot be written: its folder does not exist.");
        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the output {PathDisplay.Of(path)} cannot be written: {ex.Message}");
        }

        return ExitCodes.Ok;
    }
}
