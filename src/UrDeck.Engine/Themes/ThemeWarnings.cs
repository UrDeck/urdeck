// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

/// <summary>
/// The findings already reported for one loaded theme. A theme is resolved several times (page, dock, each display change),
/// and each finding is reported once; loading the theme again starts afresh, which is wanted after the user edits it.
/// </summary>
internal sealed class ThemeWarnings(IThemeFindings sink)
{
    private readonly HashSet<string> _reported = [];

    /// <summary>Reports <paramref name="finding"/> unless a finding with this <paramref name="once"/> key was reported before.</summary>
    public void Once(string once, ThemeFinding finding)
    {
        lock (_reported)
            if (!_reported.Add(once))
                return;
        sink.Report(finding);
    }

    /// <summary>A finding about <paramref name="key"/>, in the deck's <c>Theme 'name': </c> form; a warning unless <paramref name="severity"/> says more.</summary>
    public void Once(string once, string code, ThemeSeverity severity, string? key, string message) =>
        Once(once, new ThemeFinding(code, severity, key, message));

    public void Once(string once, string code, string? key, string message) =>
        Once(once, code, ThemeSeverity.Warning, key, message);

    /// <summary>As <see cref="Once(string, string, string?, string)"/>, at information level: a finding the engine has dealt with.</summary>
    public void InfoOnce(string once, string code, string? key, string message) =>
        Once(once, code, ThemeSeverity.Info, key, message);
}
