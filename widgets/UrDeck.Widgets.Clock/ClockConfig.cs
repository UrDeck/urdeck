// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;

namespace UrDeck.Widgets.Clock.Config;

public class ClockConfig : WidgetConfig
{
    /// <summary>"24h" or "12h".</summary>
    public string Format { get; set; } = "24h";
    public bool ShowDate { get; set; } = true;
    /// <summary>Hex color override (e.g. "#ffcc00"); null uses the theme text color.</summary>
    public string? TextColor { get; set; }
    /// <summary>Multiplier on the size-derived font size.</summary>
    public double FontSize { get; set; } = 1.0;
    /// <summary>"simple" (default) or "flap", a split-flap display; anything else is "simple".</summary>
    public string Style { get; set; } = "simple";
}
