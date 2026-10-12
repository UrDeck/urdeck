// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

/// <summary>
/// Gives the deck window square corners. Windows 11 rounds the corners of a top-level window, and the desktop
/// compositor then clips the window while it draws it. With a swap chain in the window (a moving background) that clip
/// blended the translucent content over the swap chain three times along the window's diagonal: a bright dotted line
/// through every translucent card. The deck covers its display, so it has no use for round corners either.
/// </summary>
internal static class WindowCorners
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;

    public static void Square(nint hwnd)
    {
        int preference = DwmwcpDoNotRound;
        int result = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        if (result == 0)
            UrDeckLog.Info("Window corners are square");
        else
            UrDeckLog.Info($"Window corners left to Windows (0x{result:X8}); Windows 10 has no round corners");
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
