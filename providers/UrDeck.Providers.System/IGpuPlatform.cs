// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Machine;

/// <summary>
/// What the GPU readings ask of the machine. Windows implements it for real; tests supply a fake one. Nothing here keeps
/// a handle: every call is self-contained, so nothing has to be released when the provider stops.
/// </summary>
internal interface IGpuPlatform
{
    /// <summary>Every adapter Windows lists, software ones included, in Windows' order.</summary>
    IReadOnlyList<GpuAdapter> Adapters();

    /// <summary>The current time in 100 ns units, the unit of the engines' running times.</summary>
    long Now();

    /// <summary>The number of engines (3D, compute, copy, video decode, ...) of the adapter; 0 when unknown.</summary>
    int EngineCount(GpuAdapter adapter);

    /// <summary>How long the engine has run in total, in 100 ns units.</summary>
    bool TryReadRunningTime(GpuAdapter adapter, int engine, out long runningTime);

    /// <summary>The temperature in degrees Celsius; false when the driver reports none.</summary>
    bool TryReadTemperature(GpuAdapter adapter, out double celsius);

    /// <summary>The reader for power and clock of this adapter's vendor; null when there is none yet.</summary>
    IGpuVendorReader? CreateVendorReader(GpuAdapter adapter);
}
