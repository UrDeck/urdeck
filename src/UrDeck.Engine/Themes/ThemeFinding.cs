// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Themes;

public enum ThemeSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>How the log sink joins the theme's name to a finding's message, so the log text is what it always was.</summary>
internal enum ThemeLogForm
{
    /// <summary><c>Theme 'name': message</c>.</summary>
    Colon,

    /// <summary><c>Theme 'name' message</c>.</summary>
    Space,

    /// <summary>The message as it is: it does not name the theme.</summary>
    Bare,
}

/// <summary>
/// One finding about a theme. <see cref="Message"/> is the line the deck logs without its <c>Theme 'name': </c> prefix, and
/// says how to fix the problem; <see cref="Key"/> is the path of the value it is about (<c>card.shadow.blur</c>) or null.
/// The code keeps its meaning in later versions.
/// </summary>
public sealed record ThemeFinding(string Code, ThemeSeverity Severity, string? Key, string Message)
{
    internal ThemeLogForm Form { get; init; } = ThemeLogForm.Colon;

    /// <summary>The deck has dealt with the finding itself (it limited a reach), so its log line is information; the tool still warns.</summary>
    internal bool LoggedAsInfo { get; init; }

    public override string ToString() => Key == null ? $"{Code} {Message}" : $"{Code} {Key}: {Message}";
}

/// <summary>Receives what the theme checks find. The deck logs it; the command line tool keeps it.</summary>
public interface IThemeFindings
{
    void Report(ThemeFinding finding);
}

/// <summary>The deck's sink: one log line for each finding, the line the deck has always written for it.</summary>
internal sealed class LogFindings(string themeName) : IThemeFindings
{
    public void Report(ThemeFinding finding)
    {
        string text = finding.Form switch
        {
            ThemeLogForm.Colon => $"Theme '{themeName}': {finding.Message}",
            ThemeLogForm.Space => $"Theme '{themeName}' {finding.Message}",
            _ => finding.Message,
        };
        if (finding.Severity == ThemeSeverity.Info || finding.LoggedAsInfo)
            UrDeckLog.Info(text);
        else
            UrDeckLog.Warn(text);
    }
}

/// <summary>A sink that keeps every finding, in the order they were reported.</summary>
public sealed class CollectedFindings : IThemeFindings
{
    private readonly List<ThemeFinding> _found = [];

    public IReadOnlyList<ThemeFinding> Found => _found;

    public void Report(ThemeFinding finding) => _found.Add(finding);
}

/// <summary>The finding codes, fixed so the log, the documentation, the tests and the command line tool agree.</summary>
public static class ThemeCodes
{
    public const string FileUnusable = "THEME001";
    public const string UnknownKey = "THEME002";
    public const string WrongType = "THEME003";
    public const string OutOfRange = "THEME004";
    public const string NotOneOf = "THEME005";
    public const string BadColor = "THEME006";
    public const string BadList = "THEME007";
    public const string MissingMember = "THEME008";
    public const string FontFile = "THEME020";
    public const string FontFileSize = "THEME021";
    public const string FontFamily = "THEME022";
    public const string FontWeight = "THEME023";
    public const string UnknownBackground = "THEME040";
    public const string UnknownPairing = "THEME041";
    public const string DefaultNotPaired = "THEME042";
    public const string StillPicture = "THEME043";
    public const string UnknownIconSet = "THEME044";
    public const string IconManifest = "THEME070";
    public const string IconEntry = "THEME071";
    public const string IconFile = "THEME072";
    public const string IconKey = "THEME073";
    public const string IconLicense = "THEME074";
    public const string IconOrphan = "THEME075";
    public const string IconMissingKeys = "THEME076";
    public const string BorderWithoutWidth = "THEME050";
    public const string SpinWithoutConic = "THEME051";
    public const string PatternDense = "THEME052";
    public const string CornerMarksReach = "THEME053";
    public const string EffectReach = "THEME054";
    public const string Hairline = "THEME055";
    public const string CompactCapsule = "THEME056";
    public const string AnimatedDashedBorder = "THEME057";
    public const string ContrastText = "THEME060";
    public const string ContrastTint = "THEME061";
    public const string ContrastMuted = "THEME062";
    public const string NoMetadata = "THEME080";
    public const string Animations = "THEME090";
}
