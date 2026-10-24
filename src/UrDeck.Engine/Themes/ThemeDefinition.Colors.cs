// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizeColors(ThemeValueCheck check)
    {
        if (Colors is not { } c)
            return;
        c.Background = check.Color("colors.background", c.Background);
        c.CardFill = check.Color("colors.cardFill", c.CardFill);
        c.CardBorder = check.Color("colors.cardBorder", c.CardBorder);
        c.Text = check.Color("colors.text", c.Text);
        c.TextMuted = check.Color("colors.textMuted", c.TextMuted);
        c.Accent = check.Color("colors.accent", c.Accent);
        c.AccentDim = check.Color("colors.accentDim", c.AccentDim);
        c.Good = check.Color("colors.good", c.Good);
        c.Warning = check.Color("colors.warning", c.Warning);
        c.Critical = check.Color("colors.critical", c.Critical);
        c.BackgroundPalette = check.Palette("colors.backgroundPalette", c.BackgroundPalette);
        c.Sanitize(check, "colors");
    }
}

public sealed partial class ThemeColorsDefinition
{
    public string? Background { get; set; }
    public string? CardFill { get; set; }
    public string? CardBorder { get; set; }
    public string? Text { get; set; }
    public string? TextMuted { get; set; }
    public string? Accent { get; set; }
    public string? AccentDim { get; set; }
    public string? Good { get; set; }
    public string? Warning { get; set; }
    public string? Critical { get; set; }

    /// <summary>One to four colours for the backgrounds' shaders; absent means derived from this theme's own colours.</summary>
    public List<string>? BackgroundPalette { get; set; }

    /// <summary>A second accent for gradients, markers, redlines, tab highlights. Default: accent.</summary>
    public string? Accent2 { get; set; }

    /// <summary>Low-to-high load colours. Default: [good, warning, critical]. Used when gauge.colorBy is 'ramp'.</summary>
    public List<string>? Ramp { get; set; }

    /// <summary>Fill colours that cards cycle through when card.tint is 'cycle' (Pop).</summary>
    public List<string>? CardTints { get; set; }

    /// <summary>Text colour used on top of an accent or tint fill. Default: whichever of text/background contrasts most with the fill.</summary>
    public string? Ink { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Accent2 = check.Color($"{path}.accent2", Accent2);
        Ramp = check.Colors($"{path}.ramp", Ramp, 2, 5);
        CardTints = check.Colors($"{path}.cardTints", CardTints, 1, 8);
        Ink = check.Color($"{path}.ink", Ink);
    }

    internal ThemeColorsDefinition MergeOver(ThemeColorsDefinition? b)
    {
        var result = new ThemeColorsDefinition
        {
            Background = Background ?? b?.Background,
            CardFill = CardFill ?? b?.CardFill,
            CardBorder = CardBorder ?? b?.CardBorder,
            Text = Text ?? b?.Text,
            TextMuted = TextMuted ?? b?.TextMuted,
            Accent = Accent ?? b?.Accent,
            AccentDim = AccentDim ?? b?.AccentDim,
            Good = Good ?? b?.Good,
            Warning = Warning ?? b?.Warning,
            Critical = Critical ?? b?.Critical,

            // Not taken from the baseline, unlike every other member: a light theme written before backgrounds existed would
            // otherwise get the dark theme's palette. A theme without one has its own colours' palette derived.
            BackgroundPalette = BackgroundPalette,
        };
        result.Accent2 = Accent2 ?? b?.Accent2;
        result.Ramp = Ramp;
        result.CardTints = CardTints;
        result.Ink = Ink ?? b?.Ink;
        return result;
    }
}
