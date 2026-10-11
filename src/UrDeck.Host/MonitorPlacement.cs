// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using System.Runtime.InteropServices;
using UrDeck.Engine.Display;

namespace UrDeck.Host;

/// <summary>
/// The Win32 side of display handling: which displays are connected, in physical pixels (the process is per-monitor
/// DPI aware, see app.manifest), and where the window's content area is.
/// </summary>
internal static class MonitorPlacement
{
    /// <summary>
    /// The connected displays in <c>EnumDisplayMonitors</c> order. Bounds and the primary flag come from there; the
    /// name, the model and the device path come from the display configuration, joined by the GDI device name. A
    /// display without a target there (a remote session, an indirect display driver) keeps its device name as path.
    /// </summary>
    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var targets = GetTargets();
        var displays = new List<DisplayInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                double scaling = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1;
                bool known = targets.TryGetValue(info.szDevice, out var target);
                string fallback = info.szDevice.TrimStart('\\', '.');
                displays.Add(new DisplayInfo(
                    known && target.Name.Length > 0 ? target.Name : known && target.Model.Length > 0 ? target.Model : fallback,
                    known ? target.Model : "",
                    known && target.Path.Length > 0 ? target.Path : info.szDevice,
                    info.szDevice,
                    info.rcMonitor.ToRectangle(),
                    scaling,
                    (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            }
            return true;
        }, IntPtr.Zero);
        return displays;
    }

    /// <summary>Friendly name, model and device path of each active display, by GDI device name.</summary>
    private static Dictionary<string, (string Name, string Model, string Path)> GetTargets()
    {
        var targets = new Dictionary<string, (string Name, string Model, string Path)>(StringComparer.OrdinalIgnoreCase);

        // The configuration can change between asking for the sizes and asking for the paths.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
                break;
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            int result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (result == ERROR_INSUFFICIENT_BUFFER)
                continue;
            if (result != 0)
                break;

            for (int i = 0; i < pathCount; i++)
            {
                var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                source.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                source.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                source.header.adapterId = paths[i].sourceInfo.adapterId;
                source.header.id = paths[i].sourceInfo.id;

                var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                target.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                target.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                target.header.adapterId = paths[i].targetInfo.adapterId;
                target.header.id = paths[i].targetInfo.id;

                if (GetSourceName(ref source) != 0 || GetTargetName(ref target) != 0)
                    continue;

                // A cloned source has several targets; the first one names it.
                string model = (target.flags & EdidIdsValid) != 0 ? EdidModel(target.edidManufactureId, target.edidProductCodeId) : "";
                targets.TryAdd(source.viewGdiDeviceName, (target.monitorFriendlyDeviceName ?? "", model, target.monitorDevicePath ?? ""));
            }
            break;
        }

        return targets;
    }

    /// <summary>
    /// The model code as Windows writes it in hardware ids (<c>RTD1100</c>): three letters packed in five bits each,
    /// stored big-endian in the EDID, and the product code in hexadecimal.
    /// </summary>
    private static string EdidModel(ushort manufacturer, ushort product)
    {
        int packed = ((manufacturer & 0xFF) << 8) | (manufacturer >> 8);
        Span<char> letters = stackalloc char[3];
        for (int i = 0; i < 3; i++)
        {
            int letter = (packed >> (10 - 5 * i)) & 0x1F;
            letters[i] = letter is >= 1 and <= 26 ? (char)('A' + letter - 1) : '?';
        }
        return $"{new string(letters)}{product:X4}";
    }

    /// <summary>The scaling of the monitor the window is on, as the window sees it (1 is 100%).</summary>
    public static double GetWindowScaling(nint hwnd)
    {
        uint dpi = hwnd == IntPtr.Zero ? 0 : GetDpiForWindow(hwnd);
        return dpi > 0 ? dpi / 96.0 : 1;
    }

    /// <summary>
    /// Moves the window so that its content area covers <paramref name="bounds"/> exactly (physical pixels). The window
    /// keeps a thin non-client frame, so the content area is measured after each placement and the window is grown by
    /// what is missing; this converges in a pass or two and never hard-codes the frame size. With <paramref name="nudge"/> the
    /// window is first resized by a pixel so that the content re-lays out.
    /// </summary>
    public static void Cover(nint hwnd, Rectangle bounds, bool nudge)
    {
        if (hwnd == IntPtr.Zero)
            return;

        var window = bounds;
        if (nudge)
        {
            // A scale change can leave XAML with the old content size even though the window is right; a real resize
            // makes it lay out again.
            SetWindowPos(hwnd, IntPtr.Zero, window.X, window.Y, window.Width + 1, window.Height + 1, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        for (int pass = 0; pass < 3; pass++)
        {
            SetWindowPos(hwnd, IntPtr.Zero, window.X, window.Y, window.Width, window.Height, SWP_NOZORDER | SWP_NOACTIVATE);
            var client = GetContentBounds(hwnd);
            if (client.IsEmpty || client == bounds)
                return;
            // Move each edge of the window by how far the matching edge of the content area is from the target.
            int left = window.Left - (client.Left - bounds.Left);
            int top = window.Top - (client.Top - bounds.Top);
            int right = window.Right - (client.Right - bounds.Right);
            int bottom = window.Bottom - (client.Bottom - bounds.Bottom);
            window = Rectangle.FromLTRB(left, top, right, bottom);
        }
    }

    /// <summary>The window's content (client) area in screen coordinates, physical pixels.</summary>
    public static Rectangle GetContentBounds(nint hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out var client))
            return Rectangle.Empty;
        var origin = new POINT { X = 0, Y = 0 };
        if (!ClientToScreen(hwnd, ref origin))
            return Rectangle.Empty;
        return new Rectangle(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
    }

    private const int MONITORINFOF_PRIMARY = 1;
    private const int MDT_EFFECTIVE_DPI = 0;
    private const uint QDC_ONLY_ACTIVE_PATHS = 2;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    private const uint EdidIdsValid = 0x4;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public uint refreshRateNumerator;
        public uint refreshRateDenominator;
        public uint scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    // Only the paths are read; the mode entries are 64 bytes each and must be there for the call.
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] DISPLAYCONFIG_PATH_INFO[] paths, ref uint modeCount, [Out] DISPLAYCONFIG_MODE_INFO[] modes, IntPtr currentTopologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int GetSourceName(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME packet);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int GetTargetName(ref DISPLAYCONFIG_TARGET_DEVICE_NAME packet);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
}
