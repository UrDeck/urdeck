// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    /// <summary>The 1100 pixel panel width, in cells, at which a pattern's marks must stay three pixels apart.</summary>
    private const double MinPatternRatio = 3;

    private const double DefaultPatternSpacing = 0.13;
    private const double DefaultPatternThickness = 0.01;

    /// <summary>
    /// The rules between values that the file alone decides. They run on the merged definition (a border width of 0 is
    /// usually the default theme's), after every value has been checked on its own, and each fails only what it names.
    /// </summary>
    internal void CheckRules(IThemeFindings sink)
    {
        var check = new ThemeValueCheck(sink);
        CheckBorder(check);
        CheckPattern(check);
        CheckCornerMarks(check);
        NoteAnimations(check, ContinuousAnimations());
    }

    private void CheckBorder(ThemeValueCheck check)
    {
        if (Card is not { } card)
            return;

        var styled = new List<string>();
        if (card.BorderStyle != null && card.BorderStyle != "none")
            styled.Add("card.borderStyle");
        if (card.BorderGradient != null)
            styled.Add("card.borderGradient");
        if (card.BorderAnimate is { } animate && animate.Mode != null && animate.Mode != "none")
            styled.Add("card.borderAnimate");
        if (styled.Count > 0 && card.BorderWidth == 0)
            check.Rule(ThemeCodes.BorderWithoutWidth, "card.borderWidth", $"{string.Join(", ", styled)} has no effect with card.borderWidth 0.");

        // A spinning border turns a conic gradient's start angle; there is nothing to turn in any other.
        if (card.BorderAnimate is { Mode: "spin" } && (card.BorderGradient?.Kind ?? "linear") != "conic")
        {
            check.Rule(ThemeCodes.SpinWithoutConic, "card.borderAnimate.mode", "card.borderAnimate 'spin' needs card.borderGradient of kind 'conic'; the animation is switched off.");
            card.BorderAnimate = null;
        }
    }

    private void CheckPattern(ThemeValueCheck check)
    {
        if (Pattern is not { } pattern || pattern.Kind is null or "none")
            return;

        double spacing = pattern.Spacing ?? DefaultPatternSpacing;
        double thickness = pattern.Thickness ?? DefaultPatternThickness;
        if (spacing / thickness < MinPatternRatio)
            check.Rule(ThemeCodes.PatternDense, "pattern.spacing", $"pattern is too dense: spacing {spacing} is less than {MinPatternRatio} times thickness {thickness}, so its marks run together.");
    }

    private void CheckCornerMarks(ThemeValueCheck check)
    {
        if (Card is { CornerMarks: { Inset: < 0 } marks, Gap: { } gap } && gap < -marks.Inset)
            check.Rule(ThemeCodes.CornerMarksReach, "card.cornerMarks.inset", $"card.cornerMarks.inset {marks.Inset} reaches past card.gap {gap}; the marks would overlap the next card.");
    }

    /// <summary>The theme-driven animations that run all the time, as <c>motion.level</c> will cap them.</summary>
    private int ContinuousAnimations() =>
        Card is { BorderAnimate.Mode: "spin" or "pulse" } ? 1 : 0;

    /// <summary>Several continuous animations are allowed; the log says so, because each costs frames.</summary>
    internal static void NoteAnimations(ThemeValueCheck check, int count)
    {
        if (count > 1)
            check.Note(ThemeCodes.Animations, null, $"{count} continuous animations are on; each repaints on every frame.");
    }
}
