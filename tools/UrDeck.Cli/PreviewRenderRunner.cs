// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using UrDeck.Sdk;

namespace UrDeck.Cli;

internal sealed record PreviewRenderOptions(
    string Output,
    string? Config,
    string? Page,
    string? Widget,
    string? WidgetSize,
    string? Theme,
    string? ThemeDir,
    string? Background,
    string? BackgroundDir,
    string Size,
    int Width,
    string? Time,
    int? Frames,
    double Fps,
    string Motion,
    bool FailOnWarnings);

/// <summary>
/// <c>preview render</c>: a configuration's page (the sample page by default), or one widget alone, drawn by the preview
/// renderer with the providers' sample data, as one PNG or as a folder of frames. Usage errors are exit code 2; an input that
/// cannot be used (a configuration, a theme, a widget, a size, a page, a background) is exit code 1 with nothing written.
/// </summary>
internal static class PreviewRenderRunner
{
    public const string DefaultSize = "1100x1920";

    public static int Run(PreviewRenderOptions options, TextWriter output, TextWriter error)
    {
        // Usage first: nothing below reads a file before the arguments make sense together.
        if (options.Theme != null && options.ThemeDir != null)
            throw new ToolFailureException("give --theme or --theme-dir, not both.");
        if ((options.Widget == null) != (options.WidgetSize == null))
            throw new ToolFailureException("--widget and --widget-size go together.");
        if (options.Widget != null && options.Page != null)
            throw new ToolFailureException("--page draws a page of a configuration; a widget alone has none.");
        if (options.Width <= 0)
            throw new ToolFailureException($"--width {options.Width} is not a width in pixels.");
        if (options.Frames is <= 0)
            throw new ToolFailureException("--frames must be 1 or more.");
        if (!(options.Fps > 0) || !double.IsFinite(options.Fps))
            throw new ToolFailureException("--fps must be above zero.");

        var (width, height) = RenderRunner.ParseSize(options.Size, DefaultSize);
        var widgetSize = options.WidgetSize is { } ws ? RenderRunner.ParseSize(ws, "4x2") : default;
        var motion = MotionPolicy.Parse(options.Motion)
            ?? throw new ToolFailureException($"--motion '{options.Motion}' must be full, reduced or off.");
        DateTime? moment = options.Time is { } t ? ParseTime(t) : null;
        (int? pageNumber, bool withDock) = ParsePage(options.Page);

        string outputPath = Path.GetFullPath(options.Output);
        bool frames = options.Frames != null;
        if (!frames && Directory.Exists(outputPath))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} is a folder, not a file; give --frames to write frames into it.");
        if (frames && File.Exists(outputPath))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} is a file; with --frames it is the folder the frames are written to.");
        if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
            throw new ToolFailureException($"the output {PathDisplay.Of(outputPath)} cannot be written: its folder does not exist.");

        using var log = new LogCapture();
        try
        {
            var config = options.Config is { } configPath ? ReadConfig(configPath) : null;
            var findings = new List<ThemeDiagnostic>();
            if (!TryLoadTheme(options, findings, error, out var theme))
                return ExitCodes.Findings;

            string backgrounds = options.BackgroundDir is { } dir ? Path.GetFullPath(dir) : Path.Combine(AppContext.BaseDirectory, "backgrounds");
            if (options.BackgroundDir != null && !Directory.Exists(backgrounds))
                throw new PreviewException($"the background folder {PathDisplay.Of(options.BackgroundDir)} does not exist.");
            var shaders = new ShaderStore(backgrounds);
            var background = ParseBackground(options.Background, shaders);
            var renderer = new PreviewRenderer(new ThemeStore(Path.Combine(AppContext.BaseDirectory, "themes")), shaders,
                options.Config is { } c ? Path.GetDirectoryName(Path.GetFullPath(c))! : Environment.CurrentDirectory);

            var request = new PreviewRequest
            {
                Config = config,
                Page = pageNumber,
                WithDock = withDock,
                Widget = options.Widget is { } typeId ? new PreviewWidget(typeId, new System.Drawing.Size(widgetSize.Width, widgetSize.Height), WidgetConfigFrom(config, typeId)) : null,
                Theme = theme,
                Background = background,
                Width = options.Widget != null ? options.Width : width,
                Height = height,
                Moment = moment,
                Motion = motion,
            };

            if (frames)
            {
                (int w, int h) = (0, 0);
                renderer.RenderFrames(request, options.Frames!.Value, options.Fps, (i, bitmap) =>
                {
                    // Made only once the first frame is drawn: a preview that is refused writes nothing.
                    if (i == 0)
                        PrepareFolder(outputPath);
                    Write(bitmap, Path.Combine(outputPath, $"frame-{i:D4}.png"));
                    (w, h) = (bitmap.Width, bitmap.Height);
                });
                output.Write($"wrote {options.Frames} frames ({w}x{h}, {options.Fps.ToString(CultureInfo.InvariantCulture)} per second) to {PathDisplay.Of(outputPath)}\n");
            }
            else
            {
                using var bitmap = renderer.Render(request);
                Write(bitmap, outputPath);
                output.Write($"wrote {PathDisplay.Of(outputPath)} ({bitmap.Width}x{bitmap.Height})\n");
            }

            // What the engine warned about while drawing (a configuration's theme that is not there, say) is worth seeing.
            foreach (string warning in log.Warnings.Distinct(StringComparer.Ordinal))
                error.Write($"warning: {warning}\n");

            bool fails = options.FailOnWarnings && findings.Any(f => f.Severity != ThemeSeverity.Info);
            return fails ? ExitCodes.Findings : ExitCodes.Ok;
        }
        catch (PreviewException ex)
        {
            error.Write($"error: {ex.Message} Nothing was written.\n");
            return ExitCodes.Findings;
        }
    }

    private static DateTime ParseTime(string text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var time)
            ? DateTime.SpecifyKind(time, DateTimeKind.Local)
            : throw new ToolFailureException($"--time '{text}' is not a local date and time such as 2026-10-13T21:47:32.");

    /// <summary>A page number, <c>dock</c> (the active page as the deck shows it, with the dock and the indicator), or the active page.</summary>
    private static (int? Page, bool WithDock) ParsePage(string? page)
    {
        if (page == null)
            return (null, false);
        if (page.Equals("dock", StringComparison.OrdinalIgnoreCase))
            return (null, true);
        return int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? (number, false)
            : throw new ToolFailureException($"--page '{page}' must be a page number from 0, or dock.");
    }

    private static UrDeckConfig ReadConfig(string path)
    {
        string full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new PreviewException($"the configuration {PathDisplay.Of(path)} does not exist.");
        try
        {
            return PreviewConfig.Load(full);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new PreviewException($"the configuration {PathDisplay.Of(path)} cannot be read: {ex.Message}");
        }
    }

    /// <summary>The widget's settings for a widget alone: the first widget of the type in the configuration, else its default.</summary>
    private static WidgetConfig? WidgetConfigFrom(UrDeckConfig? config, string typeId) =>
        config?.Pages.SelectMany(p => p.Widgets).Concat(config.Dock)
            .FirstOrDefault(w => string.Equals(w.WidgetTypeId, typeId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The theme to draw: a built-in by name, a folder (its findings printed as warnings), or null for the configuration's own.
    /// False, after saying why, when a folder cannot be read at all.
    /// </summary>
    private static bool TryLoadTheme(PreviewRenderOptions options, List<ThemeDiagnostic> findings, TextWriter error, out LoadedTheme? theme)
    {
        theme = null;
        if (options.Theme is { } name)
        {
            theme = ThemeStore.LoadBuiltIn(name)
                ?? throw new PreviewException($"unknown built-in theme '{name}'; the built-in themes are {string.Join(", ", ThemeStore.BuiltInNames)}.");
            return true;
        }

        if (options.ThemeDir is not { } dir)
            return true;

        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
        if (!Directory.Exists(folder))
            throw new PreviewException($"the theme folder {PathDisplay.Of(dir)} does not exist.");
        var source = new DirectoryThemeSource(folder);
        string shown = PathDisplay.Of(folder);
        try
        {
            findings.AddRange(ThemeLint.Check(source));
            theme = ThemeLoader.Load(source, ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!, new CollectedFindings());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PreviewException($"the theme {shown} could not be read: {ex.Message}");
        }

        foreach (var finding in findings)
            error.Write(ReportWriters.TextLine($"{shown}/{finding.File}", Diagnostic.From(finding)) + "\n");
        if (theme == null)
            error.Write($"error: the theme {shown} cannot be read, so nothing was written.\n");
        return theme != null;
    }

    private static PreviewBackground ParseBackground(string? value, ShaderStore shaders)
    {
        switch (value)
        {
            case null:
                return PreviewBackground.Configuration;
            case "none":
                return PreviewBackground.None;
            case "theme":
                return PreviewBackground.Theme;
        }

        if (!BackgroundSettings.IsValidName(value))
            throw new ToolFailureException($"--background '{value}' must be a background's name, none or theme.");
        if (shaders.TryLoad(value).Package == null)
        {
            throw new PreviewException($"there is no background '{value}'; the built-in backgrounds are {string.Join(", ", ShaderStore.BuiltInNames)}" +
                " (and the folders of --background-dir).");
        }

        return PreviewBackground.Named(value);
    }

    /// <summary>The frames folder, made if needed, without the frames of an earlier run.</summary>
    private static void PrepareFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            foreach (string old in Directory.EnumerateFiles(path, "frame-*.png"))
                File.Delete(old);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the output {PathDisplay.Of(path)} cannot be written: {ex.Message}");
        }
    }

    private static void Write(SKBitmap bitmap, string path)
    {
        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the output {PathDisplay.Of(path)} cannot be written: {ex.Message}");
        }
    }
}
