// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using DotMake.CommandLine;
using UrDeck.Engine.Backgrounds;

namespace UrDeck.Cli;

/// <summary>The command classes DotMake builds the parser from. They only carry the options; the work is in the runners.</summary>
[CliCommand(Description = "Checks, renders and starts UrDeck background folders (shader) and theme folders (theme) without a deck." + ExitCodes.HelpText,
    ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class RootCommand
{
    [CliCommand(Description = "Commands about themes." + ExitCodes.HelpText)]
    public class ThemeCommand
    {
        [CliCommand(Description = "Checks a theme folder, every folder in a directory, or the built-in themes with the findings the deck logs when it loads a theme and the checks only the tool makes (keys the deck does not read, fonts, backgrounds, text contrast, metadata), and prints them."
            + ExitCodes.HelpText)]
        public class ValidateCommand
        {
            [CliArgument(Description = "The theme folder. Its name is the theme's name.", Arity = CliArgumentArity.ZeroOrOne, Required = false)]
            public string? Folder { get; set; }

            [CliOption(Description = "Check every folder directly inside this directory instead of one folder.", Name = "all", Required = false)]
            public string? All { get; set; }

            [CliOption(Description = "Check the built-in themes as the product ships them, instead of a folder.", Name = "builtin")]
            public bool Builtin { get; set; }

            [CliOption(Description = "A directory of background folders a theme may name; may be repeated. The built-in backgrounds are always known.", Name = "background-dir")]
            public List<string> BackgroundDir { get; set; } = [];

            [CliOption(Description = "How the findings are printed.", Name = "format", Alias = "f", AllowedValues = ["text", "markdown", "json", "sarif"])]
            public string Format { get; set; } = "text";

            [CliOption(Description = "A warning fails the run as an error does.", Name = "fail-on-warnings")]
            public bool FailOnWarnings { get; set; }

            public int Run() => ThemeValidateRunner.Run(new ThemeValidateOptions(Folder, All, Builtin, BackgroundDir, Format, FailOnWarnings), CliApp.Out);
        }

        [CliCommand(Description = "Draws the sample page (a clock, two stats cards, a weather card and four shortcuts, with fixed readings and time) in a theme and writes it as a PNG. The theme's findings go to the error stream."
            + ExitCodes.HelpText)]
        public class RenderCommand
        {
            [CliArgument(Description = "The theme folder. Its name is the theme's name.", Arity = CliArgumentArity.ZeroOrOne, Required = false)]
            public string? Folder { get; set; }

            [CliOption(Description = "Draw this built-in theme instead of a folder.", Name = "builtin", Required = false)]
            public string? Builtin { get; set; }

            [CliOption(Description = "The PNG file to write; an existing file is replaced.", Name = "output", Alias = "o", Required = true)]
            public string Output { get; set; } = "";

            [CliOption(Description = "What to draw: the page, or the page with the dock and the page indicator.", Name = "page", AllowedValues = ["page", "dock"])]
            public string Page { get; set; } = "page";

            [CliOption(Description = "The picture's size in pixels, WIDTHxHEIGHT.", Name = "size")]
            public string Size { get; set; } = ThemeRenderRunner.DefaultSize;

            [CliOption(Description = "A warning fails the run as an error does.", Name = "fail-on-warnings")]
            public bool FailOnWarnings { get; set; }

            public int Run() => ThemeRenderRunner.Run(new ThemeRenderOptions(Folder, Builtin, Output, Page, Size, FailOnWarnings), CliApp.Out, CliApp.Error);
        }

        [CliCommand(Description = "Makes a theme folder: a small starter, or with --from a copy of a built-in theme with the fonts and licences it names. Never overwrites a folder."
            + ExitCodes.HelpText)]
        public class InitCommand
        {
            [CliArgument(Description = "The theme's name: a single folder name of letters, digits, '-' and '_'.")]
            public string Name { get; set; } = "";

            [CliOption(Description = "Make the folder in this existing directory instead of the current one.", Name = "output", Alias = "o", Required = false)]
            public string? Output { get; set; }

            [CliOption(Description = "Start from a copy of this built-in theme.", Name = "from", Required = false)]
            public string? From { get; set; }

            public int Run() => ThemeInitRunner.Run(new ThemeInitOptions(Name, Output, From), CliApp.Out);
        }

        [CliCommand(Description = "Prints the JSON Schema of the theme format, the file the product publishes as docs/schemas/v2/theme.json."
            + ExitCodes.HelpText)]
        public class SchemaCommand
        {
            [CliOption(Description = "Write the schema to this file instead of the output; an existing file is replaced.", Name = "output", Alias = "o", Required = false)]
            public string? Output { get; set; }

            public int Run() => ThemeSchemaRunner.Run(new ThemeSchemaOptions(Output), CliApp.Out);
        }
    }

    [CliCommand(Description = "Commands about previews: pictures of a page or a widget drawn with sample data." + ExitCodes.HelpText)]
    public class PreviewCommand
    {
        [CliCommand(Description = "Draws a configuration's page (the sample page by default), or one widget alone, in a theme over a background with the data providers' sample readings, and writes it as a PNG or, with --frames, as a folder of frames. Makes no network request."
            + ExitCodes.HelpText)]
        public class RenderCommand
        {
            [CliOption(Description = "The PNG file to write, or with --frames the folder the frames are written to; what is there is replaced.", Name = "output", Alias = "o", Required = true)]
            public string Output { get; set; } = "";

            [CliOption(Description = "A urdeck-config.json to draw; the sample page when absent. It is read, never written.", Name = "config", Required = false)]
            public string? Config { get; set; }

            [CliOption(Description = "The page to draw, from 0, or dock for the active page with the dock and the page indicator; default: the configuration's active page.", Name = "page", Required = false)]
            public string? Page { get; set; }

            [CliOption(Description = "Draw one widget of this type alone instead of a page, with its default configuration (or the first of its type in --config).", Name = "widget", Required = false)]
            public string? Widget { get; set; }

            [CliOption(Description = "The widget's size in cells, WIDTHxHEIGHT; one the widget declares.", Name = "widget-size", Required = false)]
            public string? WidgetSize { get; set; }

            [CliOption(Description = "A built-in theme; default: the configuration's theme.", Name = "theme", Required = false)]
            public string? Theme { get; set; }

            [CliOption(Description = "A theme folder instead of a built-in theme; its findings go to the error stream.", Name = "theme-dir", Required = false)]
            public string? ThemeDir { get; set; }

            [CliOption(Description = "A background's name, none, or theme for the theme's own choice; default: the configuration's background.", Name = "background", Required = false)]
            public string? Background { get; set; }

            [CliOption(Description = "A directory of background folders a name may refer to; the built-in backgrounds are always known.", Name = "background-dir", Required = false)]
            public string? BackgroundDir { get; set; }

            [CliOption(Description = "The picture's size in pixels for a page, WIDTHxHEIGHT.", Name = "size")]
            public string Size { get; set; } = PreviewRenderRunner.DefaultSize;

            [CliOption(Description = "For a widget alone: the width in pixels of the four-cell panel it is drawn on.", Name = "width")]
            public int Width { get; set; } = 1100;

            [CliOption(Description = "The moment drawn, a local date and time; default: the sample page's, 2026-10-13T21:47:32.", Name = "time", Required = false)]
            public string? Time { get; set; }

            [CliOption(Description = "Write this many frames, frame-0000.png onwards, into the output folder.", Name = "frames", Required = false)]
            public int? Frames { get; set; }

            [CliOption(Description = "The frames per second of --frames.", Name = "fps")]
            public double Fps { get; set; } = 30;

            [CliOption(Description = "The motion level the deck runs at: the background and an animated border are still below full.", Name = "motion", AllowedValues = ["full", "reduced", "off"])]
            public string Motion { get; set; } = "full";

            [CliOption(Description = "A warning about the theme folder fails the run as an error does.", Name = "fail-on-warnings")]
            public bool FailOnWarnings { get; set; }

            public int Run() => PreviewRenderRunner.Run(
                new PreviewRenderOptions(Output, Config, Page, Widget, WidgetSize, Theme, ThemeDir, Background, BackgroundDir, Size, Width, Time,
                    Frames, Fps, Motion, FailOnWarnings),
                CliApp.Out, CliApp.Error);
        }
    }

    [CliCommand(Description = "Commands about what the product ships." + ExitCodes.HelpText)]
    public class CatalogCommand
    {
        [CliCommand(Description = "Writes the themes, backgrounds, pairings, widgets (sizes, default configurations, settings), data providers and finding codes as one JSON document, sorted, so the same input gives the same bytes."
            + ExitCodes.HelpText)]
        public class ExportCommand
        {
            [CliOption(Description = "The file to write; the output stream when absent. An existing file is replaced.", Name = "output", Alias = "o", Required = false)]
            public string? Output { get; set; }

            [CliOption(Description = "A directory of theme folders to list besides the built-in themes.", Name = "theme-dir", Required = false)]
            public string? ThemeDir { get; set; }

            [CliOption(Description = "A directory of background folders to list besides the built-in backgrounds.", Name = "background-dir", Required = false)]
            public string? BackgroundDir { get; set; }

            public int Run() => CatalogExportRunner.Run(new CatalogExportOptions(Output, ThemeDir, BackgroundDir), CliApp.Out);
        }
    }

    [CliCommand(Description = "Commands about shader backgrounds." + ExitCodes.HelpText)]
    public class ShaderCommand
    {
        [CliCommand(Description = "Checks a background folder with the lint the deck runs when it loads one, and prints the findings."
            + ExitCodes.HelpText)]
        public class ValidateCommand
        {
            [CliArgument(Description = "The background folder. Its name is the background's name.", Arity = CliArgumentArity.ZeroOrOne, Required = false)]
            public string? Folder { get; set; }

            [CliOption(Description = "Check every folder directly inside this directory instead of one folder.", Name = "all", Required = false)]
            public string? All { get; set; }

            [CliOption(Description = "How the findings are printed.", Name = "format", Alias = "f", AllowedValues = ["text", "markdown", "json", "sarif"])]
            public string Format { get; set; } = "text";

            [CliOption(Description = "A warning fails the run as an error does.", Name = "fail-on-warnings")]
            public bool FailOnWarnings { get; set; }

            public int Run() => ValidateRunner.Run(new ValidateOptions(Folder, All, Format, FailOnWarnings), CliApp.Out, CliApp.Error);
        }

        [CliCommand(Description = "Makes a background folder with a manifest from a shader: a stub with one entry for each parameter the shader declares. Never overwrites a manifest."
            + ExitCodes.HelpText)]
        public class InitCommand
        {
            [CliArgument(Description = "A .sksl file (a folder of its name is made next to it and the file is moved in) or a folder that holds <name>.sksl and no manifest.")]
            public string Path { get; set; } = "";

            [CliOption(Description = "Make the folder in this existing directory instead of next to the shader file.", Name = "output", Alias = "o", Required = false)]
            public string? Output { get; set; }

            public int Run() => InitRunner.Run(new InitOptions(Path, Output), CliApp.Out, CliApp.Error);
        }

        [CliCommand(Description = "Draws one frame of a background folder on the CPU and writes it as a PNG." + ExitCodes.HelpText)]
        public class RenderCommand
        {
            [CliArgument(Description = "The background folder. Its name is the background's name.")]
            public string Folder { get; set; } = "";

            [CliOption(Description = "The PNG file to write; an existing file is replaced.", Name = "output", Alias = "o", Required = true)]
            public string Output { get; set; } = "";

            [CliOption(Description = "The picture's size in pixels, WIDTHxHEIGHT.", Name = "size")]
            public string Size { get; set; } = "270x480";

            [CliOption(Description = "The shader's time (iTime) in seconds; the default is the snapshot time.", Name = "time")]
            public float Time { get; set; } = BackgroundClock.SnapshotTime;

            [CliOption(Description = "A built-in theme whose colours the background uses.", Name = "theme", Required = false)]
            public string? Theme { get; set; }

            [CliOption(Description = "The background's colours, one to four, as #RRGGBB separated by commas.", Name = "colors", Required = false)]
            public string? Colors { get; set; }

            [CliOption(Description = "A parameter of the shader as name=value; may be repeated.", Name = "param")]
            public List<string> Param { get; set; } = [];

            [CliOption(Description = "A warning fails the run as an error does.", Name = "fail-on-warnings")]
            public bool FailOnWarnings { get; set; }

            [CliOption(Description = "Record the picture in the folder's manifest.json: add it to samples, or update its entry, with how it was made in settings. The output must be inside the folder. Refused, with the entry to paste, when a rewrite would lose a comment, a trailing comma or a duplicate key.", Name = "update-manifest")]
            public bool UpdateManifest { get; set; }

            public int Run() => RenderRunner.Run(
                new RenderOptions(Folder, Output, Size, Time, Theme, Colors, Param, FailOnWarnings, UpdateManifest), CliApp.Out, CliApp.Error);
        }
    }
}
