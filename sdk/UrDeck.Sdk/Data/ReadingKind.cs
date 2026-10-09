// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>What a reading measures. It decides how the formatter shows the value; new kinds may be added.</summary>
public enum ReadingKind
{
    /// <summary>A plain number, shown with the descriptor's <see cref="ReadingDescriptor.Unit"/>.</summary>
    Number,
    /// <summary>A share from 0 to 100.</summary>
    Percent,
    /// <summary>Degrees Celsius; the formatter converts for display.</summary>
    Temperature,
    /// <summary>A text.</summary>
    Text,
    /// <summary>An on/off state.</summary>
    OnOff,
    /// <summary>An instant with the UTC offset it is read in; shown as the time of day.</summary>
    Time,
}

/// <summary>Where a reading stands. See the data-providers spec, "Reading States".</summary>
public enum ReadingState
{
    /// <summary>The value comes from the provider's most recent successful sample or publication.</summary>
    Ok,
    /// <summary>Subscribed to, and the provider has not reported on it since the subscription began.</summary>
    Pending,
    /// <summary>The provider's latest sample failed or skipped the reading, and an earlier value exists.</summary>
    Stale,
    /// <summary>The reading cannot be supplied; <see cref="Reading.Reason"/> says why.</summary>
    Unavailable,
}

/// <summary>A unit a kind can be shown in. Grows together with the kinds.</summary>
public enum DisplayUnit
{
    Celsius,
    Fahrenheit,
}
