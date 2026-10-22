// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Motion;

/// <summary>How much the deck may move, from most to least.</summary>
public enum MotionLevel
{
    Off = 0,
    Reduced = 1,
    Full = 2,
}

/// <summary>What decided a <see cref="MotionDecision"/>, for the log line.</summary>
public enum MotionSource
{
    Configuration,
    Windows,
    Theme,
}

/// <summary>
/// The outcome of <see cref="MotionPolicy.Decide"/>: <see cref="Effective"/> is what the theme's continuous animations obey;
/// <see cref="User"/> is the user's choice before the theme's ceiling, which is what the background obeys.
/// </summary>
public readonly record struct MotionDecision(MotionLevel Effective, MotionLevel User, MotionSource Source)
{
    /// <summary>The log's wording: <c>full (configuration)</c>.</summary>
    public override string ToString() => $"{Name(Effective)} ({Describe(Source)})";

    internal static string Name(MotionLevel level) => level switch
    {
        MotionLevel.Full => "full",
        MotionLevel.Reduced => "reduced",
        _ => "off",
    };

    private static string Describe(MotionSource source) => source switch
    {
        MotionSource.Configuration => "configuration",
        MotionSource.Windows => "Windows animation setting",
        _ => "theme",
    };
}

/// <summary>
/// Decides the effective motion level from the configuration's <c>motion</c>, Windows' animation setting and the theme's
/// <c>motion.level</c> ceiling. Pure: the host feeds it the inputs again whenever one changes.
/// </summary>
public static class MotionPolicy
{
    /// <summary>
    /// Reads a level name (trimmed, any case). Null for absent; an unknown value is absent too, with a warning when
    /// <paramref name="warn"/> is set.
    /// </summary>
    public static MotionLevel? Parse(string? value, bool warn = false)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "":
                return null;
            case "full":
                return MotionLevel.Full;
            case "reduced":
                return MotionLevel.Reduced;
            case "off":
                return MotionLevel.Off;
            default:
                if (warn)
                    UrDeckLog.Warn($"motion '{value}' must be 'full', 'reduced' or 'off'; following Windows' animation setting.");
                return null;
        }
    }

    /// <summary>
    /// The level for <c>--snapshot</c>: the configuration's value and the theme's ceiling, Windows' setting treated as on, so
    /// a snapshot is reproducible on any machine.
    /// </summary>
    public static MotionDecision ForSnapshot(string? configMotion, string? themeLevel) => Decide(configMotion, true, themeLevel);

    /// <summary>
    /// The user's choice is <paramref name="configMotion"/> when it is a level, else Windows': <c>off</c> with animations
    /// off, <c>full</c> with them on. The effective level is the lower of that and the theme's ceiling (<c>full</c> when it
    /// sets none). The source is the theme only when its ceiling is what lowered the level.
    /// </summary>
    public static MotionDecision Decide(string? configMotion, bool windowsAnimations, string? themeLevel)
    {
        var configured = Parse(configMotion);
        var user = configured ?? (windowsAnimations ? MotionLevel.Full : MotionLevel.Off);
        var ceiling = Parse(themeLevel) ?? MotionLevel.Full;
        if (ceiling < user)
            return new MotionDecision(ceiling, user, MotionSource.Theme);
        return new MotionDecision(user, user, configured != null ? MotionSource.Configuration : MotionSource.Windows);
    }
}
