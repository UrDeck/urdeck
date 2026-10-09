// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

/// <summary>
/// Keeps the deck window from ever taking focus: touching or clicking it must not pull the foreground away from the
/// application the user is running. The window never has keyboard focus as a result, so it gets no Escape.
/// </summary>
internal sealed class WindowFocus
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const uint WmMouseActivate = 0x0021;
    private const nint MaNoActivate = 3;

    private readonly SubclassProc _proc;

    private WindowFocus() => _proc = OnMessage;

    /// <summary>True unless <c>URDECK_ACTIVATABLE=1</c> asks for a window that can take focus (development).</summary>
    public static bool IsEnabled => Environment.GetEnvironmentVariable("URDECK_ACTIVATABLE") != "1";

    /// <summary>Applies the style and the activation answer; the returned object must stay alive with the window.</summary>
    public static WindowFocus? Apply(nint hwnd)
    {
        if (!IsEnabled)
        {
            UrDeckLog.Info("Window can take focus (URDECK_ACTIVATABLE=1); Escape closes it");
            return null;
        }

        var focus = new WindowFocus();
        long style = GetWindowLongPtr(hwnd, GwlExStyle);
        SetWindowLongPtr(hwnd, GwlExStyle, style | WsExNoActivate);
        if (!SetWindowSubclass(hwnd, focus._proc, 1, 0))
            UrDeckLog.Warn("Could not subclass the window to refuse activation by mouse");
        UrDeckLog.Info("Window never takes focus (WS_EX_NOACTIVATE); close it from the taskbar");
        return focus;
    }

    private static nint OnMessage(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data) =>
        message == WmMouseActivate ? MaNoActivate : DefSubclassProc(hwnd, message, wParam, lParam);

    private delegate nint SubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern long GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern long SetWindowLongPtr(nint hwnd, int index, long value);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
}
