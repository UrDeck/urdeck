// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Machine;

/// <summary>One graphics adapter as Windows lists it.</summary>
/// <param name="Luid">The adapter's locally unique id, the key of every graphics kernel query.</param>
/// <param name="Name">The adapter's name as Windows reports it.</param>
/// <param name="DedicatedMemory">Dedicated video memory in bytes; decides the order of the adapters.</param>
/// <param name="VendorId">The PCI vendor id, for example 0x10DE for NVIDIA.</param>
/// <param name="IsSoftware">True for an adapter without hardware, such as the Microsoft Basic Render Driver.</param>
/// <param name="PciBus">The PCI location, which a vendor library uses to find the same device.</param>
/// <param name="PciDevice">The PCI device number.</param>
/// <param name="PciFunction">The PCI function number.</param>
internal sealed record GpuAdapter(long Luid, string Name, ulong DedicatedMemory, uint VendorId, bool IsSoftware, int PciBus, int PciDevice, int PciFunction)
{
    /// <summary>The PCI location in the form vendor libraries take, for example <c>00000000:01:00.0</c>.</summary>
    public string PciBusId => $"00000000:{PciBus:X2}:{PciDevice:X2}.{PciFunction:X}";

    /// <summary>The vendor's name for the log.</summary>
    public string Vendor => VendorId switch
    {
        0x10DE => "NVIDIA",
        0x1002 or 0x1022 => "AMD",
        0x8086 => "Intel",
        0x1414 => "Microsoft",
        _ => $"vendor 0x{VendorId:X4}",
    };
}
