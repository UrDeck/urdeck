// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Cli;

/// <summary>The exit codes of every command. Their meaning does not change in later versions.</summary>
internal static class ExitCodes
{
    /// <summary>The command did its job and found nothing that fails the run.</summary>
    public const int Ok = 0;

    /// <summary>The command did its job and there are findings that fail the run (or, for render, no picture could be made).</summary>
    public const int Findings = 1;

    /// <summary>The command could not do its job: bad arguments, a folder that does not exist, an output that cannot be written.</summary>
    public const int Failure = 2;

    /// <summary>The exit codes as the help of the tool and of each command lists them.</summary>
    public const string HelpText = """

        Exit codes:
          0  nothing that fails the run
          1  findings that fail the run (an error, or a warning with --fail-on-warnings); for render, no picture could be made
          2  the tool could not do its job: bad arguments, a folder that does not exist, an output that cannot be written
        """;
}

/// <summary>A reason the tool cannot do its job: printed to the error stream, exit code 2, nothing on the output stream.</summary>
internal sealed class ToolFailureException(string message) : Exception(message);
