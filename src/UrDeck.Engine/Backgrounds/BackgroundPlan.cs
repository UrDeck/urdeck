// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Backgrounds;

/// <summary>Where a background was chosen.</summary>
public enum BackgroundOrigin
{
    /// <summary>The configuration's <c>background</c>.</summary>
    Configuration,

    /// <summary>The theme's <c>background.default</c> shader.</summary>
    ThemeDefault,

    /// <summary>The theme's <c>background.static</c> picture, gradient or colour.</summary>
    ThemeStill,

    /// <summary>Nothing but the theme's background colour.</summary>
    ThemeColour,
}

public static class BackgroundOriginText
{
    /// <summary>The words the log uses: <c>configuration</c>, <c>theme default</c>, <c>theme still background</c>, <c>theme colour</c>.</summary>
    public static string Label(this BackgroundOrigin origin) => origin switch
    {
        BackgroundOrigin.Configuration => "configuration",
        BackgroundOrigin.ThemeDefault => "theme default",
        BackgroundOrigin.ThemeStill => "theme still background",
        _ => "theme colour",
    };
}

/// <summary>
/// One thing the deck can show behind its cards, and where it came from. <see cref="Settings"/> is a shader or a picture
/// (the configuration's, or built from the theme's default), <see cref="Still"/> a theme's still background; neither is
/// the plain background colour, which needs nothing to build and cannot fail.
/// </summary>
public sealed record BackgroundCandidate(BackgroundOrigin Origin, BackgroundSettings? Settings, ThemeBackgroundStaticDefinition? Still)
{
    public bool IsColour => Settings == null && Still == null;
}

/// <summary>
/// What the deck shows behind its cards, decided in one place: the candidates in the order they are tried, and the first
/// that can be built is shown. The order is the configuration's <c>background</c>, the theme's default, the theme's still
/// background and the plain background colour, with these rules:
/// <list type="bullet">
/// <item>No <c>background</c> in the configuration: all the theme's choices, in that order.</item>
/// <item>A shader or a picture: it first, and the theme's still background only as what a failure falls back to; the
/// theme's default is never used instead of what the user chose.</item>
/// <item><c>none</c>: the background colour alone.</item>
/// <item>A <c>background</c> that could not be read: the theme's still background, then the colour.</item>
/// </list>
/// Below the user's motion level <see cref="MotionLevel.Full"/> a shader candidate has speed 0, which the background draws
/// still: the user's or Windows' wish for less motion, not the theme's ceiling.
/// </summary>
public sealed class BackgroundPlan
{
    private BackgroundPlan(LoadedTheme theme, IReadOnlyList<BackgroundCandidate> candidates)
    {
        Theme = theme;
        Candidates = candidates;
    }

    public LoadedTheme Theme { get; }

    /// <summary>In the order they are tried; the last is always the background colour.</summary>
    public IReadOnlyList<BackgroundCandidate> Candidates { get; }

    /// <param name="settings">The configuration's <c>background</c>.</param>
    /// <param name="theme">The loaded theme.</param>
    /// <param name="userMotion">The user's motion level (<see cref="MotionDecision.User"/>), before the theme's ceiling.</param>
    public static BackgroundPlan Resolve(BackgroundSettings settings, LoadedTheme theme, MotionLevel userMotion = MotionLevel.Full)
    {
        double? speed = userMotion < MotionLevel.Full ? 0 : null;
        var candidates = new List<BackgroundCandidate>();
        var themeBackground = theme.Definition.Background;

        bool userChose = settings.Kind is BackgroundKind.Shader or BackgroundKind.Image;
        if (userChose)
            candidates.Add(new BackgroundCandidate(BackgroundOrigin.Configuration, Slowed(settings, speed), null));

        if (!settings.IsDisabled)
        {
            if (!settings.IsExplicit && !string.IsNullOrWhiteSpace(themeBackground?.Default?.Name))
                candidates.Add(new BackgroundCandidate(BackgroundOrigin.ThemeDefault, Slowed(BackgroundSettings.ForThemeShader(themeBackground!.Default!.Name!), speed), null));

            if (DeckBackground.HasStill(themeBackground?.Static))
                candidates.Add(new BackgroundCandidate(BackgroundOrigin.ThemeStill, null, themeBackground!.Static));
        }

        candidates.Add(new BackgroundCandidate(BackgroundOrigin.ThemeColour, null, null));
        return new BackgroundPlan(theme, candidates);
    }

    private static BackgroundSettings Slowed(BackgroundSettings settings, double? speed) => speed is { } s ? settings.WithSpeed(s) : settings;

    /// <summary>
    /// A text that is equal while the same candidates would be built the same way: each candidate's own key, where it came
    /// from (with the theme's name for a theme's choice) and the scrim. Null when there is nothing to build, so a theme that
    /// defines nothing and a configuration with no background leave the layer empty. Builds and decodes nothing.
    /// </summary>
    public string? RequestKey(ShaderStore store, string appDirectory)
    {
        var parts = new List<string>();
        foreach (var candidate in Candidates)
        {
            if (candidate.IsColour)
                continue;

            string key = candidate.Settings != null
                ? DeckBackground.RequestKey(candidate.Settings, Theme, store, appDirectory)!
                : DeckBackground.StillRequestKey(candidate.Still!, Theme);
            string theme = candidate.Origin == BackgroundOrigin.Configuration ? "" : $"@{Theme.Name}";
            parts.Add($"{candidate.Origin}{theme}:{key}");
        }

        return parts.Count == 0 ? null : string.Join(" > ", parts) + DeckBackground.ScrimKey(Theme);
    }

    /// <summary>
    /// Builds the first candidate, from <paramref name="firstCandidate"/> on, that can be shown, logging one warning for
    /// each one that cannot. Null when only the background colour is left. A picture is decoded later, by
    /// <see cref="DeckBackground.PrepareAsync"/>; the caller goes on from <see cref="DeckBackground.CandidateIndex"/> + 1 if
    /// that fails.
    /// </summary>
    public DeckBackground? Build(ShaderStore store, string appDirectory, int firstCandidate = 0)
    {
        for (int index = firstCandidate; index < Candidates.Count; index++)
        {
            var candidate = Candidates[index];
            if (candidate.IsColour)
                return null;

            var background = candidate.Settings != null
                ? DeckBackground.Create(candidate.Settings, Theme, store, appDirectory)
                : CreateStill(candidate.Still!);
            if (background == null)
                continue;

            background.Adopt(candidate.Origin, index, Theme);
            return background;
        }

        return null;
    }

    private DeckBackground? CreateStill(ThemeBackgroundStaticDefinition still)
    {
        var background = DeckBackground.TryCreateStill(still, Theme, out string? failure);
        if (background == null && failure != null)
            UrDeckLog.Warn($"Background: {failure}");
        return background;
    }
}
