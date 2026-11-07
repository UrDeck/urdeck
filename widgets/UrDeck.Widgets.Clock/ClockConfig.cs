// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;
using UrDeck.Sdk.Settings;

namespace UrDeck.Widgets.Clock.Config;

/// <summary>
/// The clock's settings. It has no colour or size setting: the size comes from the card and the look from the theme. A
/// <c>textColor</c> or <c>fontSize</c> key left in an older file is ignored and kept.
/// </summary>
public class ClockConfig : WidgetConfig
{
    /// <summary>"24h" or "12h".</summary>
    [Setting("Hours", Help = "Whether the hour runs to 24 or 12 with AM and PM.")]
    [Option("24h", "24-hour")]
    [Option("12h", "12-hour")]
    public string Format { get; set; } = "24h";

    /// <summary>Shows the date under the time.</summary>
    [Setting("Show the date", Help = "Shows the date under the time.")]
    public bool ShowDate { get; set; } = true;

    /// <summary>"simple" (default) or "flap", a split-flap display; anything else is "simple".</summary>
    [Setting("Style", Help = "Plain digits, or a split-flap display whose digits flip when the minute changes.")]
    [Option("simple", "Simple")]
    [Option("flap", "Split-flap")]
    public string Style { get; set; } = "simple";

    /// <summary>
    /// "date", "label" or "none": a header row above the time (and no date below it). Absent, or a value this version does not
    /// know, keeps the date below the time.
    /// </summary>
    [Setting("Header", Help = "A row of small text above the time instead of the date below it: the date and the week or subtitle, a label and the date, or nothing.")]
    [Option("", "Under the time")]
    [Option("date", "Date row")]
    [Option("label", "Label row")]
    [Option("none", "None")]
    public string? Header { get; set; }

    /// <summary>Text in the header: the label with <c>header: "label"</c>, the right-hand text with "date".</summary>
    [Setting("Subtitle", Help = "Text for the header, for example Rig online. The label row puts it at the left; the date row puts it at the right in place of the week.", Fallback = "None")]
    [ShownWhen("header", "date", "label")]
    public string? Subtitle { get; set; }

    /// <summary>Shows the ISO week ("Wk 42") in the header.</summary>
    [Setting("Show the week", Help = "Shows the ISO week (Wk 42) in the header.")]
    public bool ShowWeek { get; set; }

    /// <summary>Draws the seconds small after the time; the card then draws once a second.</summary>
    [Setting("Show seconds", Help = "Draws the seconds small after the time. The card then draws once a second.")]
    public bool ShowSeconds { get; set; }

    /// <summary>Blinks the colon once a second; the card then draws once a second.</summary>
    [Setting("Blink the colon", Help = "Blinks the colon once a second. The card then draws once a second.")]
    public bool BlinkSeparator { get; set; }
}
