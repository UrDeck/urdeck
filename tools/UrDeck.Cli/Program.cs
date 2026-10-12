// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Cli;

public static class Program
{
    public static int Main(string[] args) => CliApp.Run(args, Console.Out, Console.Error);
}
