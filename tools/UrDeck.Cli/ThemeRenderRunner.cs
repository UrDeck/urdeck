// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Preview;

namespace UrDeck.Cli;

internal sealed record ThemeRenderOptions(string? Folder, string? Builtin, string Output, string Page, string Size, bool FailOnWarnings);

/// <summary>
/// <c>theme render</c>: the sample page drawn in a theme with the page renderer the deck uses, written as a PNG. The theme's
/// findings go to the error stream; they do not stop the picture, unless the theme cannot be read at all.
/// </summary>
internal static class ThemeRenderRunner
{
    public const string DefaultSize = "1100x1920";

    public static int Run(ThemeRenderOptions options, TextWriter output, TextWriter error)
    {
        if (options.Folder != null && options.Builtin != null)
            throw new ToolFailureException("give a folder or --builtin <name>, not both.");
        if (options.Folder == null && options.Builtin == null)
            throw new ToolFailureException("give the theme folder to draw, or --builtin <name>.");
        if (options.Folder is { } argument && argument.StartsWith('-'))
            throw new ToolFailureException($"unknown option '{argument}'.");

        var (width, height) = RenderRunner.ParseSize(options.Size, DefaultSize);
        string outputPath = Path.GetFullPath(options.Output);
        if (Directory.Exists(outputPath))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} is a folder, not a file.");
        if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} cannot be written: its folder does not exist.");

        using var log = new LogCapture();
        var baseline = ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!;
        IThemeSource source;
        string shown;
        if (options.Builtin is { } builtin)
        {
            if (!ThemeStore.BuiltInNames.Contains(builtin))
                throw new ToolFailureException($"unknown built-in theme '{builtin}'; the built-in themes are {string.Join(", ", ThemeStore.BuiltInNames)}.");
            source = new EmbeddedThemeSource(builtin);
            shown = $"builtin:{builtin}";
        }
        else
        {
            string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Folder!));
            if (!Directory.Exists(folder))
                throw new ToolFailureException($"the folder {PathDisplay.Of(options.Folder!)} does not exist.");
            source = new DirectoryThemeSource(folder);
            shown = PathDisplay.Of(folder);
        }

        IReadOnlyList<ThemeDiagnostic> found;
        LoadedTheme? theme;
        try
        {
            found = ThemeLint.Check(source);
            theme = ThemeLoader.Load(source, baseline, new CollectedFindings());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the theme {shown} could not be read: {ex.Message}");
        }

        foreach (var finding in found)
            error.Write(ReportWriters.TextLine($"{shown}/{finding.File}", Diagnostic.From(finding)) + "\n");

        if (theme == null)
        {
            error.Write($"error: the theme {shown} cannot be read, so no picture was written.\n");
            return ExitCodes.Findings;
        }

        using (var bitmap = ThemePreview.Render(theme, width, height, dock: options.Page == "dock"))
        {
            try
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(outputPath, data.ToArray());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} cannot be written: {ex.Message}");
            }
        }

        output.Write($"wrote {PathDisplay.Of(outputPath)} ({width}x{height})\n");
        bool fails = options.FailOnWarnings && found.Any(f => f.Severity != ThemeSeverity.Info);
        return fails ? ExitCodes.Findings : ExitCodes.Ok;
    }
}
