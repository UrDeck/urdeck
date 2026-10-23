// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.ComponentModel.DataAnnotations;
using UrDeck.Sdk;

namespace UrDeck.Widgets.Clock.Config;

public class ClockConfig : WidgetConfig
{
    /// <summary>"24h" or "12h".</summary>
    [AllowedValues("24h", "12h")]
    public string Format { get; set; } = "24h";
    public bool ShowDate { get; set; } = true;
    /// <summary>Hex color override (e.g. "#ffcc00"); null uses the theme text color.</summary>
    public string? TextColor { get; set; }
    /// <summary>Multiplier on the size-derived font size.</summary>
    public double FontSize { get; set; } = 1.0;
    /// <summary>"simple" (default) or "flap", a split-flap display; anything else is "simple".</summary>
    [AllowedValues("simple", "flap")]
    public string Style { get; set; } = "simple";

    /// <summary>
    /// "date", "label" or "none": a header row above the time (and no date below it). Absent, or a value this version does not
    /// know, keeps the date below the time.
    /// </summary>
    [AllowedValues("date", "label", "none")]
    public string? Header { get; set; }

    /// <summary>Text in the header: the label with <c>header: "label"</c>, the right-hand text with "date".</summary>
    public string? Subtitle { get; set; }

    /// <summary>Shows the ISO week ("Wk 42") in the header.</summary>
    public bool ShowWeek { get; set; }

    /// <summary>Draws the seconds small after the time; the card then draws once a second.</summary>
    public bool ShowSeconds { get; set; }

    /// <summary>Blinks the colon once a second; the card then draws once a second.</summary>
    public bool BlinkSeparator { get; set; }
}
