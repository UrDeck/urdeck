// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using System.Runtime.InteropServices;
using UrDeck.Engine.Config;

namespace UrDeck.Host;

/// <summary>A display in physical pixels (the process is per-monitor DPI aware, see app.manifest).</summary>
internal sealed record MonitorInfo(string DeviceName, Rectangle Bounds, Rectangle WorkArea, bool IsPrimary)
{
    public override string ToString() =>
        $"{DeviceName} {Bounds.Width}x{Bounds.Height} at {Bounds.X},{Bounds.Y}{(IsPrimary ? " (primary)" : "")}";
}

internal static class MonitorPlacement
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new MonitorInfo(
                    info.szDevice,
                    info.rcMonitor.ToRectangle(),
                    info.rcWork.ToRectangle(),
                    (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            }
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    /// <summary>
    /// Picks the target monitor from config: <c>monitorName</c> ("primary", "tallest", "widest", "largest",
    /// or a device name like "DISPLAY1"), falling back to the 1-based <c>monitor</c> index, then primary.
    /// </summary>
    public static MonitorInfo Select(UrDeckConfig config, IReadOnlyList<MonitorInfo> monitors)
    {
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        string? name = config.MonitorName?.Trim();

        MonitorInfo? chosen = name?.ToLowerInvariant() switch
        {
            null or "" => null,
            "primary" => primary,
            "tallest" => monitors.Where(m => m.Bounds.Height > m.Bounds.Width).MaxBy(m => m.Bounds.Height),
            "widest" => monitors.MaxBy(m => m.Bounds.Width),
            "largest" => monitors.MaxBy(m => (long)m.Bounds.Width * m.Bounds.Height),
            _ => monitors.FirstOrDefault(m => m.DeviceName.EndsWith(name!, StringComparison.OrdinalIgnoreCase)),
        };

        if (chosen == null && config.Monitor >= 1 && config.Monitor <= monitors.Count)
            chosen = monitors[config.Monitor - 1];

        return chosen ?? primary;
    }

    /// <summary>
    /// Moves the window so that its content area covers <paramref name="bounds"/> exactly (physical pixels). The window
    /// keeps a thin non-client frame, so the content area is measured after each placement and the window is grown by
    /// what is missing; this converges in a pass or two and never hard-codes the frame size. With <paramref name="nudge"/> the
    /// window is first resized by a pixel so that the content re-lays out.
    /// </summary>
    public static void Cover(nint hwnd, Rectangle bounds, bool nudge = false)
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
}
