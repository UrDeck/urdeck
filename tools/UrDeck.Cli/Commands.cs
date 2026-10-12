// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using DotMake.CommandLine;
using UrDeck.Engine.Backgrounds;

namespace UrDeck.Cli;

/// <summary>The command classes DotMake builds the parser from. They only carry the options; the work is in the runners.</summary>
[CliCommand(Description = "Checks and renders UrDeck background folders without a deck." + ExitCodes.HelpText,
    ShortFormAutoGenerate = CliNameAutoGenerate.None)]
public class RootCommand
{
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
