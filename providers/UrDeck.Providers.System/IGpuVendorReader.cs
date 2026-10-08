// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Machine;

/// <summary>
/// Power and clock of one adapter from its vendor's own library. This is the place to add AMD and Intel: implement it and
/// return it from <see cref="IGpuPlatform.CreateVendorReader"/>; no id and no page changes.
/// </summary>
internal interface IGpuVendorReader
{
    /// <summary>What the values come from, for the log, for example <c>nvml.dll</c>.</summary>
    string Source { get; }

    /// <summary>Loads the library and finds the adapter's device. False with a reason when that cannot be done.</summary>
    bool TryOpen(GpuAdapter adapter, out string failure);

    /// <summary>The power the adapter draws in watts; null when the library has no value.</summary>
    double? PowerWatts();

    /// <summary>The current core clock in MHz; null when the library has no value.</summary>
    double? ClockMegahertz();

    /// <summary>Releases the library.</summary>
    void Close();
}
