// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Serialization;
using UrDeck.Sdk;
using UrDeck.Sdk.Settings;

namespace UrDeck.Widgets.List;

/// <summary>
/// The list widget's settings: flat, typed and each with a default. <see cref="Title"/> follows one rule: absent (null) is the
/// reading's label, a text is used as given and an empty text hides the title. Properties this version does not know stay in
/// <see cref="WidgetConfig.ExtensionData"/> and are kept across load and save.
/// </summary>
public class ListConfig : WidgetConfig
{
    /// <summary>The list shown, a reading id of a list reading (<c>system:processes/top</c>).</summary>
    [JsonPropertyName("reading")]
    [Setting("Reading", Help = "The list to show: a reading that holds rows, such as the busiest processes.", Required = true)]
    [Reading(Filter = ReadingFilter.ListsOnly)]
    public string? Reading { get; set; }

    /// <summary>The text above the rows. Absent: the reading's label; an empty text hides it.</summary>
    [JsonPropertyName("title")]
    [Setting("Title", Help = "The text above the rows. Left out, it is the list's own name; none hides it.", AllowsNone = true, FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
    public string? Title { get; set; }

    /// <summary>What happens when the rows do not fit: <c>scroll</c> or <c>rotate</c>. Anything else counts as <c>scroll</c>.</summary>
    [JsonPropertyName("overflow")]
    [Setting("When there are more rows than fit", Help = "Scroll: tap the lower or upper part to page. Rotate: the rows move up on their own, one by one.")]
    [Option("scroll", "Scroll")]
    [Option("rotate", "Rotate")]
    public string Overflow { get; set; } = "scroll";

    /// <summary>At most this many rows are taken from the list, in the provider's order. Absent: all.</summary>
    [JsonPropertyName("maxRows")]
    [Setting("Most rows", Help = "Show at most this many rows, from the top of the list. Left out, every row.", Fallback = "All")]
    [Number(Min = 1, Max = 200, Step = 1)]
    public int? MaxRows { get; set; }

    /// <summary>Whether the rows' pictures are drawn.</summary>
    [JsonPropertyName("showImages")]
    [Setting("Pictures", Help = "Draw the picture of each row (a friend's avatar, a game's icon) where the list has one.")]
    public bool ShowImages { get; set; } = true;
}
