// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using DotMake.CommandLine;
using DotCli = DotMake.CommandLine.Cli;

namespace UrDeck.Cli;

/// <summary>
/// The entry point, callable in-process: it parses first and returns the exit code, so a parse error is <c>2</c> (the parser's
/// own code, <c>1</c>, would collide with "findings") and an unexpected exception is <c>2</c> on the error stream too.
/// </summary>
public static class CliApp
{
    private static readonly AsyncLocal<(TextWriter Out, TextWriter Error)?> Streams = new();

    internal static TextWriter Out => Streams.Value?.Out ?? Console.Out;

    internal static TextWriter Error => Streams.Value?.Error ?? Console.Error;

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var previous = Streams.Value;
        Streams.Value = (stdout, stderr);
        try
        {
            var settings = new CliSettings
            {
                Output = stdout,
                Error = stderr,
                ResponseFileTokenReplacer = null,
                EnableSuggestDirective = false,
                Theme = CliTheme.NoColor,
            };

            var result = DotCli.Parse<RootCommand>(args, settings);
            if (result.ParseResult.Errors.Count > 0)
            {
                foreach (var error in result.ParseResult.Errors)
                    stderr.WriteLine($"error: {error.Message}");
                return ExitCodes.Failure;
            }

            return result.Run();
        }
        catch (ToolFailureException ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return ExitCodes.Failure;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            stderr.WriteLine($"error: unexpected failure: {ex.Message}");
            return ExitCodes.Failure;
        }
        finally
        {
            Streams.Value = previous;
        }
    }
}
