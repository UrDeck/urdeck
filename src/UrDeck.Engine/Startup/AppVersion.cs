// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;

namespace UrDeck.Engine.Startup;

/// <summary>The version the running app was built as (release-process spec, "Version Shown").</summary>
public static class AppVersion
{
    /// <summary>The product version of the given assembly, as <c>0.2.0-rc.1+abc1234</c>; the assembly version when it carries none.</summary>
    public static string Of(Assembly assembly)
    {
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? "unknown"
            : informational;
    }

    /// <summary>The version without the build metadata (<c>+commit</c>): what a user is told.</summary>
    public static string WithoutMetadata(string informational)
    {
        int plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}
