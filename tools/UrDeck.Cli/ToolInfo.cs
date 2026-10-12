// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;

namespace UrDeck.Cli;

internal static class ToolInfo
{
    public const string Name = "urdeck-cli";
    public const string InformationUri = "https://github.com/UrDeck/urdeck";

    /// <summary>The tool's version without the source revision that follows a '+'.</summary>
    public static string Version { get; } =
        typeof(ToolInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
}
